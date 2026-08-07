namespace GenealogyDiffUtility
{
    internal class MainWindowViewModel : ViewModelBase
    {
        private readonly TreeSyncService _syncService = new();

        public DiffTreeViewModel LeftTree { get; } = new();
        public DiffTreeViewModel RightTree { get; } = new();

        public MainWindowViewModel()
        {
            // Initial placeholder labels
            LeftTree.FileName = "Select Left File...";
            RightTree.FileName = "Select Right File...";

            // Connect the two trees so navigation/expansion stays in sync
            _syncService.Attach(LeftTree, RightTree);
        }
    }
}