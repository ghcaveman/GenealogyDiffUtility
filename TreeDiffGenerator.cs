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
            public int Matches, Missing, Extra, Changed;
        }

        // Every tag is padded to the width of the longest one ("[DIFFERENT]") so the
        // text after the tags lines up from one line to the next.
        private const int TagWidth = 11;
        private const string RecordIndent = "  ";
        private const string DetailIndent = "      ";

        private static string Tag(string tag) => tag.PadRight(TagWidth) + " ";

        // Where a [CHANGED] line's left/right values start: under the text after the tag.
        private static readonly string ChangeIndent = new(' ', DetailIndent.Length + TagWidth + 1);

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
        /// window shows in the top bar. <paramref name="thisSide"/> and
        /// <paramref name="otherSide"/> ("left", "right") and the file names are
        /// used in the key at the top and on [CHANGED] lines.
        /// </summary>
        public static string Generate(GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, bool includeDetails,
            out DiffSummary summary, string thisSide = "left", string otherSide = "right",
            string? thisFileName = null, string? otherFileName = null)
        {
            var sides = (This: thisSide, Other: otherSide);
            var sb = new StringBuilder();
            var totals = new Totals();
            summary = new DiffSummary { IncludesDetails = includeDetails };

            AppendSection(sb, "Individuals", totals, summary,
                thisCtx.Individuals.Values.OrderBy(p => p.DisplayName).ThenBy(p => p.Id),
                otherCtx.Individuals.Values.OrderBy(p => p.DisplayName).ThenBy(p => p.Id),
                p => TreeNodeKeys.GetKey(p)!, p => p.DisplayName,
                includeDetails
                    ? (a, b) => AppendRecordDetails(sb, sides, a.DisplayName, a.Events, b.Events, AllSources(a.SourceIds, a.Events, thisCtx), AllSources(b.SourceIds, b.Events, otherCtx), totals)
                    : null,
                includeDetails ? p => AppendRecordDetailsMissing(sb, p.Events, AllSources(p.SourceIds, p.Events, thisCtx), totals) : null);

            AppendSection(sb, "Families", totals, summary,
                thisCtx.Families.Values.OrderBy(f => f.DisplayName).ThenBy(f => f.Id),
                otherCtx.Families.Values.OrderBy(f => f.DisplayName).ThenBy(f => f.Id),
                f => TreeNodeKeys.GetKey(f)!, f => f.DisplayName,
                includeDetails
                    ? (a, b) => AppendRecordDetails(sb, sides, a.DisplayName, a.Events, b.Events, AllSources(a.SourceIds, a.Events, thisCtx), AllSources(b.SourceIds, b.Events, otherCtx), totals)
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
                if (includeDetails)
                    sb.AppendLine($"Changed: {totals.Changed} ({100.0 * totals.Changed / total:F1}%)");
            }

            return BuildSummaryBlock(summary) +
                   BuildKey(includeDetails, FileLabel(thisSide, thisFileName), FileLabel(otherSide, otherFileName)) +
                   sb.ToString();
        }

        /// <summary>
        /// Explains the tags in terms of the two files, and what is not counted
        /// as a difference.
        /// </summary>
        private static string FileLabel(string side, string? fileName) =>
            string.IsNullOrWhiteSpace(fileName) ? $"the {side} file" : $"the {side} file ({fileName})";

        private static string BuildKey(bool includeDetails, string thisLabel, string otherLabel)
        {
            var sb = new StringBuilder();
            sb.Append($"Key: [MISSING] = only in {thisLabel}; [EXTRA] = only in {otherLabel}");
            if (includeDetails)
                sb.Append("; [CHANGED] = in both, but some values differ (both shown); [DIFFERENT] = a person or family in both whose events or sources differ");
            sb.AppendLine(".");
            if (includeDetails)
                sb.AppendLine("Not counted as differences: case and spacing, a \"Y\" event flag vs. no value, and generated descriptions like \"Birth of <name>\".");
            sb.AppendLine();
            return sb.ToString();
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
                    sb.AppendLine($"{RecordIndent}{Tag("[MISSING]")}{label(item)}{DuplicateNote(match.OtherKeys, key(item))}");
                    missingDetails?.Invoke(item);
                }
            }

            foreach (var item in match.Extra)
            {
                sb.AppendLine($"{RecordIndent}{Tag("[EXTRA]")}{label(item)}{DuplicateNote(match.ThisKeys, key(item))}");
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
        private static int AppendRecordDetails(StringBuilder sb, (string This, string Other) sides, string label,
            IEnumerable<GedcomEvent> thisEvents, IEnumerable<GedcomEvent> otherEvents,
            IEnumerable<SourceNode> thisSources, IEnumerable<SourceNode> otherSources, Totals totals)
        {
            var lines = new List<string>();
            int differences = 0;

            var events = Match(thisEvents.Where(e => !e.IsInternal), otherEvents.Where(e => !e.IsInternal), EventKey);

            // An unmatched event on each side that is the same event with an edited
            // value (e.g. a refined date) is reported once, as [CHANGED], with only
            // the values that differ.
            var changed = PairChangedEvents(events.Missing, events.Extra);
            var changedThis = new HashSet<GedcomEvent>(changed.Select(c => c.This), ReferenceEqualityComparer.Instance);
            var changedOther = new HashSet<GedcomEvent>(changed.Select(c => c.Other), ReferenceEqualityComparer.Instance);

            foreach (var (a, b) in changed)
            {
                lines.Add($"{DetailIndent}{Tag("[CHANGED]")}Event: {a.DisplayName}");
                lines.AddRange(DescribeChange(a, b, sides));
                differences++;
            }
            foreach (var evt in events.Missing.Where(e => !changedThis.Contains(e)))
            {
                lines.Add($"{DetailIndent}{Tag("[MISSING]")}Event: {evt.DisplayName}{DuplicateNote(events.OtherKeys, EventKey(evt))}");
                differences++;
            }
            foreach (var evt in events.Extra.Where(e => !changedOther.Contains(e)))
            {
                lines.Add($"{DetailIndent}{Tag("[EXTRA]")}Event: {evt.DisplayName}{DuplicateNote(events.ThisKeys, EventKey(evt))}");
                differences++;
            }

            var sources = Match(thisSources, otherSources, s => TreeNodeKeys.GetKey(s)!);
            foreach (var src in sources.Missing)
            {
                lines.Add($"{DetailIndent}{Tag("[MISSING]")}Source: {src.Title}{DuplicateNote(sources.OtherKeys, TreeNodeKeys.GetKey(src)!)}");
                differences++;
            }
            foreach (var src in sources.Extra)
            {
                lines.Add($"{DetailIndent}{Tag("[EXTRA]")}Source: {src.Title}{DuplicateNote(sources.ThisKeys, TreeNodeKeys.GetKey(src)!)}");
                differences++;
            }

            if (lines.Count > 0)
            {
                sb.AppendLine($"{RecordIndent}{Tag("[DIFFERENT]")}{label}");
                foreach (var line in lines)
                    sb.AppendLine(line);
            }

            totals.Matches += events.Matched.Count + sources.Matched.Count;
            totals.Missing += events.Missing.Count - changed.Count + sources.Missing.Count;
            totals.Extra += events.Extra.Count - changed.Count + sources.Extra.Count;
            totals.Changed += changed.Count;

            return differences;
        }

        /// <summary>
        /// Pairs an unmatched event on this side with one on the other side when
        /// they look like the same event with an edited value: same kind of event
        /// and the same date or the same place. Pairs agreeing on both come first.
        /// </summary>
        private static List<(GedcomEvent This, GedcomEvent Other)> PairChangedEvents(
            List<GedcomEvent> thisOnly, List<GedcomEvent> otherOnly)
        {
            var pairs = new List<(GedcomEvent, GedcomEvent)>();
            var used = new bool[otherOnly.Count];
            var usedThis = new HashSet<GedcomEvent>(ReferenceEqualityComparer.Instance);

            // Pass 1: same date and place; pass 2: same date; pass 3: same place.
            for (int pass = 1; pass <= 3; pass++)
            {
                foreach (var a in thisOnly)
                {
                    if (usedThis.Contains(a)) continue;
                    for (int i = 0; i < otherOnly.Count; i++)
                    {
                        if (used[i]) continue;
                        var b = otherOnly[i];
                        if (!SameKindOfEvent(a, b)) continue;

                        bool sameDate = N(a.Date) == N(b.Date) && N(a.Date).Length > 0;
                        bool samePlace = N(a.Place) == N(b.Place) && N(a.Place).Length > 0;
                        bool ok = pass switch
                        {
                            1 => N(a.Date) == N(b.Date) && N(a.Place) == N(b.Place),
                            2 => sameDate,
                            _ => samePlace
                        };
                        if (!ok) continue;

                        used[i] = true;
                        usedThis.Add(a);
                        pairs.Add((a, b));
                        break;
                    }
                }
            }

            // Keep the order the events appear in this record.
            return pairs.OrderBy(p => thisOnly.IndexOf(p.Item1)).ToList();
        }

        private static bool SameKindOfEvent(GedcomEvent a, GedcomEvent b) =>
            a.Type == b.Type && (a.Type != "EVEN" || N(a.EventName) == N(b.EventName));

        /// <summary>
        /// One pair of left/right lines per value that differs between two events
        /// reported as [CHANGED].
        /// </summary>
        private static IEnumerable<string> DescribeChange(GedcomEvent a, GedcomEvent b, (string This, string Other) sides)
        {
            // "left:" / "right:" padded to the same width so the two values line up
            int sideWidth = Math.Max(sides.This.Length, sides.Other.Length) + 2;
            string thisWord = (sides.This + ":").PadRight(sideWidth);
            string otherWord = (sides.Other + ":").PadRight(sideWidth);

            var fields = new (string Name, string This, string Other, bool Differs)[]
            {
                ("Date", a.Date, b.Date, N(a.Date) != N(b.Date)),
                ("Place", a.Place, b.Place, N(a.Place) != N(b.Place)),
                ("Type", a.SubType, b.SubType, N(EffectiveSubType(a)) != N(EffectiveSubType(b))),
                ("Value", a.Data, b.Data, N(EffectiveData(a)) != N(EffectiveData(b))),
            };

            foreach (var f in fields.Where(f => f.Differs))
            {
                string name = (f.Name + ":").PadRight(7);
                yield return $"{ChangeIndent}{name}{thisWord}{Shown(f.This)}";
                yield return $"{ChangeIndent}{new string(' ', 7)}{otherWord}{Shown(f.Other)}";
            }
        }

        private static string Shown(string value) =>
            string.IsNullOrWhiteSpace(value) ? "(blank)" : value.Trim();

        private static void AppendRecordDetailsMissing(StringBuilder sb, IEnumerable<GedcomEvent> events,
            IEnumerable<SourceNode> sources, Totals totals)
        {
            foreach (var evt in events.Where(e => !e.IsInternal))
            {
                totals.Missing++;
                sb.AppendLine($"{DetailIndent}{Tag("[MISSING]")}Event: {evt.DisplayName}");
            }
            foreach (var src in sources)
            {
                totals.Missing++;
                sb.AppendLine($"{DetailIndent}{Tag("[MISSING]")}Source: {src.Title}");
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

        /// <summary>
        /// The identity of an event for matching. Values that mean the same thing
        /// across programs are compared as equal: see <see cref="EffectiveData"/>
        /// and <see cref="EffectiveSubType"/>.
        /// </summary>
        internal static string EventKey(GedcomEvent e) =>
            $"{e.Type}|{N(EffectiveSubType(e))}|{N(e.Date)}|{N(e.Place)}|{N(EffectiveData(e))}";

        /// <summary>
        /// The event line's value, ignoring values that only restate the event:
        /// "Y" (GEDCOM's "this event happened" flag, which some programs write
        /// and others leave off) and a generated description such as
        /// "Alt. Birth of Smith, John".
        /// </summary>
        internal static string EffectiveData(GedcomEvent e)
        {
            string data = e.Data.Trim();
            if (data.Equals("Y", StringComparison.OrdinalIgnoreCase)) return string.Empty;
            if (IsGeneratedDescription(data, e)) return string.Empty;
            return data;
        }

        /// <summary>
        /// The TYPE line, ignoring a generated description such as "Birth of
        /// Smith, John" on a standard event. On a generic EVEN the TYPE is the
        /// event's name and is always kept.
        /// </summary>
        internal static string EffectiveSubType(GedcomEvent e)
        {
            string subType = e.SubType.Trim();
            if (e.Type != "EVEN" && IsGeneratedDescription(subType, e)) return string.Empty;
            return subType;
        }

        /// <summary>
        /// True for text of the form "&lt;event name&gt; of &lt;anything&gt;", e.g.
        /// "Birth of Altimare, Carmine" on a birth, which some programs fill in
        /// automatically from the event type and the person's name.
        /// </summary>
        private static bool IsGeneratedDescription(string value, GedcomEvent e)
        {
            if (value.Length == 0) return false;
            string name = e.Type == "EVEN" ? e.SubType.Trim() : e.EventName;
            if (name.Length == 0) return false;
            return N(value).StartsWith(N(name) + " of ", StringComparison.Ordinal);
        }

        private static string N(string value) => TreeNodeKeys.NormalizeName(value);
    }
}
