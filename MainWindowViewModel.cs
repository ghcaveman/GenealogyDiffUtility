namespace GenealogyDiffUtility
{
    internal class MainWindowViewModel : ViewModelBase
    {
        public DiffTreeViewModel LeftTree { get; } = new();
        public DiffTreeViewModel RightTree { get; } = new();

        public MainWindowViewModel()
        {
            // Initial placeholder labels
            LeftTree.FileName = "Select Left File...";
            RightTree.FileName = "Select Right File...";
        }
    }
}
