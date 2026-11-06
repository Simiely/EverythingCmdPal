using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            Console.WriteLine("usage: StoreHarness <dir> concurrency|corrupt|dedupe|bench <N>");
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
