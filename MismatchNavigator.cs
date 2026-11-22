using System;
using System.Collections.Generic;
using System.Linq;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Coordinates navigation between the left and right trees, jumping
    /// to the next or previous mismatch (a node missing from one tree,
    /// or a node whose data differs between the two trees).
    /// <para>
    /// Records are compared by content, never by GEDCOM record Id: two exports
    /// of the same tree usually number their records differently (@S55@ vs
    /// @S00055@), so linked sources and notes are compared by their stable keys
    /// and dates, places and names are compared after normalization
    /// ("24 Aug 1938" equals "24 AUG 1938").
    /// </para>
    /// </summary>
    internal class MismatchNavigator
    {
        private DiffTreeViewModel? _left;
        private DiffTreeViewModel? _right;

        public void Attach(DiffTreeViewModel left, DiffTreeViewModel right)
        {
            _left = left;
            _right = right;
        }

        public void Detach()
        {
            _left = null;
            _right = null;
        }

        /// <summary>
        /// Navigates to the next mismatch, starting from the currently selected
        /// node (in either tree). The match is selected in <paramref name="source"/>
        /// when it exists there, otherwise in the other tree.
        /// </summary>
        public void NavigateToNextMismatch(DiffTreeViewModel source)
        {
            Navigate(source, 1);
        }

        /// <summary>
        /// Navigates to the previous mismatch, starting from the currently
        /// selected node (in either tree).
        /// </summary>
        public void NavigateToPreviousMismatch(DiffTreeViewModel source)
        {
            Navigate(source, -1);
        }

        private void Navigate(DiffTreeViewModel source, int direction)
        {
            if (_left == null || _right == null) return;
            var other = ReferenceEquals(source, _left) ? _right : _left;

            var sourceFlat = FlattenWithKeys(source);
            var otherFlat = FlattenWithKeys(other);
            var sourceNodes = FirstByKey(sourceFlat);
            var otherNodes = FirstByKey(otherFlat);

            var allKeys = MergeOrder(sourceFlat, otherFlat, sourceNodes);
            if (allKeys.Count == 0) return;

            var comparer = new NodeComparer(source.Context, other.Context);
            bool IsMismatch(string key)
            {
                sourceNodes.TryGetValue(key, out var a);
                otherNodes.TryGetValue(key, out var b);
                if (a == null || b == null) return true;
                return !comparer.NodesEqual(a, b);
            }

            // The current position is the selected node in either tree (a record
            // that exists only in the other tree is selected there).
            int currentIndex = -1;
            var selectedKey = source.GetSelectedNodeKey() ?? other.GetSelectedNodeKey();
            if (selectedKey != null)
            {
                currentIndex = allKeys.IndexOf(selectedKey);
            }

            int i = currentIndex >= 0
                ? currentIndex + direction
                : (direction > 0 ? 0 : allKeys.Count - 1);

            for (; i >= 0 && i < allKeys.Count; i += direction)
            {
                var key = allKeys[i];
                if (!IsMismatch(key)) continue;

                if (sourceNodes.TryGetValue(key, out var inSource))
                    source.SelectNode(inSource);
                else if (otherNodes.TryGetValue(key, out var inOther))
                    other.SelectNode(inOther);
                return;
            }
        }

        /// <summary>
        /// Builds one ordered key list covering both trees: the source tree's order,
        /// with keys that exist only in the other tree inserted right after the
        /// nearest preceding key the two trees share.
        /// </summary>
        private static List<string> MergeOrder(List<(string Key, object Node)> sourceFlat,
            List<(string Key, object Node)> otherFlat, Dictionary<string, object> sourceNodes)
        {
            var leading = new List<string>();                          // other-only keys before any shared key
            var insertAfter = new Dictionary<string, List<string>>();   // shared key -> other-only keys after it
            var seenOtherOnly = new HashSet<string>();
            string? anchor = null;

            foreach (var (key, _) in otherFlat)
            {
                if (sourceNodes.ContainsKey(key))
                {
                    anchor = key;
                    continue;
                }
                if (!seenOtherOnly.Add(key)) continue;

                if (anchor == null)
                {
                    leading.Add(key);
                }
                else
                {
                    if (!insertAfter.TryGetValue(anchor, out var list))
                        insertAfter[anchor] = list = new List<string>();
                    list.Add(key);
                }
            }

            var result = new List<string>(leading);
            var seen = new HashSet<string>(leading);
            foreach (var (key, _) in sourceFlat)
            {
                if (!seen.Add(key)) continue;
                result.Add(key);
                if (insertAfter.TryGetValue(key, out var following))
                {
                    foreach (var k in following)
                    {
                        if (seen.Add(k)) result.Add(k);
                    }
                }
            }
            return result;
        }

        private static Dictionary<string, object> FirstByKey(List<(string Key, object Node)> flat)
        {
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var (key, node) in flat)
            {
                map.TryAdd(key, node);
            }
            return map;
        }

        private static List<(string Key, object Node)> FlattenWithKeys(DiffTreeViewModel vm)
        {
            var result = new List<(string, object)>();
            foreach (var group in vm.TreeNodes)
            {
                var key = TreeNodeKeys.GetKey(group);
                if (key != null)
                {
                    result.Add((key, group));
                }
                FlattenChildrenWithKeys(group.Children, result);
            }
            return result;
        }

        private static void FlattenChildrenWithKeys(IEnumerable<object> children, List<(string, object)> result)
        {
            foreach (var child in children)
            {
                var key = TreeNodeKeys.GetKey(child);
                if (key != null)
                {
                    result.Add((key, child));
                }
                var subChildren = TreeNodeKeys.GetChildren(child);
                if (subChildren != null)
                {
                    FlattenChildrenWithKeys(subChildren, result);
                }
            }
        }

        /// <summary>
        /// Compares two nodes already matched by stable key, resolving each tree's
        /// record pointers through that tree's own context.
        /// </summary>
        internal sealed class NodeComparer
        {
            private readonly GedcomTreeContext _a;
            private readonly GedcomTreeContext _b;

            public NodeComparer(GedcomTreeContext a, GedcomTreeContext b)
            {
                _a = a;
                _b = b;
            }

            public bool NodesEqual(object a, object b)
            {
                return a switch
                {
                    IndividualNode ia => b is IndividualNode ib && IndividualEqual(ia, ib),
                    FamilyNode fa => b is FamilyNode fb && FamilyEqual(fa, fb),
                    SourceNode sa => b is SourceNode sb && SourceEqual(sa, sb),
                    RepositoryNode ra => b is RepositoryNode rb && RepositoryEqual(ra, rb),
                    NoteNode na => b is NoteNode nb && N(na.Text) == N(nb.Text),
                    GedcomHeader ha => b is GedcomHeader hb && HeaderEqual(ha, hb),
                    TreeGroupNode ga => b is TreeGroupNode gb && ga.Key == gb.Key,
                    SurnameGroupNode sa => b is SurnameGroupNode sb && sa.Surname == sb.Surname,
                    _ => ReferenceEquals(a, b)
                };
            }

            private bool IndividualEqual(IndividualNode a, IndividualNode b)
            {
                return N(a.FullName) == N(b.FullName) &&
                       Sex(a.Gender) == Sex(b.Gender) &&
                       N(a.BirthDate) == N(b.BirthDate) &&
                       N(a.BirthPlace) == N(b.BirthPlace) &&
                       N(a.DeathDate) == N(b.DeathDate) &&
                       N(a.DeathPlace) == N(b.DeathPlace) &&
                       TextSet(a.AlternateNames).SetEquals(TextSet(b.AlternateNames)) &&
                       SourceKeys(a.SourceIds, _a).SetEquals(SourceKeys(b.SourceIds, _b)) &&
                       SourceKeys(EventSourceIds(a.Events), _a).SetEquals(SourceKeys(EventSourceIds(b.Events), _b)) &&
                       NoteKeys(a.NoteIds, _a).SetEquals(NoteKeys(b.NoteIds, _b)) &&
                       TextSet(a.InlineNotes).SetEquals(TextSet(b.InlineNotes));
            }

            private bool FamilyEqual(FamilyNode a, FamilyNode b)
            {
                // Husband/wife identity is already matched by the stable family key
                // (built from their person keys), so here we only compare the
                // remaining family-specific data.
                return N(a.MarriageDate) == N(b.MarriageDate) &&
                       N(a.MarriagePlace) == N(b.MarriagePlace) &&
                       PersonKeys(a.ChildrenIds, _a).SetEquals(PersonKeys(b.ChildrenIds, _b)) &&
                       SourceKeys(a.SourceIds, _a).SetEquals(SourceKeys(b.SourceIds, _b)) &&
                       SourceKeys(EventSourceIds(a.Events), _a).SetEquals(SourceKeys(EventSourceIds(b.Events), _b)) &&
                       NoteKeys(a.NoteIds, _a).SetEquals(NoteKeys(b.NoteIds, _b)) &&
                       TextSet(a.InlineNotes).SetEquals(TextSet(b.InlineNotes));
            }

            private bool SourceEqual(SourceNode a, SourceNode b)
            {
                return N(a.Title) == N(b.Title) &&
                       N(a.Author) == N(b.Author) &&
                       N(a.PublicationInfo) == N(b.PublicationInfo) &&
                       RepositoryKey(a.RepositoryId, _a) == RepositoryKey(b.RepositoryId, _b) &&
                       NoteKeys(a.NoteIds, _a).SetEquals(NoteKeys(b.NoteIds, _b)) &&
                       TextSet(a.InlineNotes).SetEquals(TextSet(b.InlineNotes));
            }

            private bool RepositoryEqual(RepositoryNode a, RepositoryNode b)
            {
                return N(a.Name) == N(b.Name) &&
                       N(a.Address) == N(b.Address) &&
                       NoteKeys(a.NoteIds, _a).SetEquals(NoteKeys(b.NoteIds, _b));
            }

            private static bool HeaderEqual(GedcomHeader a, GedcomHeader b)
            {
                // The file creation date is left out: it differs between every two exports.
                return N(a.SourceSoftware) == N(b.SourceSoftware) &&
                       N(a.SoftwareVersion) == N(b.SoftwareVersion) &&
                       N(a.GedcomVersion) == N(b.GedcomVersion) &&
                       N(a.CharacterEncoding) == N(b.CharacterEncoding);
            }

            private static string N(string value) => TreeNodeKeys.NormalizeName(value);

            /// <summary>
            /// Normalizes a SEX value for comparison. "U" (unknown) and a missing
            /// value mean the same thing, so both compare as empty.
            /// </summary>
            internal static string Sex(string value)
            {
                string s = N(value);
                return s == "u" ? string.Empty : s;
            }

            private static HashSet<string> TextSet(IEnumerable<string> values) =>
                values.Select(N).Where(v => v.Length > 0).ToHashSet();

            private static IEnumerable<string> EventSourceIds(IEnumerable<GedcomEvent> events) =>
                events.SelectMany(e => e.SourceIds);

            private static HashSet<string> SourceKeys(IEnumerable<string> ids, GedcomTreeContext ctx) =>
                ids.Select(id => ctx.Sources.TryGetValue(id, out var s) ? TreeNodeKeys.GetKey(s)! : "(unresolved source)")
                   .ToHashSet();

            private static HashSet<string> NoteKeys(IEnumerable<string> ids, GedcomTreeContext ctx) =>
                ids.Select(id => ctx.Notes.TryGetValue(id, out var n) ? TreeNodeKeys.GetKey(n)! : "(unresolved note)")
                   .ToHashSet();

            private static HashSet<string> PersonKeys(IEnumerable<string> ids, GedcomTreeContext ctx) =>
                ids.Select(id => ctx.Individuals.TryGetValue(id, out var p) ? TreeNodeKeys.GetKey(p)! : "(unresolved person)")
                   .ToHashSet();

            private static string RepositoryKey(string id, GedcomTreeContext ctx)
            {
                if (string.IsNullOrEmpty(id)) return string.Empty;
                return ctx.Repositories.TryGetValue(id, out var r) ? TreeNodeKeys.GetKey(r)! : "(unresolved repository)";
            }
        }
    }
}
