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

        /// <summary>
        /// Records in both trees whose identifying values changed (e.g. a corrected
        /// birth date), so their keys differ; reported once as [CHANGED].
        /// </summary>
        public int Changed { get; set; }

        /// <summary>Records found in both trees whose events or sources differ.</summary>
        public int Different { get; set; }

        /// <summary>Event and source differences within the <see cref="Different"/> and <see cref="Changed"/> records.</summary>
        public int DetailDifferences { get; set; }

        /// <summary>Records that are missing, extra, changed, or present in both but different.</summary>
        public int RecordDifferences => Missing + Extra + Changed + Different;
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

        // Every tag is padded to the width of the longest one ("[DIFFERENT]") so the
        // text after the tags lines up from one line to the next.
        private const int TagWidth = 11;
        private const string RecordIndent = "  ";
        private const string DetailIndent = "      ";

        private static string Tag(string tag) => tag.PadRight(TagWidth) + " ";

        // Where a [CHANGED] line's left/right values start: under the text after the tag.

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
        /// Like <see cref="Match{T}"/>, but when a key has several copies on a side,
        /// pairs the copies so the fewest differences remain (see
        /// <see cref="PairCopies{T}"/>) instead of first-with-first. The ▲/▼
        /// navigation pairs copies the same way.
        /// </summary>
        internal static MatchResult<T> MatchBest<T>(IEnumerable<T> thisItems, IEnumerable<T> otherItems,
            Func<T, string> key, Func<T, T, int> distance)
        {
            var thisList = thisItems.ToList();
            var otherList = otherItems.ToList();
            var result = new MatchResult<T>();

            var otherGroups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int j = 0; j < otherList.Count; j++)
            {
                var k = key(otherList[j]);
                result.OtherKeys.Add(k);
                if (!otherGroups.TryGetValue(k, out var g)) otherGroups[k] = g = new List<int>();
                g.Add(j);
            }
            var thisGroups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < thisList.Count; i++)
            {
                var k = key(thisList[i]);
                result.ThisKeys.Add(k);
                if (!thisGroups.TryGetValue(k, out var g)) thisGroups[k] = g = new List<int>();
                g.Add(i);
            }

            var pairedWith = new int[thisList.Count];
            Array.Fill(pairedWith, -1);
            var otherPaired = new bool[otherList.Count];
            foreach (var (k, ti) in thisGroups)
            {
                if (!otherGroups.TryGetValue(k, out var oi)) continue;
                foreach (var (a, b) in PairCopies(ti.Select(i => thisList[i]).ToList(), oi.Select(j => otherList[j]).ToList(), distance))
                {
                    pairedWith[ti[a]] = oi[b];
                    otherPaired[oi[b]] = true;
                }
            }

            for (int i = 0; i < thisList.Count; i++)
            {
                if (pairedWith[i] >= 0) result.Matched.Add((thisList[i], otherList[pairedWith[i]]));
                else result.Missing.Add(thisList[i]);
            }
            for (int j = 0; j < otherList.Count; j++)
            {
                if (!otherPaired[j]) result.Extra.Add(otherList[j]);
            }
            return result;
        }

        /// <summary>
        /// Pairs copies of one record (same key) on each side: one-to-one copies
        /// pair directly; otherwise the pair with the fewest differences is taken
        /// first, then the next, and so on (ties: earliest first). Returns index
        /// pairs; copies left over are unpaired.
        /// </summary>
        internal static List<(int This, int Other)> PairCopies<T>(List<T> thisCopies, List<T> otherCopies, Func<T, T, int> distance)
        {
            var pairs = new List<(int, int)>();
            if (thisCopies.Count == 0 || otherCopies.Count == 0) return pairs;
            if (thisCopies.Count == 1 && otherCopies.Count == 1)
            {
                pairs.Add((0, 0));
                return pairs;
            }

            var candidates = new List<(int D, int I, int J)>();
            for (int i = 0; i < thisCopies.Count; i++)
                for (int j = 0; j < otherCopies.Count; j++)
                    candidates.Add((distance(thisCopies[i], otherCopies[j]), i, j));

            var usedThis = new bool[thisCopies.Count];
            var usedOther = new bool[otherCopies.Count];
            foreach (var (_, i, j) in candidates.OrderBy(c => c.D).ThenBy(c => c.I).ThenBy(c => c.J))
            {
                if (usedThis[i] || usedOther[j]) continue;
                usedThis[i] = usedOther[j] = true;
                pairs.Add((i, j));
            }
            return pairs;
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
            var totals = new DiffTotals();
            var comparer = new RecordComparer(thisCtx, otherCtx, includeDetails, sides, totals);

            // For pairing copies of a record: how many differences a pairing leaves
            // (a separate comparer, so trying pairings doesn't touch the totals).
            var trial = new RecordComparer(thisCtx, otherCtx, includeDetails, sides);
            Func<object, object, int> distance = (a, b) => trial.Compare(a, b).Count;
            summary = new DiffSummary { IncludesDetails = includeDetails };

            AppendSection(sb, "Individuals", totals, summary,
                thisCtx.Individuals.Values.OrderBy(p => p.DisplayName).ThenBy(p => p.Id),
                otherCtx.Individuals.Values.OrderBy(p => p.DisplayName).ThenBy(p => p.Id),
                p => TreeNodeKeys.GetKey(p)!, p => p.DisplayName,
                comparer.ComparePeople,
                includeDetails ? p => AppendRecordDetailsMissing(sb, p.Events, AllSources(p.SourceIds, p.Events, thisCtx), totals) : null,
                p => RecordPairKey(p), distance);

            AppendSection(sb, "Families", totals, summary,
                thisCtx.Families.Values.OrderBy(f => f.DisplayName).ThenBy(f => f.Id),
                otherCtx.Families.Values.OrderBy(f => f.DisplayName).ThenBy(f => f.Id),
                f => TreeNodeKeys.GetKey(f)!, f => f.DisplayName,
                comparer.CompareFamilies,
                includeDetails ? f => AppendRecordDetailsMissing(sb, f.Events, AllSources(f.SourceIds, f.Events, thisCtx), totals) : null,
                f => RecordPairKey(f), distance);

            AppendSection(sb, "Sources", totals, summary,
                thisCtx.Sources.Values.OrderBy(s => s.Title).ThenBy(s => s.Id),
                otherCtx.Sources.Values.OrderBy(s => s.Title).ThenBy(s => s.Id),
                s => TreeNodeKeys.GetKey(s)!, s => s.Title, comparer.CompareSources, null, null, distance);

            AppendSection(sb, "Repositories", totals, summary,
                thisCtx.Repositories.Values.OrderBy(r => r.Name).ThenBy(r => r.Id),
                otherCtx.Repositories.Values.OrderBy(r => r.Name).ThenBy(r => r.Id),
                r => TreeNodeKeys.GetKey(r)!, r => r.Name, comparer.CompareRepositories, null, null, distance);

            // Shared note records, matched by their text.
            AppendSection(sb, "Notes", totals, summary,
                thisCtx.Notes.Values.OrderBy(n => n.Text).ThenBy(n => n.Id),
                otherCtx.Notes.Values.OrderBy(n => n.Text).ThenBy(n => n.Id),
                n => TreeNodeKeys.GetKey(n)!, n => NoteLabel(n.Text), (_, _) => new RecordDiff(), null);

            sb.AppendLine();
            sb.AppendLine("=== Overall Score ===");
            int total = totals.Matches + totals.Missing + totals.Extra + totals.Changed;
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
            sb.Append($"Key: [MISSING] = only in {thisLabel}; [EXTRA] = only in {otherLabel}; [CHANGED] = in both, but some values differ (both shown); [DIFFERENT] = in both, with differences listed beneath it");
            sb.AppendLine(".");
            if (includeDetails)
                sb.AppendLine("Not counted as differences: GEDCOM record numbers, case and spacing, a \"Y\" event flag vs. no value, and generated descriptions like \"Birth of <name>\".");
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
                bool hasPeopleOrFamilies = c.Name == "Individuals" || c.Name == "Families";
                if (hasPeopleOrFamilies)
                {
                    parts.Add($"{c.Changed} changed");
                }
                parts.Add(hasPeopleOrFamilies
                    ? $"{c.Different} with differences ({c.DetailDifferences} differences in their details)"
                    : $"{c.Different} with differences");
                sb.AppendLine($"{c.Name}: {c.RecordDifferences} differences - {string.Join(", ", parts)}");
            }

            sb.Append($"Total: {summary.RecordDifferences} record differences");
            sb.AppendLine(summary.IncludesDetails
                ? $", {summary.DetailDifferences} detail differences"
                : " (turn on Details to compare events and sources)");
            sb.AppendLine();
            return sb.ToString();
        }

        private static void AppendSection<T>(StringBuilder sb, string title,
            DiffTotals totals, DiffSummary summary,
            IEnumerable<T> thisItems, IEnumerable<T> otherItems,
            Func<T, string> key, Func<T, string> label,
            Func<T, T, RecordDiff> compare, Action<T>? missingDetails,
            Func<T, string>? pairKey = null, Func<object, object, int>? distance = null)
            where T : class
        {
            var counts = new DiffCategoryCounts { Name = title };
            summary.Categories.Add(counts);

            if (title != "Individuals") sb.AppendLine();
            sb.AppendLine($"=== {title} ===");

            var match = distance == null
                ? Match(thisItems, otherItems, key)
                : MatchBest(thisItems, otherItems, key, (a, b) => distance(a, b));

            // A record missing on one side and extra on the other under the same
            // name is usually one record whose identifying values were edited
            // (e.g. a corrected birth date): report it once, as [CHANGED].
            var changedPairs = pairKey == null
                ? new List<(T This, T Other)>()
                : PairChangedRecords(
                    match.Missing.Where(m => !match.OtherKeys.Contains(key(m))).ToList(),
                    match.Extra.Where(x => !match.ThisKeys.Contains(key(x))).ToList(),
                    pairKey);
            var changedByThis = new Dictionary<object, T>(ReferenceEqualityComparer.Instance);
            var changedOthers = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var (a, b) in changedPairs)
            {
                changedByThis[a] = b;
                changedOthers.Add(b);
            }

            // Report in this tree's order: matched records (with their detail
            // differences), changed records and missing records, interleaved.
            var matchedByThis = new Dictionary<object, T>(ReferenceEqualityComparer.Instance);
            foreach (var (thisItem, otherItem) in match.Matched)
                matchedByThis[thisItem] = otherItem;
            foreach (var item in thisItems)
            {
                if (matchedByThis.TryGetValue(item, out var other))
                {
                    var diff = compare(item, other);
                    if (diff.Count > 0)
                    {
                        counts.Different++;
                        counts.DetailDifferences += diff.Count;
                        sb.AppendLine($"{RecordIndent}{Tag("[DIFFERENT]")}{label(item)}");
                        foreach (var line in diff.Lines) sb.AppendLine(line);
                    }
                }
                else if (changedByThis.TryGetValue(item, out var changedTo))
                {
                    var diff = compare(item, changedTo);
                    counts.Changed++;
                    counts.DetailDifferences += diff.Count;
                    sb.AppendLine($"{RecordIndent}{Tag("[CHANGED]")}{label(item)}");
                    foreach (var line in diff.Lines) sb.AppendLine(line);
                }
                else
                {
                    sb.AppendLine($"{RecordIndent}{Tag("[MISSING]")}{label(item)}{DuplicateNote(match.OtherKeys, key(item))}");
                    missingDetails?.Invoke(item);
                }
            }

            foreach (var item in match.Extra)
            {
                if (changedOthers.Contains(item)) continue;
                sb.AppendLine($"{RecordIndent}{Tag("[EXTRA]")}{label(item)}{DuplicateNote(match.ThisKeys, key(item))}");
            }

            counts.Matches = match.Matched.Count;
            counts.Missing = match.Missing.Count - changedPairs.Count;
            counts.Extra = match.Extra.Count - changedPairs.Count;

            string changedNote = pairKey != null ? $" | Changed: {counts.Changed}" : string.Empty;
            string differentNote = $" | Different: {counts.Different} ({counts.DetailDifferences} detail differences)";
            sb.AppendLine($"Matches: {counts.Matches} | Missing: {counts.Missing} | Extra: {counts.Extra}{changedNote}{differentNote}");
            totals.Matches += counts.Matches;
            totals.Missing += counts.Missing;
            totals.Extra += counts.Extra;
            totals.Changed += counts.Changed;
        }

        /// <summary>
        /// Pairs records left unmatched on each side that share a pair key (a
        /// person's name; a family's spouse names). Only one-to-one pairings are
        /// made: if two unmatched people on one side share a name, there is no
        /// telling which is which, so they stay [MISSING]/[EXTRA].
        /// </summary>
        internal static List<(T This, T Other)> PairChangedRecords<T>(List<T> thisOnly, List<T> otherOnly, Func<T, string> pairKey)
            where T : class
        {
            var otherByKey = otherOnly
                .GroupBy(pairKey)
                .Where(g => g.Key.Length > 0)
                .ToDictionary(g => g.Key, g => g.ToList());

            return thisOnly
                .GroupBy(pairKey)
                .Where(g => g.Key.Length > 0 && g.Count() == 1 &&
                            otherByKey.TryGetValue(g.Key, out var others) && others.Count == 1)
                .Select(g => (g.Single(), otherByKey[g.Key].Single()))
                .ToList();
        }

        /// <summary>
        /// What an unmatched person or family is paired on to be reported as
        /// [CHANGED]: a person's name, or a family's spouse names. Empty for
        /// anything else. Also used by the ▲/▼ navigation so it stops once per
        /// changed record.
        /// </summary>
        internal static string RecordPairKey(object record)
        {
            switch (record)
            {
                case IndividualNode p:
                    string name = TreeNodeKeys.NormalizeName(p.FullName);
                    return name.Length == 0 ? string.Empty : "I|" + name;
                case FamilyNode f:
                    string husband = TreeNodeKeys.NormalizeName(f.Husband?.FullName ?? string.Empty);
                    string wife = TreeNodeKeys.NormalizeName(f.Wife?.FullName ?? string.Empty);
                    return husband.Length == 0 && wife.Length == 0 ? string.Empty : $"F|{husband}|{wife}";
                default:
                    return string.Empty;
            }
        }


        private const string MoreSpecificMarker = "◀ more specific";

        /// <summary>
        /// Two lines (this side, other side) for each field whose normalized
        /// values differ, with the field names and side names padded so the
        /// values line up. On dates and places, the side with the more specific
        /// value (25 JAN 1877 vs 1877; "Belsito, Cosenza, Italy" vs "Italy") is
        /// marked, when one clearly is.
        /// </summary>
        internal static IEnumerable<string> DescribeFields((string This, string Other) sides, string indent,
            params (string Name, string This, string Other)[] fields)
        {
            var differing = fields.Where(f => N(f.This) != N(f.Other)).ToList();
            if (differing.Count == 0) yield break;

            int nameWidth = differing.Max(f => f.Name.Length) + 2;
            int sideWidth = Math.Max(sides.This.Length, sides.Other.Length) + 2;
            string thisWord = (sides.This + ":").PadRight(sideWidth);
            string otherWord = (sides.Other + ":").PadRight(sideWidth);

            foreach (var f in differing)
            {
                int moreSpecific = f.Name.EndsWith("date", StringComparison.OrdinalIgnoreCase) ? DateSpecificity.Compare(f.This, f.Other)
                                 : f.Name.EndsWith("place", StringComparison.OrdinalIgnoreCase) ? PlaceSpecificity(f.This, f.Other)
                                 : 0;
                string thisValue = Shown(f.This), otherValue = Shown(f.Other);
                int valueWidth = Math.Max(thisValue.Length, otherValue.Length) + 2;
                string thisLine = moreSpecific > 0 ? thisValue.PadRight(valueWidth) + MoreSpecificMarker : thisValue;
                string otherLine = moreSpecific < 0 ? otherValue.PadRight(valueWidth) + MoreSpecificMarker : otherValue;

                yield return $"{indent}{(f.Name + ":").PadRight(nameWidth)}{thisWord}{thisLine}";
                yield return $"{indent}{new string(' ', nameWidth)}{otherWord}{otherLine}";
            }
        }

        /// <summary>
        /// +1 if place <paramref name="a"/> is more specific than <paramref name="b"/>,
        /// -1 if less, 0 if neither clearly is. One place is more specific when it
        /// contains the other's parts and adds more ("Belsito, Cosenza, Calabria,
        /// Italy" vs "Italy"; "Philadelphia, Pennsylvania" vs "Philadelphia"), or
        /// when the other is blank.
        /// </summary>
        internal static int PlaceSpecificity(string a, string b)
        {
            static List<string> Parts(string place) =>
                place.Split(',').Select(N).Where(p => p.Length > 0).ToList();

            var pa = Parts(a);
            var pb = Parts(b);
            if (pa.Count == pb.Count) return 0;
            if (pb.Count == 0) return 1;
            if (pa.Count == 0) return -1;

            var (longer, shorter, sign) = pa.Count > pb.Count ? (pa, pb, 1) : (pb, pa, -1);
            bool isPrefix = longer.Take(shorter.Count).SequenceEqual(shorter);
            bool isSuffix = longer.Skip(longer.Count - shorter.Count).SequenceEqual(shorter);
            return isPrefix || isSuffix ? sign : 0;
        }

        /// <summary>
        /// A record whose key exists on the other side too is an unpaired extra copy.
        /// </summary>
        private static string DuplicateNote(HashSet<string> otherSideKeys, string key) =>
            otherSideKeys.Contains(key) ? " (duplicate)" : string.Empty;

        private static string NoteLabel(string text)
        {
            string line = string.Join(" ", (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return "\"" + (line.Length <= 70 ? line : line.Substring(0, 69) + "…") + "\"";
        }

        internal static string Shown(string value) =>
            string.IsNullOrWhiteSpace(value) ? "(blank)" : value.Trim();

        private static void AppendRecordDetailsMissing(StringBuilder sb, IEnumerable<GedcomEvent> events,
            IEnumerable<SourceNode> sources, DiffTotals totals)
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
            string v = N(value);
            if (v.StartsWith(N(name) + " of ", StringComparison.Ordinal)) return true;

            // Some programs word the event differently in the description than
            // GEDCOM does (Gramps: "Adopted of Smith, John" on an ADOP).
            return AlternateEventNames.TryGetValue(e.Type, out var names) &&
                   names.Any(n => v.StartsWith(n + " of ", StringComparison.Ordinal));
        }

        private static readonly Dictionary<string, string[]> AlternateEventNames = new(StringComparer.Ordinal)
        {
            ["ADOP"] = new[] { "adopted" },
            ["BAPM"] = new[] { "baptism" },
            ["CHR"] = new[] { "christening" },
            ["EVEN"] = Array.Empty<string>(),
        };

        private static string N(string value) => TreeNodeKeys.NormalizeName(value);
    }
    /// <summary>
    /// Decides which of two GEDCOM dates is more specific, when one clearly is:
    /// a day beats a month beats a year ("25 JAN 1877" vs "1877"), an exact date
    /// beats an approximate one ("1885" vs "ABT 1885"), and a single date beats a
    /// range or a before/after it falls within ("1877" vs "BET 1870 AND 1880").
    /// Dates that disagree ("1877" vs "1878") or can't be read get no verdict.
    /// </summary>
    internal static class DateSpecificity
    {
        private enum Kind { Exact, Approximate, Before, After, Range }

        private sealed record Parsed(Kind Kind, int Year, int Month, int Day, int EndYear)
        {
            // 1 = year only, 2 = month and year, 3 = full date
            public int Precision => Day > 0 ? 3 : Month > 0 ? 2 : 1;
        }

        private static readonly string[] Months =
            { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

        /// <summary>+1 if <paramref name="a"/> is more specific, -1 if <paramref name="b"/> is, else 0.</summary>
        public static int Compare(string a, string b)
        {
            bool blankA = string.IsNullOrWhiteSpace(a), blankB = string.IsNullOrWhiteSpace(b);
            if (blankA != blankB) return blankA ? -1 : 1;

            var pa = Parse(a);
            var pb = Parse(b);
            if (pa == null || pb == null) return 0;

            bool singleA = pa.Kind is Kind.Exact or Kind.Approximate;
            bool singleB = pb.Kind is Kind.Exact or Kind.Approximate;

            if (singleA && singleB)
            {
                if (!Compatible(pa, pb)) return 0;
                int scoreA = pa.Precision * 2 + (pa.Kind == Kind.Exact ? 1 : 0);
                int scoreB = pb.Precision * 2 + (pb.Kind == Kind.Exact ? 1 : 0);
                return Math.Sign(scoreA - scoreB);
            }
            if (singleA && WithinBounds(pa.Year, pb)) return 1;
            if (singleB && WithinBounds(pb.Year, pa)) return -1;
            return 0;
        }

        private static bool Compatible(Parsed a, Parsed b) =>
            a.Year == b.Year &&
            (a.Month == 0 || b.Month == 0 || a.Month == b.Month) &&
            (a.Day == 0 || b.Day == 0 || a.Day == b.Day);

        private static bool WithinBounds(int year, Parsed bound) => bound.Kind switch
        {
            Kind.Range => year >= bound.Year && year <= bound.EndYear,
            Kind.Before => year <= bound.Year,
            Kind.After => year >= bound.Year,
            _ => false
        };

        private static Parsed? Parse(string text)
        {
            var tokens = text.ToUpperInvariant()
                .Replace(",", " ")
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            if (tokens.Count == 0) return null;

            Kind kind = Kind.Exact;
            switch (tokens[0].TrimEnd('.'))
            {
                case "ABT": case "ABOUT": case "EST": case "CAL": case "CIRCA": case "C": case "CA":
                    kind = Kind.Approximate; tokens.RemoveAt(0); break;
                case "BEF": case "BEFORE":
                    kind = Kind.Before; tokens.RemoveAt(0); break;
                case "AFT": case "AFTER":
                    kind = Kind.After; tokens.RemoveAt(0); break;
                case "BET": case "FROM":
                    kind = Kind.Range; tokens.RemoveAt(0); break;
            }

            if (kind == Kind.Range)
            {
                int split = tokens.FindIndex(t => t == "AND" || t == "TO");
                if (split < 0) return null;
                var start = ParseSimple(tokens.Take(split).ToList());
                var end = ParseSimple(tokens.Skip(split + 1).ToList());
                if (start == null || end == null) return null;
                return new Parsed(Kind.Range, start.Value.Year, 0, 0, end.Value.Year);
            }

            var date = ParseSimple(tokens);
            return date == null ? null : new Parsed(kind, date.Value.Year, date.Value.Month, date.Value.Day, 0);
        }

        /// <summary>"25 JAN 1877", "JAN 1877", "1877" or "1877-01-25".</summary>
        private static (int Year, int Month, int Day)? ParseSimple(List<string> tokens)
        {
            if (tokens.Count == 1 && tokens[0].Length == 10 && tokens[0][4] == '-' &&
                DateTime.TryParseExact(tokens[0], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var iso))
            {
                return (iso.Year, iso.Month, iso.Day);
            }

            int year = 0, month = 0, day = 0;
            switch (tokens.Count)
            {
                case 1:
                    if (!int.TryParse(tokens[0], out year)) return null;
                    break;
                case 2:
                    month = MonthNumber(tokens[0]);
                    if (month == 0 || !int.TryParse(tokens[1], out year)) return null;
                    break;
                case 3:
                    month = MonthNumber(tokens[1]);
                    if (!int.TryParse(tokens[0], out day) || month == 0 || !int.TryParse(tokens[2], out year)) return null;
                    break;
                default:
                    return null;
            }
            if (year < 1 || year > 9999 || day < 0 || day > 31) return null;
            return (year, month, day);
        }

        private static int MonthNumber(string token)
        {
            if (token.Length < 3) return 0;
            int index = Array.IndexOf(Months, token.Substring(0, 3));
            return index < 0 ? 0 : index + 1;
        }
    }
}
