using System.Collections.Generic;

namespace GenealogyDiffUtility
{
    internal class MainWindowViewModel : ViewModelBase
    {
        private readonly TreeSyncService _syncService = new();
        private readonly MismatchNavigator _mismatchNavigator = new();
        private bool _showDetails = true;
        private string _diffSummaryText = NoComparisonText;

        private const string NoComparisonText = "Load two files to see the differences.";

        public DiffTreeViewModel LeftTree { get; } = new();
        public DiffTreeViewModel RightTree { get; } = new();

        /// <summary>
        /// The single Details toggle in the top bar. Applies to both trees so the
        /// left and right sides always show the same level of detail.
        /// </summary>
        public bool ShowDetails
        {
            get => _showDetails;
            set
            {
                if (RaiseAndSetIfChanged(ref _showDetails, value))
                {
                    LeftTree.ShowDetails = value;
                    RightTree.ShowDetails = value;
                }
            }
        }

        /// <summary>
        /// Difference counts shown in the top bar, e.g.
        /// "Differences - Individuals: 5 (12 details) · Families: 2 (3 details) · Sources: 1 · Repositories: 0".
        /// </summary>
        public string DiffSummaryText
        {
            get => _diffSummaryText;
            private set => RaiseAndSetIfChanged(ref _diffSummaryText, value);
        }

        public MainWindowViewModel()
        {
            // Which pane each tree is in, for the Diff report's wording
            LeftTree.SideName = "left";
            RightTree.SideName = "right";

            // Initial placeholder labels
            LeftTree.FileName = "No file selected...";
            RightTree.FileName = "No file selected...";

            // Connect the two trees so navigation/expansion stays in sync
            _syncService.Attach(LeftTree, RightTree);

            // Connect the mismatch navigator for jumping between differences
            _mismatchNavigator.Attach(LeftTree, RightTree);

            // Cross-link the trees so the Diff tab can compare them
            LeftTree.OtherTree = RightTree;
            RightTree.OtherTree = LeftTree;

            // Details are on by default for both trees.
            LeftTree.ShowDetails = _showDetails;
            RightTree.ShowDetails = _showDetails;

            // The left tree's comparison (left against right) drives the top-bar counts.
            LeftTree.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DiffTreeViewModel.DiffSummary))
                    DiffSummaryText = FormatSummary(LeftTree.DiffSummary);
            };
        }

        private static string FormatSummary(DiffSummary? summary)
        {
            if (summary == null) return NoComparisonText;
            if (summary.RecordDifferences == 0 && summary.DetailDifferences == 0)
                return summary.IncludesDetails ? "No differences." : "No record differences (turn on Details to compare events and sources).";

            var parts = new List<string>();
            foreach (var c in summary.Categories)
            {
                bool hasDetails = summary.IncludesDetails && (c.Name == "Individuals" || c.Name == "Families");
                parts.Add(hasDetails
                    ? $"{c.Name}: {c.RecordDifferences} ({c.DetailDifferences} details)"
                    : $"{c.Name}: {c.RecordDifferences}");
            }
            return "Differences - " + string.Join(" · ", parts);
        }

        /// <summary>
        /// Navigates to the next mismatch (difference) between the two trees.
        /// </summary>
        public void NavigateToNextMismatch()
        {
            // Use the left tree as the source for navigation
            _mismatchNavigator.NavigateToNextMismatch(LeftTree);
        }

        /// <summary>
        /// Navigates to the previous mismatch (difference) between the two trees.
        /// </summary>
        public void NavigateToPreviousMismatch()
        {
            // Use the left tree as the source for navigation
            _mismatchNavigator.NavigateToPreviousMismatch(LeftTree);
        }
    }
}
