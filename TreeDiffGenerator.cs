using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GenealogyDiffUtility
{
    /// <summary>Difference counts for one category (Individuals, Families...).</summary>
    internal sealed class DiffCategoryCounts
    {
        public string Name { get; init; } = string.Empty;
        public int Matches { get; set; }
        public int Missing { get; set; }
        public int Extra { get; set; }

        /// <summary>Records found in both trees whose events or sources differ.</summary>
        public int Different { get; set; }

        /// <summary>Event and source differences within the <see cref="Different"/> records.</summary>
        public int DetailDifferences { get; set; }

        /// <summary>Records that are missing, extra, or present in both but different.</summary>
        public int RecordDifferences => Missing + Extra + Different;
    }

    /// <summary>Difference counts for a whole comparison.</summary>
    internal sealed class DiffSummary
    {
        public List<DiffCategoryCounts> Categories { get; } = new();

        /// <summary>True when events and sources were compared (Details on).</summary>
        public bool IncludesDetails { get; set; }

        public int RecordDifferences => Categories.Sum(c => c.RecordDifferences);
        public int DetailDifferences => Categories.Sum(c => c.DetailDifferences);

        public DiffCategoryCounts? Get(string name) => Categories.FirstOrDefault(c => c.Name == name);
    }

    /// <summary>
    /// Generates a plain-text comparison report between two parsed GEDCOM trees.
    /// Reports matches, records missing from the other tree, and records that
    /// exist only in the other tree, with roll-up counts per category and an
    /// overall score. When details are enabled, individual and family events
    /// and sources are also compared.
    /// <para>
    /// Matching is by stable key and counts copies: if one tree has a record
    /// (or event) twice and the other once, one pair matches and the second copy
    /// is reported, marked "(duplicate)".
    /// </para>
    /// </summary>
    internal static class TreeDiffGenerator
    {
        private sealed class Totals
        {
            public int Matches, Missing, Extra;
        }

        /// <summary>
        /// The result of pairing two lists by key, counting copies.
        /// </summary>
        internal sealed class MatchResult<T>
        {
            public List<(T This, T Other)> Matched { get; } = new();
            public List<T> Missing { get; } = new();
            public List<T> Extra { get; } = new();
            public HashSet<string> ThisKeys { get; } = new(StringComparer.Ordinal);
            public HashSet<string> OtherKeys { get; } = new(StringComparer.Ordinal);
        }

        /// <summary>
        /// Pairs items with equal keys, first with first, second with second, and
        /// so on. Unpaired copies go to Missing (this side) or Extra (other side).
        /// Never throws on duplicate keys.
        /// </summary>
        internal static MatchResult<T> Match<T>(IEnumerable<T> thisItems, IEnumerable<T> otherItems, Func<T, string> key)
        {
            var result = new MatchResult<T>();
            var others = otherItems.ToList();
            var otherQueues = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
            var paired = new bool[others.Count];

            for (int i = 0; i < others.Count; i++)
            {
                var k = key(others[i]);
                result.OtherKeys.Add(k);
                if (!otherQueues.TryGetValue(k, out var q))
                    otherQueues[k] = q = new Queue<int>();
                q.Enqueue(i);
            }

            foreach (var item in thisItems)
            {
                var k = key(item);
                result.ThisKeys.Add(k);
                if (otherQueues.TryGetValue(k, out var q) && q.Count > 0)
                {
                    int i = q.Dequeue();
                    paired[i] = true;
                    result.Matched.Add((item, others[i]));
                }
                else
                {
                    result.Missing.Add(item);
                }
            }

            // Unpaired copies on the other side, in that side's order
            for (int i = 0; i < others.Count; i++)
            {
                if (!paired[i]) result.Extra.Add(others[i]);
            }
            return result;
        }

        public static string Generate(GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, bool includeDetails) =>
            Generate(thisCtx, otherCtx, includeDetails, out _);

        /// <summary>
        /// Builds the report and also returns its difference counts, which the
        /// window shows in the top bar.
        /// </summary>
        public static string Generate(GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, bool includeDetails, out DiffSummary summary)
        {
            var sb = new StringBuilder();
            var totals = new Totals();
            summary = new DiffSummary { IncludesDetails = includeDetails };

            AppendSection(sb, "Individuals", totals, summary,
                thisCtx.Individuals.Values.OrderBy(p => p.DisplayName).ThenBy(p => p.Id),
                otherCtx.Individuals.Values.OrderBy(p => p.DisplayName).ThenBy(p => p.Id),
                p => TreeNodeKeys.GetKey(p)!, p => p.DisplayName,
                includeDetails
                    ? (a, b) => AppendRecordDetails(sb, a.DisplayName, a.Events, b.Events, AllSources(a.SourceIds, a.Events, thisCtx), AllSources(b.SourceIds, b.Events, otherCtx), totals)
                    : null,
                includeDetails ? p => AppendRecordDetailsMissing(sb, p.Events, AllSources(p.SourceIds, p.Events, thisCtx), totals) : null);

            AppendSection(sb, "Families", totals, summary,
                thisCtx.Families.Values.OrderBy(f => f.DisplayName).ThenBy(f => f.Id),
                otherCtx.Families.Values.OrderBy(f => f.DisplayName).ThenBy(f => f.Id),
                f => TreeNodeKeys.GetKey(f)!, f => f.DisplayName,
                includeDetails
                    ? (a, b) => AppendRecordDetails(sb, a.DisplayName, a.Events, b.Events, AllSources(a.SourceIds, a.Events, thisCtx), AllSources(b.SourceIds, b.Events, otherCtx), totals)
                    : null,
                includeDetails ? f => AppendRecordDetailsMissing(sb, f.Events, AllSources(f.SourceIds, f.Events, thisCtx), totals) : null);

            AppendSection(sb, "Sources", totals, summary,
                thisCtx.Sources.Values.OrderBy(s => s.Title).ThenBy(s => s.Id),
                otherCtx.Sources.Values.OrderBy(s => s.Title).ThenBy(s => s.Id),
                s => TreeNodeKeys.GetKey(s)!, s => s.Title, null, null);

            AppendSection(sb, "Repositories", totals, summary,
                thisCtx.Repositories.Values.OrderBy(r => r.Name).ThenBy(r => r.Id),
                otherCtx.Repositories.Values.OrderBy(r => r.Name).ThenBy(r => r.Id),
                r => TreeNodeKeys.GetKey(r)!, r => r.Name, null, null);

            sb.AppendLine();
            sb.AppendLine("=== Overall Score ===");
            int total = totals.Matches + totals.Missing + totals.Extra;
            if (total == 0)
            {
                sb.AppendLine("No data to compare.");
            }
            else
            {
                sb.AppendLine($"Total elements: {total}");
                sb.AppendLine($"Matches: {totals.Matches} ({100.0 * totals.Matches / total:F1}%)");
                sb.AppendLine($"Missing: {totals.Missing} ({100.0 * totals.Missing / total:F1}%)");
                sb.AppendLine($"Extra: {totals.Extra} ({100.0 * totals.Extra / total:F1}%)");
            }

            return BuildSummaryBlock(summary) + sb.ToString();
        }

        /// <summary>
        /// The "=== Summary ===" block at the top of the report: how many records
        /// differ in each category and, with Details on, how many event and
        /// source differences they contain.
        /// </summary>
        private static string BuildSummaryBlock(DiffSummary summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Summary ===");
            foreach (var c in summary.Categories)
            {
                var parts = new List<string> { $"{c.Missing} missing", $"{c.Extra} extra" };
                if (summary.IncludesDetails && (c.Name == "Individuals" || c.Name == "Families"))
                {
                    parts.Add($"{c.Different} with different details ({c.DetailDifferences} detail differences)");
                }
                sb.AppendLine($"{c.Name}: {c.RecordDifferences} differences - {string.Join(", ", parts)}");
            }

            sb.Append($"Total: {summary.RecordDifferences} record differences");
            sb.AppendLine(summary.IncludesDetails
                ? $", {summary.DetailDifferences} detail differences"
                : " (turn on Details to compare events and sources)");
            sb.AppendLine();
            return sb.ToString();
        }

        private static void AppendSection<T>(StringBuilder sb, string title, Totals totals, DiffSummary summary,
            IEnumerable<T> thisItems, IEnumerable<T> otherItems,
            Func<T, string> key, Func<T, string> label,
            Func<T, T, int>? matchedDetails, Action<T>? missingDetails)
            where T : class
        {
            var counts = new DiffCategoryCounts { Name = title };
            summary.Categories.Add(counts);

            if (title != "Individuals") sb.AppendLine();
            sb.AppendLine($"=== {title} ===");

            var match = Match(thisItems, otherItems, key);

            // Report in this tree's order: matched records (with their detail
            // differences) and missing records, interleaved as they appear.
            var matchedByThis = new Dictionary<object, T>(ReferenceEqualityComparer.Instance);
            foreach (var (thisItem, otherItem) in match.Matched)
                matchedByThis[thisItem] = otherItem;
            foreach (var item in thisItems)
            {
                if (matchedByThis.TryGetValue(item, out var other))
                {
                    int detailDiffs = matchedDetails?.Invoke(item, other) ?? 0;
                    if (detailDiffs > 0)
                    {
                        counts.Different++;
                        counts.DetailDifferences += detailDiffs;
                    }
                }
                else
                {
                    sb.AppendLine($"  [MISSING] {label(item)}{DuplicateNote(match.OtherKeys, key(item))}");
                    missingDetails?.Invoke(item);
                }
            }

            foreach (var item in match.Extra)
            {
                sb.AppendLine($"  [EXTRA] {label(item)}{DuplicateNote(match.ThisKeys, key(item))}");
            }

            counts.Matches = match.Matched.Count;
            counts.Missing = match.Missing.Count;
            counts.Extra = match.Extra.Count;

            string differentNote = matchedDetails != null
                ? $" | Different: {counts.Different} ({counts.DetailDifferences} detail differences)"
                : string.Empty;
            sb.AppendLine($"Matches: {match.Matched.Count} | Missing: {match.Missing.Count} | Extra: {match.Extra.Count}{differentNote}");
            totals.Matches += match.Matched.Count;
            totals.Missing += match.Missing.Count;
            totals.Extra += match.Extra.Count;
        }

        /// <summary>
        /// A record whose key exists on the other side too is an unpaired extra copy.
        /// </summary>
        private static string DuplicateNote(HashSet<string> otherSideKeys, string key) =>
            otherSideKeys.Contains(key) ? " (duplicate)" : string.Empty;

        /// <summary>
        /// Compares the events and sources of a record found in both trees. When
        /// anything differs, writes a "[DIFFERENT] name" heading so the detail
        /// lines beneath it say which individual or family they belong to.
        /// Returns the number of detail differences found.
        /// </summary>
        private static int AppendRecordDetails(StringBuilder sb, string label,
            IEnumerable<GedcomEvent> thisEvents, IEnumerable<GedcomEvent> otherEvents,
            IEnumerable<SourceNode> thisSources, IEnumerable<SourceNode> otherSources, Totals totals)
        {
            var lines = new List<string>();

            var events = Match(thisEvents.Where(e => !e.IsInternal), otherEvents.Where(e => !e.IsInternal), EventKey);
            foreach (var evt in events.Missing)
                lines.Add($"      [MISSING] Event: {evt.DisplayName}{DuplicateNote(events.OtherKeys, EventKey(evt))}");
            foreach (var evt in events.Extra)
                lines.Add($"      [EXTRA] Event: {evt.DisplayName}{DuplicateNote(events.ThisKeys, EventKey(evt))}");

            var sources = Match(thisSources, otherSources, s => TreeNodeKeys.GetKey(s)!);
            foreach (var src in sources.Missing)
                lines.Add($"      [MISSING] Source: {src.Title}{DuplicateNote(sources.OtherKeys, TreeNodeKeys.GetKey(src)!)}");
            foreach (var src in sources.Extra)
                lines.Add($"      [EXTRA] Source: {src.Title}{DuplicateNote(sources.ThisKeys, TreeNodeKeys.GetKey(src)!)}");

            if (lines.Count > 0)
            {
                sb.AppendLine($"  [DIFFERENT] {label}");
                foreach (var line in lines)
                    sb.AppendLine(line);
            }

            totals.Matches += events.Matched.Count + sources.Matched.Count;
            totals.Missing += events.Missing.Count + sources.Missing.Count;
            totals.Extra += events.Extra.Count + sources.Extra.Count;

            return lines.Count;
        }

        private static void AppendRecordDetailsMissing(StringBuilder sb, IEnumerable<GedcomEvent> events,
            IEnumerable<SourceNode> sources, Totals totals)
        {
            foreach (var evt in events.Where(e => !e.IsInternal))
            {
                totals.Missing++;
                sb.AppendLine($"      [MISSING] Event: {evt.DisplayName}");
            }
            foreach (var src in sources)
            {
                totals.Missing++;
                sb.AppendLine($"      [MISSING] Source: {src.Title}");
            }
        }

        /// <summary>
        /// The distinct sources a record cites, directly or through its events.
        /// </summary>
        private static List<SourceNode> AllSources(IEnumerable<string> directIds, IEnumerable<GedcomEvent> events, GedcomTreeContext ctx)
        {
            var ids = new HashSet<string>(directIds);
            foreach (var evt in events)
            {
                ids.UnionWith(evt.SourceIds);
            }

            var result = new List<SourceNode>();
            foreach (var id in ids)
            {
                if (ctx.Sources.TryGetValue(id, out var src))
                    result.Add(src);
            }
            return result;
        }

        private static string EventKey(GedcomEvent e) =>
            $"{e.Type}|{TreeNodeKeys.NormalizeName(e.SubType)}|{TreeNodeKeys.NormalizeName(e.Date)}|{TreeNodeKeys.NormalizeName(e.Place)}|{TreeNodeKeys.NormalizeName(e.Data)}";
    }
}
