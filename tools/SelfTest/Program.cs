using System;
using System.Collections.Generic;
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
