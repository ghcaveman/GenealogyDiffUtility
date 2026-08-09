namespace GenealogyDiffUtility
{
    internal class MainWindowViewModel : ViewModelBase
    {
        private readonly TreeSyncService _syncService = new();
        private readonly MismatchNavigator _mismatchNavigator = new();

        public DiffTreeViewModel LeftTree { get; } = new();
        public DiffTreeViewModel RightTree { get; } = new();

        public MainWindowViewModel()
        {
            // Initial placeholder labels
            LeftTree.FileName = "Select Left File...";
            RightTree.FileName = "Select Right File...";

            // Connect the two trees so navigation/expansion stays in sync
            _syncService.Attach(LeftTree, RightTree);

            // Connect the mismatch navigator for jumping between differences
            _mismatchNavigator.Attach(LeftTree, RightTree);

            // Cross-link the trees so the Diff tab can compare them
            LeftTree.OtherTree = RightTree;
            RightTree.OtherTree = LeftTree;
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
