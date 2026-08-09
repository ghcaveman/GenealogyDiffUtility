using System;
using System.Collections.Generic;
using System.Linq;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Coordinates navigation between the left and right trees, jumping
    /// to the next or previous mismatch (a node missing from one tree,
    /// or a node whose data differs between the two trees).
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
        /// Navigates to the next mismatch in the tree, starting from the
        /// currently selected node in the given source tree.
        /// </summary>
        public void NavigateToNextMismatch(DiffTreeViewModel source)
        {
            Navigate(source, 1);
        }

        /// <summary>
        /// Navigates to the previous mismatch in the tree, starting from the
        /// currently selected node in the given source tree.
        /// </summary>
        public void NavigateToPreviousMismatch(DiffTreeViewModel source)
        {
            Navigate(source, -1);
        }

        private void Navigate(DiffTreeViewModel source, int direction)
        {
            if (_left == null || _right == null) return;

            // Build a unified ordered list of all node keys from both trees
            var leftFlat = FlattenWithKeys(_left);
            var rightFlat = FlattenWithKeys(_right);

            // Build a unified ordered list of unique keys (preserving tree order)
            var allKeys = new List<string>();
            var keySet = new HashSet<string>();
            foreach (var (key, _) in leftFlat.Concat(rightFlat))
            {
                if (keySet.Add(key))
                {
                    allKeys.Add(key);
                }
            }

            // Determine which keys are mismatches
            var mismatchKeys = new HashSet<string>();
            foreach (var key in allKeys)
            {
                if (IsMismatch(key, leftFlat, rightFlat))
                {
                    mismatchKeys.Add(key);
                }
            }

            if (mismatchKeys.Count == 0) return;

            // Find the current position based on the selected node in the source tree
            int currentIndex = -1;
            var selectedKey = source.GetSelectedNodeKey();
            if (selectedKey != null)
            {
                currentIndex = allKeys.IndexOf(selectedKey);
            }

            // Find the next/previous mismatch
            int targetIndex = -1;
            if (currentIndex < 0)
            {
                // No selection - start from beginning (down) or end (up)
                if (direction > 0)
                {
                    for (int i = 0; i < allKeys.Count; i++)
                    {
                        if (mismatchKeys.Contains(allKeys[i]))
                        {
                            targetIndex = i;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = allKeys.Count - 1; i >= 0; i--)
                    {
                        if (mismatchKeys.Contains(allKeys[i]))
                        {
                            targetIndex = i;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Start from the current position and find the next/previous mismatch
                int i = currentIndex + direction;
                while (i >= 0 && i < allKeys.Count)
                {
                    if (mismatchKeys.Contains(allKeys[i]))
                    {
                        targetIndex = i;
                        break;
                    }
                    i += direction;
                }
            }

            if (targetIndex < 0) return;

            // Navigate to the target node in the source tree
            var targetKey = allKeys[targetIndex];
            var targetNode = FindNodeByKey(source, targetKey);
            if (targetNode == null) return;

            // Select the node in the source tree
            source.SelectNode(targetNode);
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

        private static bool IsMismatch(string key, List<(string Key, object Node)> left, List<(string Key, object Node)> right)
        {
            var leftNode = left.FirstOrDefault(n => n.Key == key).Node;
            var rightNode = right.FirstOrDefault(n => n.Key == key).Node;

            // Missing from one side
            if (leftNode == null || rightNode == null) return true;

            // Data difference
            return !NodesEqual(leftNode, rightNode);
        }

        private static bool NodesEqual(object a, object b)
        {
            return a switch
            {
                IndividualNode ia => IndividualEqual(ia, b as IndividualNode),
                FamilyNode fa => FamilyEqual(fa, b as FamilyNode),
                SourceNode sa => SourceEqual(sa, b as SourceNode),
                RepositoryNode ra => RepositoryEqual(ra, b as RepositoryNode),
                NoteNode na => NoteEqual(na, b as NoteNode),
                GedcomHeader ha => HeaderEqual(ha, b as GedcomHeader),
                TreeGroupNode ga => GroupEqual(ga, b as TreeGroupNode),
                SurnameGroupNode sa => SurnameEqual(sa, b as SurnameGroupNode),
                _ => ReferenceEquals(a, b)
            };
        }

        private static bool IndividualEqual(IndividualNode a, IndividualNode? b)
        {
            if (b == null) return false;
            return a.FullName == b.FullName &&
                   a.Gender == b.Gender &&
                   a.BirthDate == b.BirthDate &&
                   a.BirthPlace == b.BirthPlace &&
                   a.DeathDate == b.DeathDate &&
                   a.DeathPlace == b.DeathPlace &&
                   StringSetEqual(a.SourceIds, b.SourceIds) &&
                   StringSetEqual(CollectEventSourceIds(a.Events), CollectEventSourceIds(b.Events)) &&
                   StringSetEqual(a.NoteIds, b.NoteIds);
        }

        private static bool FamilyEqual(FamilyNode a, FamilyNode? b)
        {
            if (b == null) return false;
            // Husband/wife identity is already matched by the stable family key
            // (built from their person keys), so here we only compare the
            // remaining family-specific data.
            return a.MarriageDate == b.MarriageDate &&
                   a.MarriagePlace == b.MarriagePlace &&
                   a.ChildrenIds.Count == b.ChildrenIds.Count &&
                   StringSetEqual(a.SourceIds, b.SourceIds) &&
                   StringSetEqual(CollectEventSourceIds(a.Events), CollectEventSourceIds(b.Events)) &&
                   a.NoteIds.Count == b.NoteIds.Count;
        }
        private static bool StringSetEqual(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            var setA = new HashSet<string>(a);
            var setB = new HashSet<string>(b);
            return setA.SetEquals(setB);
        }
        private static bool StringSetEqual(IEnumerable<string> a, IEnumerable<string> b)
        {
            var setA = new HashSet<string>(a);
            var setB = new HashSet<string>(b);
            return setA.SetEquals(setB);
        }

        /// <summary>
        /// Collects the union of all source IDs cited by the given events.
        /// Used to detect differences in which sources prove an individual's
        /// or family's events (e.g., a birth, death, or marriage citation).
        /// </summary>
        private static HashSet<string> CollectEventSourceIds(IEnumerable<GedcomEvent> events)
        {
            var set = new HashSet<string>();
            foreach (var evt in events)
            {
                foreach (var sid in evt.SourceIds)
                {
                    set.Add(sid);
                }
            }
            return set;
        }

        private static bool SourceEqual(SourceNode a, SourceNode? b)
        {
            if (b == null) return false;
            // Source identity is already matched by the stable source key
            // (built from title, author, and publication info), so here we only
            // compare the remaining source-specific data.
            return a.Title == b.Title &&
                   a.Author == b.Author &&
                   a.PublicationInfo == b.PublicationInfo &&
                   a.NoteIds.Count == b.NoteIds.Count;
        }

        private static bool RepositoryEqual(RepositoryNode a, RepositoryNode? b)
        {
            if (b == null) return false;
            // Repository identity is already matched by the stable repository key
            // (built from name and address), so here we only compare remaining data.
            return a.Name == b.Name &&
                   a.Address == b.Address &&
                   a.NoteIds.Count == b.NoteIds.Count;
        }

        private static bool NoteEqual(NoteNode a, NoteNode? b)
        {
            if (b == null) return false;
            return a.Text == b.Text;
        }

        private static bool HeaderEqual(GedcomHeader a, GedcomHeader? b)
        {
            if (b == null) return false;
            return a.SourceSoftware == b.SourceSoftware &&
                   a.SoftwareVersion == b.SoftwareVersion &&
                   a.GedcomVersion == b.GedcomVersion &&
                   a.CharacterEncoding == b.CharacterEncoding &&
                   a.FileCreationDate == b.FileCreationDate;
        }

        private static bool GroupEqual(TreeGroupNode a, TreeGroupNode? b)
        {
            if (b == null) return false;
            return a.Key == b.Key;
        }

        private static bool SurnameEqual(SurnameGroupNode a, SurnameGroupNode? b)
        {
            if (b == null) return false;
            return a.Surname == b.Surname;
        }

        private static object? FindNodeByKey(DiffTreeViewModel vm, string key)
        {
            foreach (var group in vm.TreeNodes)
            {
                if (TreeNodeKeys.GetKey(group) == key) return group;
                var found = FindNodeByKeyRecursive(group.Children, key);
                if (found != null) return found;
            }
            return null;
        }

        private static object? FindNodeByKeyRecursive(IEnumerable<object> children, string key)
        {
            foreach (var child in children)
            {
                if (TreeNodeKeys.GetKey(child) == key) return child;
                var subChildren = TreeNodeKeys.GetChildren(child);
                if (subChildren != null)
                {
                    var found = FindNodeByKeyRecursive(subChildren, key);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}