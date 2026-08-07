using System.Collections.ObjectModel;

namespace GenealogyDiffUtility
{
    internal class DiffTreeViewModel : ViewModelBase
    {
        private GedcomTreeContext _context = new();
        private string _fileName = "No File Loaded";
        private string _rawFileText = string.Empty; // Added backing field

        public string FileName
        {
            get => _fileName;
            set => RaiseAndSetIfChanged(ref _fileName, value);
        }

        // Added missing property for the Text tab text-box binding
        public string RawFileText
        {
            get => _rawFileText;
            set => RaiseAndSetIfChanged(ref _rawFileText, value);
        }

        public string SourceSoftware => _context.Header.SourceSoftware;
        public string GedcomVersion => _context.Header.GedcomVersion;
        public string CharacterEncoding => _context.Header.CharacterEncoding;

        public ObservableCollection<GedcomHeader> HeaderNodes { get; } = new();
        public ObservableCollection<IndividualNode> IndividualNodes { get; } = new();
        public ObservableCollection<FamilyNode> FamilyNodes { get; } = new();
        public ObservableCollection<SourceNode> SourceNodes { get; } = new();
        public ObservableCollection<string> RepositoryNodes { get; } = new();

        // Updated signature to accept 3 arguments
        public void LoadTree(GedcomTreeContext context, string fileName, string rawText)
        {
            _context = context;
            FileName = fileName;
            RawFileText = rawText; // Sets the text tab display string

            OnPropertyChanged(nameof(SourceSoftware));
            OnPropertyChanged(nameof(GedcomVersion));
            OnPropertyChanged(nameof(CharacterEncoding));

            HeaderNodes.Clear();
            HeaderNodes.Add(_context.Header);

            IndividualNodes.Clear();
            foreach (var ind in _context.Individuals.Values)
                IndividualNodes.Add(ind);

            FamilyNodes.Clear();
            foreach (var fam in _context.Families.Values)
                FamilyNodes.Add(fam);

            SourceNodes.Clear();
            foreach (var src in _context.Sources.Values)
                SourceNodes.Add(src);

            RepositoryNodes.Clear();
        }
    }
}
