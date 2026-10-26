using System;
using System.Collections.Generic;
using System.IO;
using EverythingCmdPal.Pages;
using EverythingCmdPal.Services;

namespace SelfTest;

/// <summary>
/// Console self-test for SearchSession pagination/dedupe semantics.
/// Runs without Everything or the Command Palette: dotnet run --project tools/SelfTest
/// </summary>
internal static class Program
{
    private static int _failures;

    private static void Check(string name, bool condition)
    {
        Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {name}");
        if (!condition) _failures++;
    }

    private static int Main()
    {
        var fake = new FakeSearch();
        var session = new SearchSession(fake, pageSize: 3);

        // Page 1: source returns F0,F1,F2 + duplicate F0; session must dedupe.
        var page1 = session.Start("test", SortOrder.SizeDesc);
        Check("page1 count == 3", page1.Count == 3);
        Check("page1 has F0,F1,F2", Has(page1, "C:\\F0") && Has(page1, "C:\\F1") && Has(page1, "C:\\F2"));
        Check("page1 NextOffset == 4 (raw)", session.NextOffset == 4);
        Check("page1 Total == 7", session.Total == 7);
        Check("page1 HasMore", session.HasMore);

        // Page 2: last page.
        var page2 = session.LoadNext();
        Check("page2 count == 3", page2.Count == 3);
        Check("page2 has F4,F5,F6", Has(page2, "C:\\F4") && Has(page2, "C:\\F5") && Has(page2, "C:\\F6"));
        Check("page2 NextOffset == 7", session.NextOffset == 7);
        Check("page2 HasMore == false", !session.HasMore);

        // Over-exhaust: must be a no-op and not change state.
        var page3 = session.LoadNext();
        Check("page3 empty", page3.Count == 0);
        Check("page3 offset unchanged", session.NextOffset == 7);

        // Whole result set contains no duplicate paths.
        var all = new List<string>();
        foreach (var h in page1) all.Add(h.FullPath);
        foreach (var h in page2) all.Add(h.FullPath);
        Check("no duplicate paths across pages", new HashSet<string>(all).Count == all.Count);

        Check("fake searched exactly 2 times", fake.Calls == 2);
        Check("pagination reuses the sort", fake.LastSort == SortOrder.SizeDesc);

        // 连接诊断（1.3.3.3）：连不上时不能把原因说成「没有找到结果」。
        Check("classify: connected", ConnectionDiagnostics.Classify(true, false) == EverythingConnectionState.Connected);
        Check("classify: no process -> NotRunning", ConnectionDiagnostics.Classify(false, false) == EverythingConnectionState.NotRunning);
        Check("classify: process without pipe -> RunningWithoutPipe", ConnectionDiagnostics.Classify(false, true) == EverythingConnectionState.RunningWithoutPipe);
        Check("text: NotRunning", ConnectionDiagnostics.FailureText(EverythingConnectionState.NotRunning).Title == "Everything 未运行");
        Check("text: RunningWithoutPipe mentions IPC", ConnectionDiagnostics.FailureText(EverythingConnectionState.RunningWithoutPipe).Title.Contains("IPC"));
        Check("text: Connected -> no-result hint", ConnectionDiagnostics.FailureText(EverythingConnectionState.Connected).Title == "没有找到结果");

        // 预览缓存淘汰（1.3.3.3）：超条数/体积上限时按最后写入时间删最旧，未超限时不动。
        var cacheDir = Path.Combine(Path.GetTempPath(), "EverythingCmdPal-SelfTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);
        try
        {
            for (var i = 0; i < 5; i++)
            {
                var file = Path.Combine(cacheDir, $"p{i}.png");
                File.WriteAllBytes(file, new byte[16]);
                File.SetLastWriteTimeUtc(file, new DateTime(2026, 10, 26, 0, i, 0, DateTimeKind.Utc));
            }

            Check("prune: no-op under limits", PreviewService.PruneCache(cacheDir, 10, long.MaxValue) == 0);
            Check("prune: deletes oldest down to count cap", PreviewService.PruneCache(cacheDir, 3, long.MaxValue) == 2);
            Check("prune: newest three survive",
                File.Exists(Path.Combine(cacheDir, "p2.png"))
                && File.Exists(Path.Combine(cacheDir, "p3.png"))
                && File.Exists(Path.Combine(cacheDir, "p4.png")));
            Check("prune: byte cap also enforced", PreviewService.PruneCache(cacheDir, 100, 20) == 2);
        }
        finally
        {
            try { Directory.Delete(cacheDir, true); } catch { }
        }

        // 浏览页查询构造（1.3.4）：parent: 限直接子项，筛选词拼在后面。
        Check("browse: no filter -> parent only",
            BrowsePage.BuildBrowseQuery(@"C:\dir\", null) == "parent:\"C:\\dir\"");
        Check("browse: trailing slash normalized",
            BrowsePage.BuildBrowseQuery(@"C:\dir\\", "") == "parent:\"C:\\dir\"");
        Check("browse: filter appended",
            BrowsePage.BuildBrowseQuery(@"C:\dir", "  readme ") == "parent:\"C:\\dir\" readme");

        Console.WriteLine(_failures == 0 ? "ALL PASS" : $"{_failures} FAILURES");
        return _failures == 0 ? 0 : 1;
    }

    private static bool Has(IReadOnlyList<SearchHit> hits, string path)
    {
        foreach (var h in hits)
        {
            if (h.FullPath == path) return true;
        }

        return false;
    }
}

/// <summary>Deterministic fake: total 7 items (F0..F6), page overlaps nothing, but page 1 repeats F0.</summary>
internal sealed class FakeSearch : IEverythingSearch
{
    public int Calls { get; private set; }

    public SortOrder.SortSpec LastSort { get; private set; }

    public IReadOnlyList<SearchHit> Search(string query, int offset, int count, SortOrder.SortSpec sort, out int total)
    {
        Calls++;
        LastSort = sort;
        total = 7;

        var list = new List<SearchHit>();
        for (var i = offset; i < Math.Min(offset + count, 7); i++)
        {
            list.Add(new SearchHit(@"C:\F" + i, false));
        }

        if (offset == 0 && count >= 3)
        {
            list.Add(new SearchHit(@"C:\F0", false));
        }

        return list;
    }
}
