using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CmdPalLinks.Models;
using CmdPalLinks.Services;

namespace StoreHarness;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("usage: StoreHarness <dir> concurrency|corrupt|dedupe|bench <N>|editor <seedJson>");
            return 2;
        }

        var dir = args[0];
        var mode = args[1];
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "links.json");

        switch (mode)
        {
            case "concurrency":
                return Concurrency(path);
            case "corrupt":
                return Corrupt(path);
            case "dedupe":
                return Dedupe(path, dir);
            case "bench":
                return Bench(path, args.Length > 2 ? int.Parse(args[2]) : 1000);
            case "editor":
                return EditorRoundTrip(path, args.Length > 2 ? args[2] : null);
            case "csv":
                return CsvRoundTrip(path, args.Length > 2 ? args[2] : null);
            default:
                Console.WriteLine($"unknown mode: {mode}");
                return 2;
        }
    }

    private static int Concurrency(string path)
    {
        DeleteStoreFiles(path);
        var store = new LibraryStore(path);
        store.Load();

        var outcomes = new ConcurrentBag<AddResult>();
        var tasks = Enumerable.Range(0, 20)
            .Select(thread => Task.Run(() =>
            {
                for (var i = 0; i < 20; i++)
                {
                    outcomes.Add(store.Add("url", string.Empty, $"https://example.com/c/{thread}/{i}"));
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);

        var reloaded = new LibraryStore(path);
        var load = reloaded.Load();
        var added = outcomes.Count(result => result.Status == AddStatus.Added);
        var duplicates = outcomes.Count(result => result.Status == AddStatus.Duplicate);
        var failed = outcomes.Count(result => result.Status == AddStatus.SaveFailed || result.Status == AddStatus.InvalidTarget);

        Console.WriteLine($"concurrency: added={added} duplicate={duplicates} failed={failed} reloaded={load.ItemCount} source={load.Source} elapsed={load.ElapsedMs:F2}ms");
        return added == 400 && duplicates == 0 && failed == 0 && load.ItemCount == 400 && load.Source == "main" ? 0 : 1;
    }

    private static int Corrupt(string path)
    {
        DeleteStoreFiles(path);
        var store = new LibraryStore(path);
        store.Load();
        for (var i = 0; i < 3; i++)
        {
            store.Add("url", $"测试 {i}", $"https://example.com/corrupt/{i}");
        }

        File.Copy(path, path + ".bak", true);
        var full = File.ReadAllText(path, Encoding.UTF8);
        File.WriteAllText(path, full[..Math.Min(20, full.Length)], Encoding.UTF8);

        var recovered = new LibraryStore(path);
        var recoveredLoad = recovered.Load();
        Console.WriteLine($"truncated main + valid bak: source={recoveredLoad.Source} count={recoveredLoad.ItemCount}");

        File.WriteAllText(path, "{ not json", Encoding.UTF8);
        File.WriteAllText(path + ".bak", "{\"items\": [", Encoding.UTF8);

        var empty = new LibraryStore(path);
        var emptyLoad = empty.Load();
        Console.WriteLine($"both corrupt: source={emptyLoad.Source} count={emptyLoad.ItemCount}");
        Console.WriteLine($"corrupt files kept: main={File.Exists(path)} bak={File.Exists(path + ".bak")}");

        return recoveredLoad.Source == "bak" && recoveredLoad.ItemCount == 3 && emptyLoad.Source == "empty" && emptyLoad.ItemCount == 0 ? 0 : 1;
    }

    private static int Dedupe(string path, string dir)
    {
        DeleteStoreFiles(path);
        var store = new LibraryStore(path);
        store.Load();

        var first = store.Add("url", "A", "https://Example.COM/path?x=1#frag");
        var second = store.Add("url", "B", "https://example.com/path?x=1");
        var third = store.Add("url", "C", "https://example.com/path?x=2");

        var file = Path.Combine(dir, "dedupe-file.txt");
        File.WriteAllText(file, "x");
        var fourth = store.Add("file", "D", file.ToUpperInvariant());
        var fifth = store.Add("file", "E", file);

        Console.WriteLine($"url: {first.Status}/{second.Status}/{third.Status}");
        Console.WriteLine($"file: {fourth.Status}/{fifth.Status} count={store.Items.Count}");

        var ok = first.Status == AddStatus.Added
            && second.Status == AddStatus.Duplicate
            && third.Status == AddStatus.Added
            && fourth.Status == AddStatus.Added
            && fifth.Status == AddStatus.Duplicate
            && store.Items.Count == 3;

        Console.WriteLine(ok ? "dedupe PASS" : "dedupe FAIL");
        return ok ? 0 : 1;
    }

    private static int Bench(string path, int count)
    {
        DeleteStoreFiles(path);
        var data = new LibraryData { SchemaVersion = 1, UpdatedAt = DateTimeOffset.UtcNow };
        for (var i = 0; i < count; i++)
        {
            data.Items.Add(new LinkItem
            {
                Kind = "url",
                Title = $"Item {i}",
                Target = $"https://site{i % 97}.example.com/page/{i}",
                Aliases = new[] { $"alias{i % 50}" },
                Tags = new[] { $"tag{i % 20}" },
            });
        }

        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8);
        var store = new LibraryStore(path);
        var load = store.Load();
        var reload = Stopwatch.StartNew();
        var parsed = store.Load();
        reload.Stop();
        Console.WriteLine($"bench N={count}: first={load.ElapsedMs:F2}ms second={reload.Elapsed.TotalMilliseconds:F2}ms items={parsed.ItemCount}");
        return parsed.ItemCount == count ? 0 : 1;
    }

    /// <summary>
    /// 编辑器后端往返自测：从 seed（真实 links.json 的副本）起服务，走真实 HTTP 接口
    /// （含 token 校验）加一条、再删一条，核对条目数与 icon 字段是否原样保留。
    /// </summary>
    private static int EditorRoundTrip(string path, string? seed)
    {
        DeleteStoreFiles(path);
        if (seed is not null && File.Exists(seed))
        {
            File.Copy(seed, path, overwrite: true);
        }

        var store = new LibraryStore(path);
        store.Load();
        var before = store.Items.Select(item => new Snapshot(item)).ToArray();

        var url = EditorServer.Start(store);
        if (url is null)
        {
            Console.WriteLine("editor FAIL: server did not start");
            return 1;
        }

        var uri = new Uri(url);
        var token = Uri.UnescapeDataString(uri.Query.TrimStart('?').Split('&').First(part => part.StartsWith("token=", StringComparison.Ordinal))["token=".Length..]);
        var endpoint = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        Console.WriteLine($"editor server: {endpoint} (token len={token.Length})");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        try
        {
            var noToken = http.GetAsync(endpoint + "/api/links").GetAwaiter().GetResult();
            Console.WriteLine($"GET without token: HTTP {(int)noToken.StatusCode}");
            var noTokenRejected = (int)noToken.StatusCode is 401 or 403;

            var get = Send(http, HttpMethod.Get, endpoint + "/api/links", token);
            var getBody = get.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var getDoc = JsonDocument.Parse(getBody);
            var items = JsonSerializer.Deserialize<List<ApiItem>>(
                getDoc.RootElement.GetProperty("items").GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            var serverCount = getDoc.RootElement.GetProperty("count").GetInt32();
            Console.WriteLine($"GET /api/links: HTTP {(int)get.StatusCode} count={serverCount}");
            foreach (var item in items)
            {
                Console.WriteLine($"  {item.Type,-6} emoji='{item.Emoji}' color='{item.Color}' kind='{item.IconKind}' title='{item.Title}' target={item.Target}");
            }

            var test = new ApiItem(string.Empty, "url", "https://example.com/__editor-roundtrip__", "编辑器写回测试", string.Empty, string.Empty, "🧪", string.Empty, "emoji");
            var added = Post(http, endpoint, token, items.Append(test).ToArray());
            Console.WriteLine($"POST add: {added}");

            var afterAdd = LoadRaw(path);
            Console.WriteLine($"after add: items={afterAdd} bak={File.Exists(path + ".bak")}");

            var restored = Post(http, endpoint, token, items.ToArray());
            Console.WriteLine($"POST remove: {restored}");

            var afterRemove = LoadRaw(path);
            var reloaded = new LibraryStore(path);
            reloaded.Load();
            var current = reloaded.Items.Select(item => new Snapshot(item)).ToArray();
            var iconsKept = before.All(old => current.Any(item => item.Id == old.Id && item.Kind == old.Kind && item.Emoji == old.Emoji && item.Color == old.Color && item.Title == old.Title));
            Console.WriteLine($"after remove: items={afterRemove} icons kept={iconsKept}");

            // 改一条图标：emoji + 色块，核对写回值，并确认其它条目 icon 字段没被清空。
            var edited = items.ToArray();
            var targetIndex = Array.FindIndex(edited, item => item.Type == "url");
            edited[targetIndex] = edited[targetIndex] with { Emoji = "📁", Color = "#2563EB", IconKind = "emoji" };
            var editResponse = Post(http, endpoint, token, edited);
            Console.WriteLine($"POST icon edit: {editResponse}");

            var afterEdit = new LibraryStore(path);
            afterEdit.Load();
            var editedId = Guid.Parse(edited[targetIndex].Id);
            var editedItem = afterEdit.Items.First(item => item.Id == editedId);
            var editStored = editedItem.IconEmoji == "📁" && editedItem.IconColor == "#2563EB" && editedItem.IconKind == "emoji";
            var othersKept = before.Where(old => old.Id != editedId).All(old =>
                afterEdit.Items.Any(item => item.Id == old.Id && item.Kind == old.Kind && item.IconEmoji == old.Emoji && item.IconColor == old.Color && item.Title == old.Title));
            Console.WriteLine($"icon edit: kind='{editedItem.IconKind}' emoji='{editedItem.IconEmoji}' color='{editedItem.IconColor}' stored={editStored} others kept={othersKept}");

            // 无色块的 block 形态：保存时不能被降级成 auto（CSV/编辑器往返常见丢图标点）。
            var blockItems = items.ToArray();
            blockItems[targetIndex] = blockItems[targetIndex] with { Emoji = string.Empty, Color = string.Empty, IconKind = "block" };
            Post(http, endpoint, token, blockItems);
            var afterBlock = new LibraryStore(path);
            afterBlock.Load();
            var blockItem = afterBlock.Items.First(item => item.Id == editedId);
            var blockKept = blockItem.IconKind == "block" && string.IsNullOrEmpty(blockItem.IconColor) && string.IsNullOrEmpty(blockItem.IconEmoji);
            Console.WriteLine($"block without color: kind='{blockItem.IconKind}' kept={blockKept}");

            Post(http, endpoint, token, items.ToArray());
            var final = new LibraryStore(path);
            final.Load();
            var finalOk = final.Items.Count == before.Length
                && before.All(old => final.Items.Any(item => item.Id == old.Id && item.Kind == old.Kind && item.IconEmoji == old.Emoji && item.IconColor == old.Color && item.Title == old.Title));
            Console.WriteLine($"restored: items={final.Items.Count} fields kept={finalOk}");

            var ok = serverCount == before.Length
                && items.Count == before.Length
                && noTokenRejected
                && afterAdd == before.Length + 1
                && File.Exists(path + ".bak")
                && afterRemove == before.Length
                && iconsKept
                && editStored
                && othersKept
                && blockKept
                && finalOk;
            Console.WriteLine(ok ? "editor round-trip PASS" : "editor round-trip FAIL");
            return ok ? 0 : 1;
        }
        finally
        {
            EditorServer.Shutdown();
        }
    }

    /// <summary>
    /// CSV 往返自测：8 列（含 icon）导出 → 导入后 icon 字段必须一致；旧版 7 列文件仍可导入。
    /// </summary>
    private static int CsvRoundTrip(string path, string? seed)
    {
        DeleteStoreFiles(path);
        if (seed is not null && File.Exists(seed))
        {
            File.Copy(seed, path, overwrite: true);
        }

        var fixtures = new[]
        {
            new LinkItem { Kind = "url", Target = "https://a.example.com/", Title = "A", IconKind = "block" },
            new LinkItem { Kind = "url", Target = "https://b.example.com/", Title = "B", IconKind = "emoji", IconEmoji = "📁", IconColor = "#2563EB" },
            new LinkItem { Kind = "url", Target = "https://c.example.com/", Title = "C", IconKind = "transparent" },
            new LinkItem { Kind = "url", Target = "https://d.example.com/", Title = "D", IconKind = "auto" },
        };

        var exported = Encoding.UTF8.GetString(CsvCodec.ToCsvBytes(fixtures));
        var header = exported.Split("\r\n")[0];
        var targetPath = Path.Combine(Path.GetDirectoryName(path)!, "csv-target", "links.json");
        DeleteStoreFiles(targetPath);
        var targetStore = new LibraryStore(targetPath);
        targetStore.Load();
        var parsed = CsvCodec.ParseCsv(exported, "fixture.csv", targetStore);
        var imported = parsed.Preview?.Addable.Select(row => row.Item).ToArray() ?? [];
        var iconsRoundTrip = parsed.FatalError is null
            && imported.Length == fixtures.Length
            && fixtures.Zip(imported).All(pair =>
                pair.First.IconKind == pair.Second.IconKind
                && (pair.First.IconEmoji ?? string.Empty) == (pair.Second.IconEmoji ?? string.Empty)
                && (pair.First.IconColor ?? string.Empty) == (pair.Second.IconColor ?? string.Empty));
        Console.WriteLine($"export header: {header}");
        foreach (var item in imported)
        {
            Console.WriteLine($"  imported: kind='{item.IconKind}' emoji='{item.IconEmoji}' color='{item.IconColor}' target={item.Target}");
        }

        Console.WriteLine($"8-col round-trip icons: {iconsRoundTrip} fatal={parsed.FatalError}");

        var legacy = "type,target,title,alias,tags,emoji,color\r\nurl,https://e.example.com/,E,,,\U0001F4C1,none\r\n";
        var legacyPath = Path.Combine(Path.GetDirectoryName(path)!, "csv-legacy", "links.json");
        DeleteStoreFiles(legacyPath);
        var legacyStore = new LibraryStore(legacyPath);
        legacyStore.Load();
        var legacyParsed = CsvCodec.ParseCsv(legacy, "legacy.csv", legacyStore);
        var legacyItem = legacyParsed.Preview?.Addable.FirstOrDefault()?.Item;
        var legacyOk = legacyParsed.FatalError is null && legacyItem is not null
            && legacyItem.IconKind == "emoji" && legacyItem.IconEmoji == "📁" && string.IsNullOrEmpty(legacyItem.IconColor);
        Console.WriteLine($"legacy 7-col import: ok={legacyOk} fatal={legacyParsed.FatalError}");

        var ok = iconsRoundTrip && legacyOk;
        Console.WriteLine(ok ? "csv round-trip PASS" : "csv round-trip FAIL");
        return ok ? 0 : 1;
    }

    private sealed record ApiItem(string Id, string Type, string Target, string Title, string Alias, string Tags, string Emoji, string Color, string IconKind);

    private sealed record Snapshot(Guid Id, string Kind, string? Emoji, string? Color, string? Title)
    {
        public Snapshot(LinkItem item)
            : this(item.Id, item.Kind, item.IconEmoji, item.IconColor, item.Title)
        {
        }
    }

    private static HttpResponseMessage Send(HttpClient http, HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-CmdPalLinks-Token", token);
        return http.Send(request);
    }

    private static string Post(HttpClient http, string endpoint, string token, ApiItem[] items)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint + "/api/links");
        request.Headers.Add("X-CmdPalLinks-Token", token);
        request.Content = new StringContent(JsonSerializer.Serialize(new { items }, JsonOptions), Encoding.UTF8, "application/json");
        using var response = http.Send(request);
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return $"HTTP {(int)response.StatusCode} {body}";
    }

    private static int LoadRaw(string path)
    {
        var data = JsonSerializer.Deserialize<LibraryData>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
        return data?.Items.Count ?? -1;
    }

    private static void DeleteStoreFiles(string path)
    {
        foreach (var file in new[] { path, path + ".bak", path + ".tmp" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
