using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CmdPalLinks.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Services;

/// <summary>
/// 编辑器后端：只绑 127.0.0.1 的极简 HTTP 服务，页面与接口同源，
/// 避免 file:// 下的跨域与文件权限问题。端口随机、令牌随机，两者都不落盘、不写日志。
/// </summary>
internal static class EditorServer
{
    private const int MaxBodyBytes = 5 * 1024 * 1024;

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(20);

    private static readonly object Gate = new();

    private static Session? _session;

    /// <summary>返回可直接用浏览器打开的地址（含一次性令牌）；启动失败返回 null。</summary>
    public static string? Start(LibraryStore store)
    {
        lock (Gate)
        {
            if (_session is { Alive: true })
            {
                _session.Touch();
                return _session.Url;
            }

            try
            {
                _session = Session.Start(store, IdleTimeout);
                return _session.Url;
            }
            catch (Exception ex)
            {
                HostLog.Error($"editor server start failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>扩展退出或空闲超时后调用；重复调用安全。</summary>
    public static void Shutdown()
    {
        Session? session;
        lock (Gate)
        {
            session = _session;
            _session = null;
        }

        session?.Dispose();
    }

    private sealed class Session : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly LibraryStore _store;
        private readonly string _token;
        private readonly string _html;
        private readonly TimeSpan _idle;
        private readonly CancellationTokenSource _cts = new();
        private readonly int _port;
        private long _lastActivityTicks;
        private int _disposed;

        private Session(TcpListener listener, LibraryStore store, string token, string html, TimeSpan idle, int port)
        {
            _listener = listener;
            _store = store;
            _token = token;
            _html = html;
            _idle = idle;
            _port = port;
            Url = $"http://127.0.0.1:{port}/?token={Uri.EscapeDataString(token)}";
            _lastActivityTicks = Environment.TickCount64;
        }

        public string Url { get; }

        public bool Alive => Volatile.Read(ref _disposed) == 0 && !_cts.IsCancellationRequested;

        public static Session Start(LibraryStore store, TimeSpan idle)
        {
            var htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "links-editor.html");
            var html = File.ReadAllText(htmlPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(html))
            {
                throw new InvalidDataException("editor html is empty");
            }

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var session = new Session(listener, store, CreateToken(), html, idle, port);
            _ = Task.Run(session.RunAsync);
            return session;
        }

        public void Touch() => _lastActivityTicks = Environment.TickCount64;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _cts.Cancel();
            }
            catch (Exception)
            {
                // 退出路径上的取消失败无需上报。
            }

            try
            {
                _listener.Stop();
            }
            catch (Exception)
            {
                // 已经停止。
            }

            _cts.Dispose();
        }

        private async Task RunAsync()
        {
            try
            {
                while (true)
                {
                    var remaining = _idle - TimeSpan.FromMilliseconds(Environment.TickCount64 - _lastActivityTicks);
                    if (remaining <= TimeSpan.Zero)
                    {
                        HostLog.Info("editor server idle timeout, stopping");
                        break;
                    }

                    var acceptTask = _listener.AcceptTcpClientAsync(_cts.Token).AsTask();
                    var delayTask = Task.Delay(remaining, _cts.Token);
                    var completed = await Task.WhenAny(acceptTask, delayTask).ConfigureAwait(false);

                    if (completed != acceptTask)
                    {
                        break;
                    }

                    var client = await acceptTask.ConfigureAwait(false);
                    Touch();
                    await HandleAsync(client).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常关停。
            }
            catch (Exception ex)
            {
                HostLog.Error($"editor server loop failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Dispose();
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using (client)
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
            using (var stream = client.GetStream())
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    client.ReceiveTimeout = 15000;
                    var head = await ReadHeadAsync(stream, timeout.Token).ConfigureAwait(false);
                    if (head is null)
                    {
                        return;
                    }

                    var request = ParseHead(head);
                    if (request is null || !IsLoopbackHost(request.Host))
                    {
                        await WriteAsync(stream, 403, "Forbidden", "text/plain", "拒绝访问", timeout.Token).ConfigureAwait(false);
                        return;
                    }

                    await RouteAsync(stream, request, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 客户端超时或扩展退出。
                }
                catch (Exception ex)
                {
                    HostLog.Error($"editor request failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private async Task RouteAsync(NetworkStream stream, Request request, CancellationToken ct)
        {
            var path = request.Path;

            if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(path, "/", StringComparison.Ordinal) || string.Equals(path, "/editor", StringComparison.Ordinal)))
            {
                if (!TokenMatches(request.Query.GetValueOrDefault("token")))
                {
                    await WriteAsync(stream, 403, "Forbidden", "text/plain", "令牌无效或缺失", ct).ConfigureAwait(false);
                    return;
                }

                await WriteAsync(stream, 200, "OK", "text/html", _html, ct).ConfigureAwait(false);
                return;
            }

            if (string.Equals(path, "/favicon.ico", StringComparison.Ordinal))
            {
                await WriteEmptyAsync(stream, 204, "No Content", ct).ConfigureAwait(false);
                return;
            }

            if (!string.Equals(path, "/api/links", StringComparison.Ordinal))
            {
                await WriteAsync(stream, 404, "Not Found", "text/plain", "未找到", ct).ConfigureAwait(false);
                return;
            }

            if (!TokenMatches(request.Header("x-cmdpallinks-token")))
            {
                await WriteAsync(stream, 403, "Forbidden", "text/plain", "令牌无效或缺失", ct).ConfigureAwait(false);
                return;
            }

            if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(stream, 200, "OK", BuildItemsJson(), ct).ConfigureAwait(false);
                return;
            }

            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await WriteAsync(stream, 405, "Method Not Allowed", "text/plain", "只支持 GET / POST", ct).ConfigureAwait(false);
                return;
            }

            if (request.ContentLength > MaxBodyBytes)
            {
                await WriteAsync(stream, 413, "Payload Too Large", "text/plain", "请求体超过 5 MB", ct).ConfigureAwait(false);
                return;
            }

            if (request.ContentLength <= 0)
            {
                await WriteJsonAsync(stream, 400, "Bad Request", ErrorJson("缺少请求体", 0), ct).ConfigureAwait(false);
                return;
            }

            var body = await ReadBodyAsync(stream, request.ContentLength, ct).ConfigureAwait(false);
            if (body is null)
            {
                await WriteJsonAsync(stream, 400, "Bad Request", ErrorJson("请求体不完整", 0), ct).ConfigureAwait(false);
                return;
            }

            EditorSaveRequest? payload;
            try
            {
                payload = JsonSerializer.Deserialize<EditorSaveRequest>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                await WriteJsonAsync(stream, 400, "Bad Request", ErrorJson($"JSON 解析失败：{ex.Message}", 0), ct).ConfigureAwait(false);
                return;
            }

            if (payload?.Items is null)
            {
                await WriteJsonAsync(stream, 400, "Bad Request", ErrorJson("缺少 items 数组", 0), ct).ConfigureAwait(false);
                return;
            }

            if (payload.Items.Count > 10_000)
            {
                await WriteJsonAsync(stream, 400, "Bad Request", ErrorJson("超过 10000 条，请分批保存", 0), ct).ConfigureAwait(false);
                return;
            }

            var incoming = new List<LinkItem>(payload.Items.Count);
            for (var i = 0; i < payload.Items.Count; i++)
            {
                var dto = payload.Items[i];
                if (dto is null)
                {
                    await WriteJsonAsync(stream, 400, "Bad Request", ErrorJson("条目为空", i + 1), ct).ConfigureAwait(false);
                    return;
                }

                Guid id = Guid.Empty;
                if (!string.IsNullOrWhiteSpace(dto.Id))
                {
                    _ = Guid.TryParse(dto.Id, out id);
                }

                incoming.Add(new LinkItem
                {
                    Id = id,
                    Kind = dto.Type ?? string.Empty,
                    Target = dto.Target ?? string.Empty,
                    Title = dto.Title ?? string.Empty,
                    Aliases = SplitList(dto.Alias),
                    Tags = SplitList(dto.Tags),
                    IconEmoji = dto.Emoji ?? string.Empty,
                    IconColor = dto.Color ?? string.Empty,
                    IconKind = dto.IconKind ?? IconKinds.Auto,
                    Folder = LibraryStore.NormalizeFolderName(dto.Folder),
                    IconSource = LibraryStore.NormalizeIconSource(dto.IconSrc),
                });
            }

            var folders = payload.Folders?
                .Where(folder => folder is not null)
                .Select(folder => folder!)
                .ToArray();
            var result = _store.ReplaceAll(incoming, folders);
            if (!result.Success)
            {
                await WriteJsonAsync(stream, 400, "Bad Request", ErrorJson(result.Message, result.ErrorLine), ct).ConfigureAwait(false);
                return;
            }

            HostLog.Info($"editor saved: total={result.Total} added={result.Added} updated={result.Updated} removed={result.Removed}");
            await WriteJsonAsync(stream, 200, "OK", BuildSavedJson(result), ct).ConfigureAwait(false);
        }

        private byte[] BuildItemsJson()
        {
            var items = new List<object>();
            foreach (var item in _store.Items)
            {
                items.Add(new
                {
                    id = item.Id.ToString(),
                    type = item.Kind,
                    target = item.Target,
                    title = item.Title,
                    alias = string.Join('|', item.Aliases ?? Array.Empty<string>()),
                    tags = string.Join('|', item.Tags ?? Array.Empty<string>()),
                    emoji = item.IconEmoji ?? string.Empty,
                    color = string.Equals(item.IconKind, IconKinds.Transparent, StringComparison.OrdinalIgnoreCase)
                        ? string.Empty
                        : item.IconColor ?? string.Empty,
                    iconKind = IconKinds.Normalize(item.IconKind),
                    folder = item.Folder ?? string.Empty,
                    iconSrc = item.IconSource ?? string.Empty,
                });
            }

            return JsonSerializer.SerializeToUtf8Bytes(
                new { ok = true, count = items.Count, folders = _store.Folders, items },
                JsonOptions);
        }

        private static byte[] BuildSavedJson(ReplaceResult result)
            => JsonSerializer.SerializeToUtf8Bytes(new
            {
                ok = true,
                saved = result.Total,
                added = result.Added,
                updated = result.Updated,
                removed = result.Removed,
                message = result.Message,
            }, JsonOptions);

        private static byte[] ErrorJson(string message, int line)
            => JsonSerializer.SerializeToUtf8Bytes(new { ok = false, error = message, line }, JsonOptions);

        private bool IsLoopbackHost(string? host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return false;
            }

            var expected = $"127.0.0.1:{_port}";
            return string.Equals(host, expected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        private bool TokenMatches(string? provided)
        {
            if (string.IsNullOrEmpty(provided))
            {
                return false;
            }

            var expected = Encoding.UTF8.GetBytes(_token);
            var actual = Encoding.UTF8.GetBytes(provided);
            return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string CreateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string[] SplitList(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Array.Empty<string>();
        }

        var parts = text.Contains('|') ? text.Split('|') : text.Split('，');
        var result = new List<string>();
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var duplicate = false;
            foreach (var existing in result)
            {
                if (string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
            {
                result.Add(trimmed);
            }
        }

        return result.ToArray();
    }

    private static async Task<byte[]?> ReadHeadAsync(NetworkStream stream, CancellationToken ct)
    {
        const int maxHead = 16 * 1024;
        var buffer = new List<byte>(1024);
        var one = new byte[1];
        while (buffer.Count < maxHead)
        {
            var read = await stream.ReadAsync(one.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            buffer.Add(one[0]);
            var n = buffer.Count;
            if (n >= 4 && buffer[n - 4] == 13 && buffer[n - 3] == 10 && buffer[n - 2] == 13 && buffer[n - 1] == 10)
            {
                return buffer.ToArray();
            }
        }

        return null;
    }

    private static async Task<byte[]?> ReadBodyAsync(NetworkStream stream, int length, CancellationToken ct)
    {
        var body = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(body.AsMemory(offset, length - offset), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            offset += read;
        }

        return body;
    }

    private static Request? ParseHead(byte[] head)
    {
        var text = Encoding.UTF8.GetString(head);
        var lines = text.Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0)
        {
            return null;
        }

        var parts = lines[0].Split(' ');
        if (parts.Length < 3)
        {
            return null;
        }

        var method = parts[0];
        var rawTarget = parts[1];
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        var queryIndex = rawTarget.IndexOf('?');
        var path = queryIndex >= 0 ? rawTarget[..queryIndex] : rawTarget;
        var query = queryIndex >= 0 ? ParseQuery(rawTarget[(queryIndex + 1)..]) : new Dictionary<string, string>(StringComparer.Ordinal);
        var contentLength = 0;
        if (headers.TryGetValue("content-length", out var lengthText))
        {
            _ = int.TryParse(lengthText, out contentLength);
        }

        headers.TryGetValue("host", out var host);
        return new Request(method, path, query, headers, host, contentLength);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = eq >= 0 ? pair[..eq] : pair;
            var value = eq >= 0 ? pair[(eq + 1)..] : string.Empty;
            try
            {
                key = Uri.UnescapeDataString(key.Replace('+', ' '));
                value = Uri.UnescapeDataString(value.Replace('+', ' '));
            }
            catch (Exception)
            {
                // 非法转义按原文使用。
            }

            result[key] = value;
        }

        return result;
    }

    private static async Task WriteJsonAsync(NetworkStream stream, int status, string reason, byte[] body, CancellationToken ct)
        => await WriteAsync(stream, status, reason, "application/json", body, ct).ConfigureAwait(false);

    private static async Task WriteAsync(NetworkStream stream, int status, string reason, string contentType, string text, CancellationToken ct)
        => await WriteAsync(stream, status, reason, contentType, Encoding.UTF8.GetBytes(text), ct).ConfigureAwait(false);

    private static async Task WriteEmptyAsync(NetworkStream stream, int status, string reason, CancellationToken ct)
    {
        var header = $"HTTP/1.1 {status} {reason}\r\nContent-Length: 0\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(header), ct).ConfigureAwait(false);
    }

    private static async Task WriteAsync(NetworkStream stream, int status, string reason, string contentType, byte[] body, CancellationToken ct)
    {
        var header = new StringBuilder()
            .Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n")
            .Append("Content-Type: ").Append(contentType).Append("; charset=utf-8\r\n")
            .Append("Content-Length: ").Append(body.Length).Append("\r\n")
            .Append("Cache-Control: no-store\r\n")
            .Append("X-Content-Type-Options: nosniff\r\n")
            .Append("X-Frame-Options: DENY\r\n")
            .Append("Referrer-Policy: no-referrer\r\n")
            .Append("Connection: close\r\n\r\n")
            .ToString();

        await stream.WriteAsync(Encoding.UTF8.GetBytes(header), ct).ConfigureAwait(false);
        if (body.Length > 0)
        {
            await stream.WriteAsync(body, ct).ConfigureAwait(false);
        }
    }

    private sealed record Request(
        string Method,
        string Path,
        Dictionary<string, string> Query,
        Dictionary<string, string> Headers,
        string? Host,
        int ContentLength)
    {
        public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
    }

    private sealed class EditorSaveRequest
    {
        public List<EditorItemDto?>? Items { get; set; }

        /// <summary>编辑器提交的完整文件夹顺序（可含空文件夹）；缺省 = 保留现有文件夹。</summary>
        public List<string?>? Folders { get; set; }
    }

    private sealed class EditorItemDto
    {
        public string? Id { get; set; }

        public string? Type { get; set; }

        public string? Target { get; set; }

        public string? Title { get; set; }

        public string? Alias { get; set; }

        public string? Tags { get; set; }

        public string? Emoji { get; set; }

        public string? Color { get; set; }

        public string? IconKind { get; set; }

        public string? Folder { get; set; }

        public string? IconSrc { get; set; }
    }
}
