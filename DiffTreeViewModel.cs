using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace GenealogyDiffUtility
{
    internal class DiffTreeViewModel : ViewModelBase
    {
        private readonly Dictionary<object, TreeNodeBase> _nodeMap = new();
        private GedcomTreeContext _context = new();
        private string _fileName = "No File Loaded";
        private string _rawFileText = string.Empty;

        /// <summary>
        /// Raised when the user selects or expands a node in this tree,
        /// so the other tree can follow to the equivalent node.
        /// </summary>
        public event EventHandler<TreeSyncEventArgs>? SyncRequested;

        /// <summary>
        /// Raised after <see cref="ApplySync"/> completes, so the view layer
        /// can scroll the target node into view.
        /// </summary>
        public event EventHandler<TreeSyncEventArgs>? ScrollToNodeRequested;

        public string FileName
        {
            get => _fileName;
            set => RaiseAndSetIfChanged(ref _fileName, value);
        }

        public string RawFileText
        {
            get => _rawFileText;
            set => RaiseAndSetIfChanged(ref _rawFileText, value);
        }

        public string SourceSoftware => _context.Header.SourceSoftware;
        public string GedcomVersion => _context.Header.GedcomVersion;

        /// <summary>
        /// The parsed GEDCOM tree context, used by the edit dialog to look up
        /// spouses and children associated with an individual.
        /// </summary>
        public GedcomTreeContext Context => _context;

        private bool _showDetails;
        /// <summary>
        /// When true, each individual in the tree shows sub-nodes for their
        /// spouses, children, events, and notes; each family shows sub-nodes
        /// for spouses, children, and events. Toggling this rebuilds those
        /// detail sub-nodes in place without reloading the whole tree.
        /// </summary>
        public bool ShowDetails
        {
            get => _showDetails;
            set
            {
                if (RaiseAndSetIfChanged(ref _showDetails, value))
                {
                    RefreshDetails();
                }
            }
        }

        // The unified hierarchical collection your TreeView control will bind onto
        public ObservableCollection<TreeGroupNode> TreeNodes { get; } = new();

        public void LoadTree(GedcomTreeContext context, string fileName, string rawText)
        {
            _context = context;
            FileName = fileName;
            RawFileText = rawText;

            OnPropertyChanged(nameof(SourceSoftware));
            OnPropertyChanged(nameof(GedcomVersion));

            UnsubscribeAllNodes();
            TreeNodes.Clear();
            _nodeMap.Clear();

            // Assign collision indices to individuals without birth dates who share
            // the same name, so their stable identity keys are unique within this tree.
            AssignCollisionIndices(_context);

            // 1. Header Node
            var headerNode = new TreeGroupNode
            {
                Key = "Header",
                Name = "Header",
                Children = new List<object> { _context.Header }
            };
            TreeNodes.Add(headerNode);
            RegisterNode(headerNode);
            RegisterNode(_context.Header);

            // 2. Individuals Node (Grouped dynamically by Surname)
            var surnameGroups = _context.Individuals.Values
                .GroupBy(p => string.IsNullOrWhiteSpace(p.LastName) ? "Unknown" : p.LastName)
                .OrderBy(g => g.Key)
                .Select(g => new SurnameGroupNode { Surname = g.Key, People = g.ToList() })
                .Cast<object>()
                .ToList();

            var individualsNode = new TreeGroupNode
            {
                Key = "Individuals",
                Name = $"Individuals ({_context.Individuals.Count})",
                Children = surnameGroups
            };
            TreeNodes.Add(individualsNode);
            RegisterNode(individualsNode);
            foreach (var sg in surnameGroups.Cast<SurnameGroupNode>())
            {
                RegisterNode(sg);
                foreach (var p in sg.People)
                {
                    RegisterNode(p);
                }
            }

            // 3. Families Node
            // Resolve husband/wife references so the family can display meaningful names
            foreach (var f in _context.Families.Values)
            {
                if (!string.IsNullOrEmpty(f.HusbandId) && _context.Individuals.TryGetValue(f.HusbandId, out var husband))
                    f.Husband = husband;
                if (!string.IsNullOrEmpty(f.WifeId) && _context.Individuals.TryGetValue(f.WifeId, out var wife))
                    f.Wife = wife;
            }

            var familiesNode = new TreeGroupNode
            {
                Key = "Families",
                Name = $"Families ({_context.Families.Count})",
                Children = _context.Families.Values.OrderBy(f => f.DisplayName).Cast<object>().ToList()
            };
            TreeNodes.Add(familiesNode);
            RegisterNode(familiesNode);
            foreach (var f in _context.Families.Values)
            {
                RegisterNode(f);
            }

            // 4. Sources Node
            var sourcesNode = new TreeGroupNode
            {
                Key = "Sources",
                Name = $"Sources ({_context.Sources.Count})",
                Children = _context.Sources.Values
                    .OrderBy(s => s.Title)
                    .ThenBy(s => s.Id)
                    .Cast<object>()
                    .ToList()
            };
            TreeNodes.Add(sourcesNode);
            RegisterNode(sourcesNode);
            foreach (var s in _context.Sources.Values)
            {
                RegisterNode(s);
            }

            // 5. Repositories Node
            var repositoriesNode = new TreeGroupNode
            {
                Key = "Repositories",
                Name = $"Repositories ({_context.Repositories.Count})",
                Children = _context.Repositories.Values
                    .OrderBy(r => r.Name)
                    .ThenBy(r => r.Id)
                    .Cast<object>()
                    .ToList()
            };
            TreeNodes.Add(repositoriesNode);
            RegisterNode(repositoriesNode);
            foreach (var r in _context.Repositories.Values)
            {
                RegisterNode(r);
            }

            // 6. Notes Node
            var notesNode = new TreeGroupNode
            {
                Key = "Notes",
                Name = $"Notes ({_context.Notes.Count})",
                Children = _context.Notes.Values
                    .OrderBy(n => n.DisplayName)
                    .ThenBy(n => n.Id)
                    .Cast<object>()
                    .ToList()
            };
            TreeNodes.Add(notesNode);
            RegisterNode(notesNode);
            foreach (var n in _context.Notes.Values)
            {
                RegisterNode(n);
            }

            // Populate or clear per-individual detail sub-nodes based on the current
            // ShowDetails setting (families are resolved above, so spouse/child data is ready).
            RefreshDetails();
        }

        /// <summary>
        /// Rebuilds the per-individual, per-family, per-note, per-repository, and
        /// per-source detail sub-nodes when <see cref="ShowDetails"/> changes or a
        /// new tree is loaded. Detail nodes are lightweight display-only leaves
        /// (no stable key) so they do not participate in cross-tree sync or
        /// mismatch navigation.
        /// </summary>
        private void RefreshDetails()
        {
            if (_context?.Individuals == null) return;

            foreach (var person in _context.Individuals.Values)
            {
                person.Details.Clear();
                if (_showDetails)
                {
                    BuildDetails(person);
                }
            }

            foreach (var family in _context.Families.Values)
            {
                family.Details.Clear();
                if (_showDetails)
                {
                    BuildFamilyDetails(family);
                }
            }

            foreach (var note in _context.Notes.Values)
            {
                note.Details.Clear();
                if (_showDetails)
                {
                    BuildNoteDetails(note);
                }
            }

            foreach (var repo in _context.Repositories.Values)
            {
                repo.Details.Clear();
                if (_showDetails)
                {
                    BuildRepositoryDetails(repo);
                }
            }

            foreach (var source in _context.Sources.Values)
            {
                source.Details.Clear();
                if (_showDetails)
                {
                    BuildSourceDetails(source);
                }
            }
        }

        /// <summary>
        /// Resolves the records (Individual, Family, Source, Repository) that
        /// reference this note and appends them as detail sub-nodes, providing
        /// a reverse-lookup of which records a note is associated with.
        /// </summary>
        private void BuildNoteDetails(NoteNode note)
        {
            // Individuals that reference this note
            foreach (var person in _context.Individuals.Values
                .OrderBy(p => p.DisplayName)
                .ThenBy(p => p.Id))
            {
                if (person.NoteIds.Contains(note.Id))
                {
                    note.Details.Add(new NoteDetailNode
                    {
                        Role = "Individual",
                        DisplayName = $"Individual: {person.DisplayName}"
                    });
                }
            }

            // Families that reference this note
            foreach (var family in _context.Families.Values
                .OrderBy(f => f.DisplayName)
                .ThenBy(f => f.Id))
            {
                if (family.NoteIds.Contains(note.Id))
                {
                    note.Details.Add(new NoteDetailNode
                    {
                        Role = "Family",
                        DisplayName = $"Family: {family.DisplayName}"
                    });
                }
            }

            // Sources that reference this note
            foreach (var source in _context.Sources.Values
                .OrderBy(s => s.Title)
                .ThenBy(s => s.Id))
            {
                if (source.NoteIds.Contains(note.Id))
                {
                    note.Details.Add(new NoteDetailNode
                    {
                        Role = "Source",
                        DisplayName = $"Source: {source.Title}"
                    });
                }
            }

            // Repositories that reference this note
            foreach (var repo in _context.Repositories.Values
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id))
            {
                if (repo.NoteIds.Contains(note.Id))
                {
                    note.Details.Add(new NoteDetailNode
                    {
                        Role = "Repository",
                        DisplayName = $"Repository: {repo.Name}"
                    });
                }
            }
        }

        /// <summary>
        /// Resolves the sources that reference this repository (via their REPO
        /// cross-reference) and appends them as detail sub-nodes, providing a
        /// reverse-lookup of which sources are held by this repository.
        /// </summary>
        private void BuildRepositoryDetails(RepositoryNode repo)
        {
            // Sources that reference this repository
            foreach (var source in _context.Sources.Values
                .OrderBy(s => s.Title)
                .ThenBy(s => s.Id))
            {
                if (!string.IsNullOrEmpty(source.RepositoryId) &&
                    source.RepositoryId == repo.Id)
                {
                    repo.Details.Add(new RepositoryDetailNode
                    {
                        Role = "Source",
                        DisplayName = $"Source: {source.Title}"
                    });
                }
            }
        }

        /// <summary>
        /// Resolves the individuals and families that reference this source
        /// (either directly or via any of their events) and appends them as
        /// detail sub-nodes, providing a reverse-lookup of which records are
        /// proven by this source. When a source proves a specific event, the
        /// event is shown alongside the record (e.g., "Individual: John Smith —
        /// Birth").
        /// </summary>
        private void BuildSourceDetails(SourceNode source)
        {
            // Individuals that reference this source (directly or via their events)
            foreach (var person in _context.Individuals.Values
                .OrderBy(p => p.DisplayName)
                .ThenBy(p => p.Id))
            {
                // Direct (record-level) citation
                if (person.SourceIds.Contains(source.Id))
                {
                    source.Details.Add(new SourceDetailNode
                    {
                        Role = "Individual",
                        DisplayName = $"Individual: {person.DisplayName}"
                    });
                }

                // Event-level citations — one entry per event that cites this source
                foreach (var evt in person.Events.Where(e => e.SourceIds.Contains(source.Id) && !e.IsInternal))
                {
                    source.Details.Add(new SourceDetailNode
                    {
                        Role = "Individual",
                        DisplayName = $"Individual: {person.DisplayName} — {evt.DisplayName}"
                    });
                }
            }

            // Families that reference this source (directly or via their events)
            foreach (var family in _context.Families.Values
                .OrderBy(f => f.DisplayName)
                .ThenBy(f => f.Id))
            {
                // Direct (record-level) citation
                if (family.SourceIds.Contains(source.Id))
                {
                    source.Details.Add(new SourceDetailNode
                    {
                        Role = "Family",
                        DisplayName = $"Family: {family.DisplayName}"
                    });
                }

                // Event-level citations — one entry per event that cites this source
                foreach (var evt in family.Events.Where(e => e.SourceIds.Contains(source.Id) && !e.IsInternal))
                {
                    source.Details.Add(new SourceDetailNode
                    {
                        Role = "Family",
                        DisplayName = $"Family: {family.DisplayName} — {evt.DisplayName}"
                    });
                }
            }
        }

        /// <summary>
        /// Resolves the spouses, children, and events for a single family and
        /// appends them as detail sub-nodes. The husband and wife are shown as
        /// "Spouse" entries (each labeled with their role in the family), each
        /// child is shown as a "Child" entry, and each event is shown as an
        /// "Event" entry.
        /// </summary>
        private void BuildFamilyDetails(FamilyNode family)
        {
            // Husband (if resolved)
            if (family.Husband != null)
            {
                family.Details.Add(new FamilyDetailNode
                {
                    Role = "Spouse",
                    DisplayName = $"Husband: {family.Husband.DisplayName}"
                });
            }

            // Wife (if resolved)
            if (family.Wife != null)
            {
                family.Details.Add(new FamilyDetailNode
                {
                    Role = "Spouse",
                    DisplayName = $"Wife: {family.Wife.DisplayName}"
                });
            }

            // Children (resolved from their IDs)
            foreach (var childId in family.ChildrenIds)
            {
                if (_context.Individuals.TryGetValue(childId, out var child))
                {
                    family.Details.Add(new FamilyDetailNode
                    {
                        Role = "Child",
                        DisplayName = $"Child: {child.DisplayName}"
                    });
                }
            }

            // Events (marriage, divorce, etc.)
            foreach (var evt in family.Events)
            {
                if (evt.IsInternal) continue;
                family.Details.Add(new EventDetailNode
                {
                    Role = "Event",
                    DisplayName = $"Event: {evt.DisplayName}"
                });
            }
        }

        /// <summary>
        /// Resolves the spouses, children, notes, and events for a single
        /// individual and appends them as detail sub-nodes.
        /// </summary>
        private void BuildDetails(IndividualNode person)
        {
            // Spouses and children from the families in which this person is a spouse
            foreach (var family in _context.Families.Values)
            {
                bool isHusband = family.HusbandId == person.Id;
                bool isWife = family.WifeId == person.Id;
                if (!isHusband && !isWife) continue;

                var spouse = isHusband ? family.Wife : family.Husband;
                if (spouse != null)
                {
                    person.Details.Add(new IndividualDetailNode
                    {
                        Role = "Spouse",
                        DisplayName = $"Spouse: {spouse.DisplayName}"
                    });
                }

                foreach (var childId in family.ChildrenIds)
                {
                    if (_context.Individuals.TryGetValue(childId, out var child))
                    {
                        person.Details.Add(new IndividualDetailNode
                        {
                            Role = "Child",
                            DisplayName = $"Child: {child.DisplayName}"
                        });
                    }
                }
            }

            // Notes attached directly to this person
            foreach (var noteId in person.NoteIds)
            {
                if (_context.Notes.TryGetValue(noteId, out var note))
                {
                    person.Details.Add(new IndividualDetailNode
                    {
                        Role = "Note",
                        DisplayName = $"Note: {note.DisplayName}"
                    });
                }
            }

            // Events (birth, death, burial, baptism, census, residence, etc.)
            foreach (var evt in person.Events)
            {
                if (evt.IsInternal) continue;
                person.Details.Add(new EventDetailNode
                {
                    Role = "Event",
                    DisplayName = $"Event: {evt.DisplayName}"
                });
            }
        }

        /// <summary>
        /// Returns the stable key of the currently selected node, or null if none.
        /// </summary>
        public string? GetSelectedNodeKey()
        {
            foreach (var tn in _nodeMap.Values)
            {
                if (tn.IsSelected)
                {
                    return TreeNodeKeys.GetKey(tn);
                }
            }
            return null;
        }

        /// <summary>
        /// Selects the given node in this tree, expanding ancestors and scrolling
        /// it into view.
        /// </summary>
        public void SelectNode(object node)
        {
            if (node is not TreeNodeBase targetNode) return;

            // Clear selection on all other nodes first
            foreach (var other in _nodeMap.Values)
            {
                if (other.IsSelected && !ReferenceEquals(other, targetNode))
                {
                    other.IsSelected = false;
                }
            }

            // Select the target node
            targetNode.IsSelected = true;

            // Expand ancestors so the node is visible, then scroll to it
            var path = FindPath(node);
            if (path != null)
            {
                ExpandAncestors(path);
                ScrollToNodeRequested?.Invoke(this, new TreeSyncEventArgs
                {
                    Path = path,
                    SelectedSet = true,
                    IsSelectedValue = true
                });
            }
        }

        /// <summary>
        /// Assigns <see cref="IndividualNode.CollisionIndex"/> values to individuals
        /// that share a name and lack a birth date, so their stable identity keys
        /// remain unique. The order is deterministic (by record Id) so two exports
        /// of the same family assign matching indices.
        /// </summary>
        private static void AssignCollisionIndices(GedcomTreeContext context)
        {
            foreach (var group in context.Individuals.Values
                .Where(p => string.IsNullOrWhiteSpace(p.BirthDate))
                .GroupBy(p => TreeNodeKeys.NormalizeName(p.FullName))
                .Where(g => g.Count() > 1)
                .Select(g => g.OrderBy(p => p.Id, StringComparer.Ordinal).ToList()))
            {
                for (int i = 0; i < group.Count; i++)
                {
                    group[i].CollisionIndex = i + 1;
                }
            }
        }

        private void RegisterNode(object node)
        {
            if (node is TreeNodeBase tn && !_nodeMap.ContainsKey(node))
            {
                _nodeMap[node] = tn;
                tn.PropertyChanged += OnNodePropertyChanged;
            }
        }

        private void UnsubscribeAllNodes()
        {
            foreach (var tn in _nodeMap.Values)
            {
                tn.PropertyChanged -= OnNodePropertyChanged;
            }
            _nodeMap.Clear();
        }

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not TreeNodeBase tn) return;
            if (e.PropertyName != nameof(TreeNodeBase.IsExpanded) &&
                e.PropertyName != nameof(TreeNodeBase.IsSelected)) return;

            var path = FindPath(tn);
            if (path == null) return;

            SyncRequested?.Invoke(this, new TreeSyncEventArgs
            {
                Path = path,
                ExpandedSet = e.PropertyName == nameof(TreeNodeBase.IsExpanded),
                IsExpandedValue = tn.IsExpanded,
                SelectedSet = e.PropertyName == nameof(TreeNodeBase.IsSelected),
                IsSelectedValue = tn.IsSelected
            });
        }

        /// <summary>
        /// Applies a synchronization request from the other tree.
        /// Finds the equivalent node by path and sets its state.
        /// </summary>
        public void ApplySync(TreeSyncEventArgs e)
        {
            var node = FindNodeByPath(e.Path);
            if (node is not TreeNodeBase tn) return;

            // When expanding or selecting a node, expand its ancestors first so the
            // target becomes visible in the receiving tree (Avalonia realizes item
            // containers lazily for collapsed parents).
            if ((e.ExpandedSet && e.IsExpandedValue) || (e.SelectedSet && e.IsSelectedValue))
            {
                ExpandAncestors(e.Path);
            }

            // Guard against re-entrancy: if the node already has the target
            // state, setting it again would fire PropertyChanged and loop.
            if (e.ExpandedSet && tn.IsExpanded != e.IsExpandedValue)
            {
                tn.IsExpanded = e.IsExpandedValue;
            }

            if (e.SelectedSet && tn.IsSelected != e.IsSelectedValue)
            {
                // Clear selection on all other nodes first
                foreach (var other in _nodeMap.Values)
                {
                    if (other.IsSelected && !ReferenceEquals(other, tn))
                    {
                        other.IsSelected = false;
                    }
                }
                tn.IsSelected = e.IsSelectedValue;
            }

            // Notify the view layer to scroll the target node into view.
            // Only scroll when the node is being selected or expanded, not
            // when it is being deselected or collapsed (which would cause
            // the receiving tree to jump to the wrong node when the user
            // clicks a new node).
            bool shouldScroll =
                (e.SelectedSet && e.IsSelectedValue) ||
                (e.ExpandedSet && e.IsExpandedValue);

            if (shouldScroll)
            {
                ScrollToNodeRequested?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Expands every node above the target in the given path so that the
        /// target is realized and visible in the tree.
        /// </summary>
        private void ExpandAncestors(List<string> path)
        {
            if (path.Count < 2) return;

            for (int i = 1; i < path.Count; i++)
            {
                var ancestorPath = path.Take(i).ToList();
                if (FindNodeByPath(ancestorPath) is TreeNodeBase ancestor &&
                    !ancestor.IsExpanded)
                {
                    ancestor.IsExpanded = true;
                }
            }
        }


        /// <summary>
        /// Builds a stable path of keys from the root to the given node.
        /// Returns null if the node is not part of this tree.
        /// </summary>
        private List<string>? FindPath(object target)
        {
            foreach (var group in TreeNodes)
            {
                // Check if the target IS the top-level group node itself
                if (ReferenceEquals(group, target))
                {
                    return new List<string> { group.Key };
                }

                var path = new List<string> { group.Key };
                if (FindPathRecursive(group.Children, target, path))
                {
                    return path;
                }
            }
            return null;
        }

        private bool FindPathRecursive(IEnumerable<object> children, object target, List<string> path)
        {
            foreach (var child in children)
            {
                var key = TreeNodeKeys.GetKey(child);
                if (key == null) continue;

                path.Add(key);

                // The key must be added BEFORE the reference check so the returned
                // path includes the matched node itself, not just its ancestors.
                if (ReferenceEquals(child, target))
                {
                    return true;
                }

                var subChildren = TreeNodeKeys.GetChildren(child);
                if (subChildren != null && FindPathRecursive(subChildren, target, path))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        /// <summary>
        /// Finds a node by its path of keys. Returns null if any segment is missing.
        /// </summary>
        private object? FindNodeByPath(List<string> path)
        {
            if (path.Count == 0) return null;

            var group = TreeNodes.FirstOrDefault(g => g.Key == path[0]);
            if (group == null) return null;

            object current = group;
            for (int i = 1; i < path.Count; i++)
            {
                var children = TreeNodeKeys.GetChildren(current);
                if (children == null) return null;

                var next = children.FirstOrDefault(c => TreeNodeKeys.GetKey(c) == path[i]);
                if (next == null) return null;
                current = next;
            }
            return current;
        }
    }
}
