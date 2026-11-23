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
    /// "Differs" is decided by <see cref="RecordComparer"/>, the same comparison
    /// the Diff report uses, so the arrows stop at exactly the records the report
    /// lists. Records are compared by content, never by GEDCOM record Id.
    /// </para>
    /// </summary>
    internal class MismatchNavigator
    {
        private DiffTreeViewModel? _left;
        private DiffTreeViewModel? _right;

        // Where the last ▲/▼ press stopped. Kept because a second copy of a record
        // (same key) can't be told apart from the first by its key alone.
        private string? _lastStopKey;
        private object? _lastStopNode;

        // Marks the second, third... copy of a key within one tree ("key\u00012").
        private const char CopySeparator = '\u0001';

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

            // The Diff report's own comparison, so the arrows stop exactly at the
            // records it lists (with Details on, that includes events, citations,
            // notes and media).
            var comparer = new RecordComparer(source.Context, other.Context, source.ShowDetails, ("this", "other"));

            // Copies of the same record get their own position (key, key#2, ...),
            // paired across the trees the way the Diff report pairs them, so each
            // extra or differing copy is a stop of its own.
            var (sourceFlat, otherFlat) = NumberCopies(FlattenWithKeys(source), FlattenWithKeys(other),
                (a, b) => comparer.Compare(a, b).Count);
            var sourceNodes = FirstByKey(sourceFlat);
            var otherNodes = FirstByKey(otherFlat);

            var allKeys = MergeOrder(sourceFlat, otherFlat, sourceNodes);
            if (allKeys.Count == 0) return;

            // A person or family whose identifying values changed (e.g. a corrected
            // birth date) has a different key in each tree. The Diff report shows it
            // once, as [CHANGED]; stop at it once too, at its copy in the source tree.
            var otherHalfOfChanged = ChangedRecordPartners(sourceNodes, otherNodes);

            bool IsMismatch(string key)
            {
                if (otherHalfOfChanged.Contains(key)) return false;
                sourceNodes.TryGetValue(key, out var a);
                otherNodes.TryGetValue(key, out var b);

                // Group headings (Individuals, a surname...) are never stops of their
                // own: a surname only one file has is reached through its people.
                if ((a ?? b) is TreeGroupNode or SurnameGroupNode) return false;
                if (a == null || b == null) return true;
                return comparer.Compare(a, b).Count > 0;
            }

            // The current position: the last stop if it is still selected, otherwise
            // the selected node in either tree (a record that exists only in the
            // other tree is selected there).
            string? currentKey = null;
            if (_lastStopNode is TreeNodeBase last && last.IsSelected)
            {
                currentKey = _lastStopKey;
            }
            else if ((source.GetSelectedNode() ?? other.GetSelectedNode()) is { } selected)
            {
                currentKey = sourceFlat.Concat(otherFlat).FirstOrDefault(x => ReferenceEquals(x.Node, selected)).Key;
            }
            int currentIndex = currentKey != null ? allKeys.IndexOf(currentKey) : -1;

            int i = currentIndex >= 0
                ? currentIndex + direction
                : (direction > 0 ? 0 : allKeys.Count - 1);

            for (; i >= 0 && i < allKeys.Count; i += direction)
            {
                var key = allKeys[i];
                if (!IsMismatch(key)) continue;

                object? stop = null;
                if (sourceNodes.TryGetValue(key, out var inSource))
                    source.SelectNode(stop = inSource);
                else if (otherNodes.TryGetValue(key, out var inOther))
                    other.SelectNode(stop = inOther);
                _lastStopKey = key;
                _lastStopNode = stop;
                return;
            }
        }

        /// <summary>
        /// Keys of records that exist only in the other tree but pair one-to-one,
        /// by name, with a record that exists only in the source tree, using the
        /// same rule as the Diff report's [CHANGED] records.
        /// </summary>
        private static HashSet<string> ChangedRecordPartners(Dictionary<string, object> sourceNodes,
            Dictionary<string, object> otherNodes)
        {
            static Dictionary<string, List<string>> UnmatchedByPairKey(
                Dictionary<string, object> nodes, Dictionary<string, object> otherSide) =>
                nodes.Where(kv => !otherSide.ContainsKey(kv.Key))
                     .Select(kv => (kv.Key, Pair: TreeDiffGenerator.RecordPairKey(kv.Value)))
                     .Where(x => x.Pair.Length > 0)
                     .GroupBy(x => x.Pair)
                     .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

            var sourceOnly = UnmatchedByPairKey(sourceNodes, otherNodes);
            var otherOnly = UnmatchedByPairKey(otherNodes, sourceNodes);

            var partners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (pairKey, otherKeys) in otherOnly)
            {
                if (otherKeys.Count == 1 && sourceOnly.TryGetValue(pairKey, out var sourceKeys) && sourceKeys.Count == 1)
                    partners.Add(otherKeys[0]);
            }
            return partners;
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

        /// <summary>
        /// Gives the second and later copies of a key their own key ("key",
        /// "key\u00012", ...). Source copies are numbered in tree order; each other-tree
        /// copy takes the number of the source copy it pairs with (fewest
        /// differences, as in the Diff report), and leftover copies come after.
        /// </summary>
        private static (List<(string Key, object Node)> Source, List<(string Key, object Node)> Other) NumberCopies(
            List<(string Key, object Node)> source, List<(string Key, object Node)> other, Func<object, object, int> distance)
        {
            static Dictionary<string, List<int>> Groups(List<(string Key, object Node)> flat)
            {
                var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                for (int i = 0; i < flat.Count; i++)
                {
                    if (!groups.TryGetValue(flat[i].Key, out var g)) groups[flat[i].Key] = g = new List<int>();
                    g.Add(i);
                }
                return groups;
            }
            static string Copy(string key, int n) => n == 1 ? key : key + CopySeparator + n;

            var sourceGroups = Groups(source);
            var otherGroups = Groups(other);
            var sourceKeys = source.Select(x => x.Key).ToArray();
            var otherKeys = other.Select(x => x.Key).ToArray();

            foreach (var (key, si) in sourceGroups)
            {
                otherGroups.TryGetValue(key, out var oi);
                oi ??= new List<int>();
                if (si.Count == 1 && oi.Count <= 1) continue;

                for (int n = 1; n < si.Count; n++) sourceKeys[si[n]] = Copy(key, n + 1);

                var used = new bool[oi.Count];
                foreach (var (a, b) in TreeDiffGenerator.PairCopies(
                             si.Select(i => source[i].Node).ToList(), oi.Select(j => other[j].Node).ToList(), distance))
                {
                    otherKeys[oi[b]] = Copy(key, a + 1);
                    used[b] = true;
                }
                int next = si.Count + 1;
                for (int b = 0; b < oi.Count; b++)
                {
                    if (!used[b]) otherKeys[oi[b]] = Copy(key, next++);
                }
            }
            foreach (var (key, oi) in otherGroups)
            {
                if (sourceGroups.ContainsKey(key)) continue;
                for (int n = 1; n < oi.Count; n++) otherKeys[oi[n]] = Copy(key, n + 1);
            }

            return (source.Select((x, i) => (sourceKeys[i], x.Node)).ToList(),
                    other.Select((x, i) => (otherKeys[i], x.Node)).ToList());
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
            var result = new List<(string Key, object Node)>();
            foreach (var group in vm.TreeNodes)
            {
                var key = TreeNodeKeys.GetKey(group);
                if (key != null)
                {
                    result.Add((key, group));
                }
                FlattenChildrenWithKeys(group.Children, result);
            }
            // A record can be reached by more than one path in the tree: count it once.
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            return result.Where(x => seen.Add(x.Node)).ToList();
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
    }
}
