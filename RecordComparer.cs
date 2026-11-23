using System;
using System.Collections.Generic;
using System.Linq;

namespace GenealogyDiffUtility
{
    /// <summary>Running counts behind the Diff report's "Overall Score".</summary>
    internal sealed class DiffTotals
    {
        public int Matches, Missing, Extra, Changed;
    }

    /// <summary>
    /// The differences found between two records: the report lines that
    /// describe them, and how many differences there are.
    /// </summary>
    internal sealed class RecordDiff
    {
        public List<string> Lines { get; } = new();
        public int Count { get; set; }
    }

    /// <summary>
    /// The one comparison of two matched records (people, families, sources,
    /// repositories), used by both the Diff report and the ▲/▼ mismatch
    /// navigation, so the two always agree on what differs.
    /// <para>
    /// Always compared: a person's sex, birth and death, and alternate names; a
    /// family's spouses, marriage and children; a source's repository and notes;
    /// a repository's notes. With Details on, also each record's events,
    /// citations, notes and media, and each event's and each citation's own
    /// notes and media.
    /// </para>
    /// <para>
    /// GEDCOM record Ids never take part: linked records are compared by content
    /// (a source by its title, author and publication; a note by its text; a
    /// media item by its file name).
    /// </para>
    /// </summary>
    internal sealed class RecordComparer
    {
        private readonly GedcomTreeContext _this;
        private readonly GedcomTreeContext _other;
        private readonly bool _details;
        private readonly (string This, string Other) _sides;
        private readonly DiffTotals _totals;

        public RecordComparer(GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, bool includeDetails,
            (string This, string Other) sides, DiffTotals? totals = null)
        {
            _this = thisCtx;
            _other = otherCtx;
            _details = includeDetails;
            _sides = sides;
            _totals = totals ?? new DiffTotals();
        }

        /// <summary>Compares two matched records of the same kind. Other node kinds never differ.</summary>
        public RecordDiff Compare(object a, object b) => (a, b) switch
        {
            (IndividualNode x, IndividualNode y) => ComparePeople(x, y),
            (FamilyNode x, FamilyNode y) => CompareFamilies(x, y),
            (SourceNode x, SourceNode y) => CompareSources(x, y),
            (RepositoryNode x, RepositoryNode y) => CompareRepositories(x, y),
            _ => new RecordDiff()
        };

        public RecordDiff ComparePeople(IndividualNode a, IndividualNode b)
        {
            var diff = new RecordDiff();
            AddFields(diff, 0,
                ("Name", a.FullName, b.FullName),
                ("Sex", Sex(a.Gender), Sex(b.Gender)),
                ("Birth date", a.BirthDate, b.BirthDate),
                ("Birth place", a.BirthPlace, b.BirthPlace),
                ("Death date", a.DeathDate, b.DeathDate),
                ("Death place", a.DeathPlace, b.DeathPlace));
            CompareTexts(diff, 1, "Alternate name", a.AlternateNames, b.AlternateNames);

            if (_details)
            {
                CompareEvents(diff, a.Events, b.Events);
                CompareAttachments(diff, 1, a.Citations, b.Citations,
                    NoteTexts(a.DirectNoteIds, a.InlineNotes, _this), NoteTexts(b.DirectNoteIds, b.InlineNotes, _other),
                    a.Media, b.Media);
            }
            return diff;
        }

        public RecordDiff CompareFamilies(FamilyNode a, FamilyNode b)
        {
            var diff = new RecordDiff();
            AddFields(diff, 0,
                ("Husband", SpouseText(a.Husband), SpouseText(b.Husband)),
                ("Wife", SpouseText(a.Wife), SpouseText(b.Wife)),
                ("Marriage date", a.MarriageDate, b.MarriageDate),
                ("Marriage place", a.MarriagePlace, b.MarriagePlace));
            CompareKeyed(diff, 1, "Child",
                Children(a, _this), Children(b, _other));

            if (_details)
            {
                CompareEvents(diff, a.Events, b.Events);
                CompareAttachments(diff, 1, a.Citations, b.Citations,
                    NoteTexts(a.DirectNoteIds, a.InlineNotes, _this), NoteTexts(b.DirectNoteIds, b.InlineNotes, _other),
                    a.Media, b.Media);
            }
            return diff;
        }

        public RecordDiff CompareSources(SourceNode a, SourceNode b)
        {
            var diff = new RecordDiff();
            AddFields(diff, 0, ("Repository", RepositoryName(a.RepositoryId, _this), RepositoryName(b.RepositoryId, _other)));
            CompareNotes(diff, 1, NoteTexts(a.NoteIds, a.InlineNotes, _this), NoteTexts(b.NoteIds, b.InlineNotes, _other));
            return diff;
        }

        public RecordDiff CompareRepositories(RepositoryNode a, RepositoryNode b)
        {
            var diff = new RecordDiff();
            CompareNotes(diff, 1, NoteTexts(a.NoteIds, Array.Empty<string>(), _this), NoteTexts(b.NoteIds, Array.Empty<string>(), _other));
            return diff;
        }

        // ---------------------------------------------------------------- events

        private void CompareEvents(RecordDiff diff, IEnumerable<GedcomEvent> thisEvents, IEnumerable<GedcomEvent> otherEvents)
        {
            var events = TreeDiffGenerator.Match(thisEvents.Where(e => !e.IsInternal), otherEvents.Where(e => !e.IsInternal),
                TreeDiffGenerator.EventKey);

            // An unmatched event on each side that is the same event with an edited
            // value (e.g. a refined date) is reported once, as [CHANGED].
            var changed = PairChangedEvents(events.Missing, events.Extra);
            var changedThis = new HashSet<GedcomEvent>(changed.Select(c => c.This), ReferenceEqualityComparer.Instance);
            var changedOther = new HashSet<GedcomEvent>(changed.Select(c => c.Other), ReferenceEqualityComparer.Instance);

            // Events in both files: compare what is attached to them.
            foreach (var (a, b) in events.Matched)
            {
                var sub = new RecordDiff();
                CompareEventAttachments(sub, a, b);
                if (sub.Count == 0) continue;
                diff.Lines.Add($"{Indent(1)}{Tag("[DIFFERENT]")}Event: {a.DisplayName}");
                diff.Lines.AddRange(sub.Lines);
                diff.Count += sub.Count;
            }

            foreach (var (a, b) in changed)
            {
                diff.Lines.Add($"{Indent(1)}{Tag("[CHANGED]")}Event: {a.DisplayName}");
                diff.Lines.AddRange(TreeDiffGenerator.DescribeFields(_sides, ValueIndent(1),
                    ("Date", a.Date, b.Date),
                    ("Place", a.Place, b.Place),
                    ("Type", TreeDiffGenerator.EffectiveSubType(a), TreeDiffGenerator.EffectiveSubType(b)),
                    ("Value", TreeDiffGenerator.EffectiveData(a), TreeDiffGenerator.EffectiveData(b))));
                diff.Count++;
                var sub = new RecordDiff();
                CompareEventAttachments(sub, a, b);
                diff.Lines.AddRange(sub.Lines);
                diff.Count += sub.Count;
            }

            foreach (var evt in events.Missing.Where(e => !changedThis.Contains(e)))
            {
                diff.Lines.Add($"{Indent(1)}{Tag("[MISSING]")}Event: {evt.DisplayName}{DuplicateNote(events.OtherKeys, TreeDiffGenerator.EventKey(evt))}");
                diff.Count++;
            }
            foreach (var evt in events.Extra.Where(e => !changedOther.Contains(e)))
            {
                diff.Lines.Add($"{Indent(1)}{Tag("[EXTRA]")}Event: {evt.DisplayName}{DuplicateNote(events.ThisKeys, TreeDiffGenerator.EventKey(evt))}");
                diff.Count++;
            }

            _totals.Matches += events.Matched.Count;
            _totals.Missing += events.Missing.Count - changed.Count;
            _totals.Extra += events.Extra.Count - changed.Count;
            _totals.Changed += changed.Count;
        }

        private void CompareEventAttachments(RecordDiff diff, GedcomEvent a, GedcomEvent b) =>
            CompareAttachments(diff, 2, a.Citations, b.Citations,
                NoteTexts(a.DirectNoteIds, a.InlineNotes, _this), NoteTexts(b.DirectNoteIds, b.InlineNotes, _other),
                a.Media, b.Media);

        /// <summary>
        /// Pairs an unmatched event on this side with one on the other side when
        /// they look like the same event with an edited value: same kind of event
        /// and the same date or the same place (pairs agreeing on both come first),
        /// or the only unmatched event of that kind on each side (a birth whose
        /// date was corrected, say).
        /// </summary>
        internal static List<(GedcomEvent This, GedcomEvent Other)> PairChangedEvents(
            List<GedcomEvent> thisOnly, List<GedcomEvent> otherOnly)
        {
            var pairs = new List<(GedcomEvent, GedcomEvent)>();
            var used = new bool[otherOnly.Count];
            var usedThis = new HashSet<GedcomEvent>(ReferenceEqualityComparer.Instance);

            // Pass 1: same date and place; pass 2: same date; pass 3: same place;
            // pass 4: the only one of its kind left unmatched on each side.
            for (int pass = 1; pass <= 4; pass++)
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
                            3 => samePlace,
                            _ => OnlyUnmatchedOfKind(a, thisOnly, x => usedThis.Contains(x)) &&
                                 OnlyUnmatchedOfKind(b, otherOnly, x => used[otherOnly.IndexOf(x)])
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

        private static bool OnlyUnmatchedOfKind(GedcomEvent e, List<GedcomEvent> events, Func<GedcomEvent, bool> isUsed) =>
            events.Count(x => !isUsed(x) && SameKindOfEvent(x, e)) == 1;

        private static bool SameKindOfEvent(GedcomEvent a, GedcomEvent b) =>
            a.Type == b.Type && (a.Type != "EVEN" || N(a.EventName) == N(b.EventName));

        // ------------------------------------------------- citations, notes, media

        /// <summary>
        /// Compares the citations, notes and media attached at one place (a record
        /// or an event), writing item lines at <paramref name="depth"/>.
        /// </summary>
        private void CompareAttachments(RecordDiff diff, int depth,
            List<GedcomCitation> thisCitations, List<GedcomCitation> otherCitations,
            List<(string Key, string Text)> thisNotes, List<(string Key, string Text)> otherNotes,
            List<string> thisMedia, List<string> otherMedia)
        {
            CompareCitations(diff, depth, thisCitations, otherCitations);
            CompareNotes(diff, depth, thisNotes, otherNotes);
            CompareKeyed(diff, depth, "Media",
                thisMedia.Select(m => MediaEntry(m, _this)).ToList(),
                otherMedia.Select(m => MediaEntry(m, _other)).ToList());
        }

        private void CompareCitations(RecordDiff diff, int depth, List<GedcomCitation> thisCitations, List<GedcomCitation> otherCitations)
        {
            // A citation is identified by its source and page; citations of the same
            // source whose page differs pair up one-to-one, as [CHANGED].
            var thisKeyed = thisCitations.Select(c => new KeyedCitation(SourceKey(c, _this), N(c.Page), c)).ToList();
            var otherKeyed = otherCitations.Select(c => new KeyedCitation(SourceKey(c, _other), N(c.Page), c)).ToList();
            var match = TreeDiffGenerator.Match(thisKeyed, otherKeyed, x => x.Source + "|" + x.Page);
            var changedPairs = TreeDiffGenerator.PairChangedRecords(match.Missing, match.Extra, x => x.Source);
            var changedThis = new HashSet<object>(changedPairs.Select(p => (object)p.This.C), ReferenceEqualityComparer.Instance);
            var changedOther = new HashSet<object>(changedPairs.Select(p => (object)p.Other.C), ReferenceEqualityComparer.Instance);

            foreach (var (a, b) in match.Matched.Select(m => (m.This.C, m.Other.C))
                         .Concat(changedPairs.Select(p => (p.This.C, p.Other.C))))
            {
                var lines = new List<string>();
                lines.AddRange(TreeDiffGenerator.DescribeFields(_sides, ValueIndent(depth),
                    ("Page", a.Page, b.Page),
                    ("Date", a.Date, b.Date),
                    ("Confidence", a.Quality, b.Quality)));
                lines.AddRange(SummarizeSetDifference(depth, "Notes",
                    NoteTexts(a.NoteIds, a.InlineNotes, _this), NoteTexts(b.NoteIds, b.InlineNotes, _other)));
                lines.AddRange(SummarizeSetDifference(depth, "Media",
                    a.Media.Select(m => MediaEntry(m, _this)).ToList(), b.Media.Select(m => MediaEntry(m, _other)).ToList()));

                if (lines.Count == 0)
                {
                    _totals.Matches++;
                    continue;
                }
                diff.Lines.Add($"{Indent(depth)}{Tag("[CHANGED]")}Citation: {CitationLabel(a, _this)}");
                diff.Lines.AddRange(lines);
                diff.Count++;
                _totals.Changed++;
            }

            foreach (var x in match.Missing.Where(x => !changedThis.Contains(x.C)))
            {
                diff.Lines.Add($"{Indent(depth)}{Tag("[MISSING]")}Citation: {CitationLabel(x.C, _this)}");
                diff.Count++;
                _totals.Missing++;
            }
            foreach (var x in match.Extra.Where(x => !changedOther.Contains(x.C)))
            {
                diff.Lines.Add($"{Indent(depth)}{Tag("[EXTRA]")}Citation: {CitationLabel(x.C, _other)}");
                diff.Count++;
                _totals.Extra++;
            }
        }

        private sealed record KeyedCitation(string Source, string Page, GedcomCitation C);

        private void CompareNotes(RecordDiff diff, int depth, List<(string Key, string Text)> thisNotes, List<(string Key, string Text)> otherNotes) =>
            CompareKeyed(diff, depth, "Note", thisNotes.Select(n => (n.Key, Quote(n.Text))).ToList(),
                otherNotes.Select(n => (n.Key, Quote(n.Text))).ToList());

        /// <summary>
        /// Items of one kind (children, notes, media) that are on only one side,
        /// one [MISSING] / [EXTRA] line each.
        /// </summary>
        private void CompareKeyed(RecordDiff diff, int depth, string kind,
            List<(string Key, string Text)> thisItems, List<(string Key, string Text)> otherItems)
        {
            var match = TreeDiffGenerator.Match(thisItems, otherItems, x => x.Key);
            foreach (var x in match.Missing)
            {
                diff.Lines.Add($"{Indent(depth)}{Tag("[MISSING]")}{kind}: {x.Text}");
                diff.Count++;
            }
            foreach (var x in match.Extra)
            {
                diff.Lines.Add($"{Indent(depth)}{Tag("[EXTRA]")}{kind}: {x.Text}");
                diff.Count++;
            }
            _totals.Matches += match.Matched.Count;
            _totals.Missing += match.Missing.Count;
            _totals.Extra += match.Extra.Count;
        }

        private void CompareTexts(RecordDiff diff, int depth, string kind, IEnumerable<string> a, IEnumerable<string> b) =>
            CompareKeyed(diff, depth, kind,
                a.Where(t => N(t).Length > 0).Select(t => (N(t), t.Trim())).ToList(),
                b.Where(t => N(t).Length > 0).Select(t => (N(t), t.Trim())).ToList());

        /// <summary>
        /// For a citation's notes or media: one line per side saying how many
        /// are only on that side, with a few examples.
        /// </summary>
        private IEnumerable<string> SummarizeSetDifference(int depth, string label,
            List<(string Key, string Text)> a, List<(string Key, string Text)> b)
        {
            var match = TreeDiffGenerator.Match(a, b, x => x.Key);
            string indent = ValueIndent(depth);
            string first = (label + ":").PadRight(label.Length + 2);
            string rest = new string(' ', first.Length);
            bool wroteFirst = false;

            foreach (var (items, side) in new[] { (match.Missing, _sides.This), (match.Extra, _sides.Other) })
            {
                if (items.Count == 0) continue;
                // The same note or file can be attached more than once: show it once, with a count.
                var distinct = items.GroupBy(x => x.Text).Select(g => (Text: g.Key, Copies: g.Count())).ToList();
                var examples = distinct.Take(2)
                    .Select(d => (label == "Notes" ? Quote(Truncate(d.Text, 45)) : d.Text) + (d.Copies > 1 ? $" (×{d.Copies})" : string.Empty))
                    .ToList();
                string more = distinct.Count > examples.Count ? ", ..." : string.Empty;
                yield return $"{indent}{(wroteFirst ? rest : first)}{items.Count} only in the {side} file: {string.Join(", ", examples)}{more}";
                wroteFirst = true;
            }
        }

        // ---------------------------------------------------------------- helpers

        private const int TagWidth = 11;

        private static string Tag(string tag) => tag.PadRight(TagWidth) + " ";

        /// <summary>Where a line's tag starts: records at 2 spaces, then 4 more per level.</summary>
        internal static string Indent(int depth) => new(' ', 2 + 4 * depth);

        /// <summary>Where the left/right values under a line start: under the text after its tag.</summary>
        internal static string ValueIndent(int depth) => new(' ', 2 + 4 * depth + TagWidth + 1);

        private void AddFields(RecordDiff diff, int depth, params (string Name, string This, string Other)[] fields)
        {
            var lines = TreeDiffGenerator.DescribeFields(_sides, ValueIndent(depth), fields).ToList();
            diff.Lines.AddRange(lines);
            diff.Count += lines.Count / 2;
        }

        private static string DuplicateNote(HashSet<string> otherSideKeys, string key) =>
            otherSideKeys.Contains(key) ? " (duplicate)" : string.Empty;

        /// <summary>"U" (unknown) and a missing value mean the same thing.</summary>
        internal static string Sex(string value)
        {
            string s = value.Trim();
            return s.Equals("U", StringComparison.OrdinalIgnoreCase) ? string.Empty : s;
        }

        /// <summary>
        /// A spouse as name plus birth date exactly as written in the file, so a
        /// corrected date ("1773" vs "ABT 1773") shows even where the display
        /// name formats both the same way.
        /// </summary>
        internal static string SpouseText(IndividualNode? person)
        {
            if (person == null) return string.Empty;
            return string.IsNullOrWhiteSpace(person.BirthDate)
                ? person.FullName
                : $"{person.FullName} (b. {person.BirthDate.Trim()})";
        }

        private static List<(string Key, string Text)> Children(FamilyNode family, GedcomTreeContext ctx) =>
            family.ChildrenIds
                .Select(id => ctx.Individuals.TryGetValue(id, out var p)
                    ? (TreeNodeKeys.GetKey(p)!, p.DisplayName)
                    : ("(unresolved person)", "(unresolved person)"))
                .ToList();

        private static string RepositoryName(string id, GedcomTreeContext ctx)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            return ctx.Repositories.TryGetValue(id, out var r) ? r.Name : "(unresolved repository)";
        }

        /// <summary>Notes by text (pointers resolved through the file's notes), for comparing.</summary>
        private static List<(string Key, string Text)> NoteTexts(IEnumerable<string> ids, IEnumerable<string> inline, GedcomTreeContext ctx) =>
            ids.Select(id => ctx.Notes.TryGetValue(id, out var n) ? n.Text : "(unresolved note)")
               .Concat(inline)
               .Where(t => N(t).Length > 0)
               .Select(t => (N(t), OneLine(t)))
               .ToList();

        private static string SourceKey(GedcomCitation c, GedcomTreeContext ctx)
        {
            if (c.SourceId.Length == 0) return "inline:" + N(c.InlineText);
            return ctx.Sources.TryGetValue(c.SourceId, out var s) ? TreeNodeKeys.GetKey(s)! : "(unresolved source)";
        }

        private static string CitationLabel(GedcomCitation c, GedcomTreeContext ctx)
        {
            string source = c.SourceId.Length == 0
                ? OneLine(c.InlineText)
                : ctx.Sources.TryGetValue(c.SourceId, out var s) ? s.Title : "(unresolved source)";
            string page = OneLine(c.Page);
            return page.Length == 0 ? source : $"{source}, {Truncate(page, 60)}";
        }

        /// <summary>A media item by its file name (folders differ between computers), else its title.</summary>
        private static (string Key, string Text) MediaEntry(string reference, GedcomTreeContext ctx)
        {
            string file, title = string.Empty;
            if (reference.StartsWith("file:", StringComparison.Ordinal)) file = reference.Substring(5);
            else if (ctx.Media.TryGetValue(reference, out var m)) { file = m.File; title = m.Title; }
            else return ("(unresolved media)", "(unresolved media)");

            string name = file.Substring(file.LastIndexOfAny(new[] { '/', '\\' }) + 1).Trim();
            if (name.Length == 0) name = title.Trim();
            return name.Length == 0 ? ("(unnamed media)", "(unnamed media)") : (N(name), name);
        }

        private static string Quote(string text) => $"\"{Truncate(text, 60)}\"";

        private static string OneLine(string text) =>
            string.Join(" ", (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static string Truncate(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + "…";

        private static string N(string value) => TreeNodeKeys.NormalizeName(value);
    }
}
