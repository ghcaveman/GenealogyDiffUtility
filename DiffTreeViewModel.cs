using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GenealogyDiffUtility
{
    internal class DiffTreeViewModel : ViewModelBase
    {
        private GedcomTreeContext _context = new();
        private string _fileName = "No File Loaded";
        private string _rawFileText = string.Empty;

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
        public string CharacterEncoding => _context.Header.CharacterEncoding;

        // The unified hierarchical collection your TreeView control will bind onto
        public ObservableCollection<TreeGroupNode> TreeNodes { get; } = new();

        public void LoadTree(GedcomTreeContext context, string fileName, string rawText)
        {
            _context = context;
            FileName = fileName;
            RawFileText = rawText;

            OnPropertyChanged(nameof(SourceSoftware));
            OnPropertyChanged(nameof(GedcomVersion));
            OnPropertyChanged(nameof(CharacterEncoding));

            TreeNodes.Clear();

            // 1. Header Node
            TreeNodes.Add(new TreeGroupNode
            {
                Name = "Header",
                Children = new List<object> { _context.Header }
            });

            // 2. Individuals Node (Grouped dynamically by Surname)
            var surnameGroups = _context.Individuals.Values
                .GroupBy(p => string.IsNullOrWhiteSpace(p.LastName) ? "Unknown" : p.LastName)
                .OrderBy(g => g.Key)
                .Select(g => new SurnameGroupNode { Surname = g.Key, People = g.ToList() })
                .Cast<object>()
                .ToList();

            TreeNodes.Add(new TreeGroupNode
            {
                Name = $"Individuals ({_context.Individuals.Count})",
                Children = surnameGroups
            });

            // 3. Families Node
            TreeNodes.Add(new TreeGroupNode
            {
                Name = $"Families ({_context.Families.Count})",
                Children = _context.Families.Values.Cast<object>().ToList()
            });

            // 4. Sources Node
            TreeNodes.Add(new TreeGroupNode
            {
                Name = $"Sources ({_context.Sources.Count})",
                Children = _context.Sources.Values.Cast<object>().ToList()
            });
        }
    }
}
