using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// View model for the individual edit dialog. Holds editable copies of an
    /// individual's data and looks up related spouses and children by searching
    /// the family nodes in the GEDCOM tree context.
    /// </summary>
    internal class IndividualEditViewModel : ViewModelBase
    {
        private readonly IndividualNode _node;
        private readonly GedcomTreeContext _context;

        private string _fullName = string.Empty;
        private string _lastName = string.Empty;
        private string _gender = string.Empty;
        private string _birthDate = string.Empty;
        private string _birthPlace = string.Empty;
        private string _deathDate = string.Empty;
        private string _deathPlace = string.Empty;

        public IndividualEditViewModel(IndividualNode node, GedcomTreeContext context)
        {
            _node = node;
            _context = context;

            // Copy editable data from the node so edits can be cancelled
            FullName = node.FullName;
            LastName = node.LastName;
            Gender = node.Gender;
            BirthDate = node.BirthDate;
            BirthPlace = node.BirthPlace;
            DeathDate = node.DeathDate;
            DeathPlace = node.DeathPlace;

            // Look up spouses and children by searching family nodes
            Spouses = new ObservableCollection<SpouseInfo>(LookupSpouses(node, context));
            Children = new ObservableCollection<ChildInfo>(LookupChildren(node, context));

            // Resolve source and note references for display
            Sources = new ObservableCollection<SourceNode>(
                node.SourceIds
                    .Where(id => context.Sources.TryGetValue(id, out _))
                    .Select(id => context.Sources[id]));

            Notes = new ObservableCollection<NoteNode>(
                node.NoteIds
                    .Where(id => context.Notes.TryGetValue(id, out _))
                    .Select(id => context.Notes[id]));
        }

        public string Title => $"Edit Individual: {_node.FullName}";

        // --- Editable fields ---

        public string FullName
        {
            get => _fullName;
            set => RaiseAndSetIfChanged(ref _fullName, value);
        }

        public string LastName
        {
            get => _lastName;
            set => RaiseAndSetIfChanged(ref _lastName, value);
        }

        public string Gender
        {
            get => _gender;
            set => RaiseAndSetIfChanged(ref _gender, value);
        }

        public string BirthDate
        {
            get => _birthDate;
            set => RaiseAndSetIfChanged(ref _birthDate, value);
        }

        public string BirthPlace
        {
            get => _birthPlace;
            set => RaiseAndSetIfChanged(ref _birthPlace, value);
        }

        public string DeathDate
        {
            get => _deathDate;
            set => RaiseAndSetIfChanged(ref _deathDate, value);
        }

        public string DeathPlace
        {
            get => _deathPlace;
            set => RaiseAndSetIfChanged(ref _deathPlace, value);
        }

        // --- Lookup collections (read-only) ---

        public ObservableCollection<SpouseInfo> Spouses { get; }
        public ObservableCollection<ChildInfo> Children { get; }
        public ObservableCollection<SourceNode> Sources { get; }
        public ObservableCollection<NoteNode> Notes { get; }

        /// <summary>
        /// Writes the edited values back to the source individual node and
        /// notifies the tree view that the display name has changed.
        /// </summary>
        public void CommitChanges()
        {
            _node.FullName = FullName;
            _node.LastName = LastName;
            _node.Gender = Gender;
            _node.BirthDate = BirthDate;
            _node.BirthPlace = BirthPlace;
            _node.DeathDate = DeathDate;
            _node.DeathPlace = DeathPlace;
            _node.RefreshDisplay();
        }

        /// <summary>
        /// Searches every family node for ones where the given individual is a
        /// spouse (husband or wife), and returns the other spouse paired with
        /// the family they share.
        /// </summary>
        private static IEnumerable<SpouseInfo> LookupSpouses(IndividualNode person, GedcomTreeContext context)
        {
            var spouses = new List<SpouseInfo>();

            foreach (var family in context.Families.Values)
            {
                IndividualNode? spouse = null;

                if (family.HusbandId == person.Id &&
                    !string.IsNullOrEmpty(family.WifeId) &&
                    context.Individuals.TryGetValue(family.WifeId, out var wife))
                {
                    spouse = wife;
                }
                else if (family.WifeId == person.Id &&
                         !string.IsNullOrEmpty(family.HusbandId) &&
                         context.Individuals.TryGetValue(family.HusbandId, out var husband))
                {
                    spouse = husband;
                }

                if (spouse != null)
                {
                    spouses.Add(new SpouseInfo { Spouse = spouse, Family = family });
                }
            }

            return spouses;
        }

        /// <summary>
        /// Searches every family node for ones where the given individual is a
        /// parent (husband or wife), and returns the children in those families
        /// paired with the family they belong to.
        /// </summary>
        private static IEnumerable<ChildInfo> LookupChildren(IndividualNode person, GedcomTreeContext context)
        {
            var children = new List<ChildInfo>();
            var seenChildIds = new HashSet<string>();

            foreach (var family in context.Families.Values)
            {
                if (family.HusbandId != person.Id && family.WifeId != person.Id)
                    continue;

                foreach (var childId in family.ChildrenIds)
                {
                    if (childId == person.Id || seenChildIds.Contains(childId))
                        continue;

                    if (context.Individuals.TryGetValue(childId, out var child))
                    {
                        seenChildIds.Add(childId);
                        children.Add(new ChildInfo { Child = child, Family = family });
                    }
                }
            }

            return children;
        }
    }

    /// <summary>
    /// Pairs a spouse individual with the family they share with the edited person.
    /// </summary>
    internal class SpouseInfo
    {
        public IndividualNode? Spouse { get; set; }
        public FamilyNode? Family { get; set; }

        public string SpouseName => Spouse?.DisplayName ?? "(unresolved spouse)";
        public string FamilyDisplay => Family?.DisplayName ?? string.Empty;
    }

    /// <summary>
    /// Pairs a child individual with the family they belong to.
    /// </summary>
    internal class ChildInfo
    {
        public IndividualNode? Child { get; set; }
        public FamilyNode? Family { get; set; }

        public string ChildName => Child?.DisplayName ?? "(unresolved child)";
        public string FamilyDisplay => Family?.DisplayName ?? string.Empty;
    }
}
