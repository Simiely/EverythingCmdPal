using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CmdPalLinks.Models;

namespace CmdPalLinks.Services;

internal sealed class IndexedLink
{
    public required LinkItem Item { get; init; }

    public required string Title { get; init; }

    public required string[] Aliases { get; init; }

    public required string[] Tags { get; init; }

    public required string Domain { get; init; }

    public required string FileName { get; init; }

    public required string Target { get; init; }
}

internal readonly record struct SearchOutcome(LinkItem[] Items, double ElapsedMs, int IndexedCount);

internal static class SearchEngine
{
    public static IndexedLink[] BuildIndex(IEnumerable<LinkItem> items)
    {
        return items
            .Where(item => LinkKinds.IsValid(item.Kind) && !string.IsNullOrWhiteSpace(item.Target))
            .Select(item => new IndexedLink
            {
                Item = item,
                Title = ToLower(item.Title),
                Aliases = ToLowerArray(item.Aliases),
                Tags = ToLowerArray(item.Tags),
                Domain = DomainOf(item),
                FileName = FileNameOf(item),
                Target = ToLower(item.Target),
            })
            .ToArray();
    }

    public static SearchOutcome Search(IndexedLink[] index, string query, int maxResults = 50, int maxRecent = 30)
    {
        var stopwatch = Stopwatch.StartNew();
        var trimmed = (query ?? string.Empty).Trim();
        LinkItem[] results;

        if (trimmed.Length == 0)
        {
            results = index
                .Select(entry => entry.Item)
                .OrderByDescending(item => item.LastOpenedAt.HasValue)
                .ThenByDescending(item => item.LastOpenedAt ?? DateTimeOffset.MinValue)
                .ThenByDescending(item => item.OpenCount)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(maxRecent)
                .ToArray();
        }
        else
        {
            var terms = trimmed
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(term => term.ToLowerInvariant())
                .ToArray();

            var scored = new List<(LinkItem Item, int Score)>();
            foreach (var entry in index)
            {
                var total = 0;
                var matchedAll = true;
                foreach (var term in terms)
                {
                    var termScore = ScoreTerm(entry, term);
                    if (termScore == 0)
                    {
                        matchedAll = false;
                        break;
                    }

                    total += termScore;
                }

                if (matchedAll)
                {
                    scored.Add((entry.Item, total));
                }
            }

            results = scored
                .OrderByDescending(pair => pair.Score)
                .ThenByDescending(pair => Frecency(pair.Item))
                .ThenByDescending(pair => pair.Item.UpdatedAt)
                .ThenBy(pair => pair.Item.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(maxResults)
                .Select(pair => pair.Item)
                .ToArray();
        }

        stopwatch.Stop();
        return new SearchOutcome(results, stopwatch.Elapsed.TotalMilliseconds, index.Length);
    }

    private static int ScoreTerm(IndexedLink entry, string term)
    {
        if (term.StartsWith("tag:", StringComparison.Ordinal) && term.Length > 4)
        {
            return BestField(entry.Tags, term[4..], 100);
        }

        var best = 0;
        best = Math.Max(best, ScoreField(entry.Title, term, 100));
        best = Math.Max(best, BestField(entry.Aliases, term, 90));
        best = Math.Max(best, ScoreField(entry.FileName, term, 85));
        best = Math.Max(best, ScoreField(entry.Domain, term, 80));
        best = Math.Max(best, BestField(entry.Tags, term, 70));
        best = Math.Max(best, ScoreField(entry.Target, term, 60));
        return best;
    }

    private static int BestField(string[] fields, string term, int weight)
    {
        var best = 0;
        foreach (var field in fields)
        {
            best = Math.Max(best, ScoreField(field, term, weight));
            if (best >= 1000)
            {
                break;
            }
        }

        return best;
    }

    private static int ScoreField(string field, string term, int weight)
    {
        var raw = ScoreFieldRaw(field, term);
        return raw == 0 ? 0 : raw * weight / 100;
    }

    private static int ScoreFieldRaw(string field, string term)
    {
        if (field.Length == 0 || term.Length == 0)
        {
            return 0;
        }

        if (field.Equals(term, StringComparison.Ordinal))
        {
            return 1000;
        }

        if (field.StartsWith(term, StringComparison.Ordinal))
        {
            return 700;
        }

        var index = field.IndexOf(term, StringComparison.Ordinal);
        if (index >= 0)
        {
            return IsWordStart(field, index) ? 550 : 400;
        }

        return IsSubsequence(field, term) ? 200 : 0;
    }

    private static bool IsWordStart(string field, int index)
    {
        return index == 0 || !char.IsLetterOrDigit(field[index - 1]);
    }

    private static bool IsSubsequence(string field, string term)
    {
        var position = 0;
        foreach (var ch in field)
        {
            if (position < term.Length && ch == term[position])
            {
                position++;
            }

            if (position == term.Length)
            {
                return true;
            }
        }

        return position == term.Length;
    }

    private static double Frecency(LinkItem item)
    {
        if (item.LastOpenedAt is null)
        {
            return item.OpenCount * 0.1;
        }

        var hours = (DateTimeOffset.UtcNow - item.LastOpenedAt.Value).TotalHours;
        var bonus = hours switch
        {
            < 1 => 30.0,
            < 24 => 20.0,
            < 168 => 10.0,
            < 720 => 4.0,
            _ => 1.0,
        };

        return (item.OpenCount * 0.5) + bonus;
    }

    private static string DomainOf(LinkItem item)
    {
        if (item.Kind != LinkKinds.Url)
        {
            return string.Empty;
        }

        try
        {
            return new Uri(item.Target).Host.ToLowerInvariant();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string FileNameOf(LinkItem item)
    {
        if (item.Kind == LinkKinds.Url)
        {
            return string.Empty;
        }

        return Path.GetFileName(item.Target.TrimEnd('\\', '/')).ToLowerInvariant();
    }

    private static string ToLower(string? value) => (value ?? string.Empty).ToLowerInvariant();

    private static string[] ToLowerArray(string[]? values) =>
        values is null ? Array.Empty<string>() : values.Select(value => ToLower(value)).ToArray();
}
