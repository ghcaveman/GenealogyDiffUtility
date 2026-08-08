using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GenealogyDiffUtility
{
    public partial class IndividualEditDialog : Window
    {
        public IndividualEditDialog()
        {
            InitializeComponent();
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is IndividualEditViewModel vm)
            {
                vm.CommitChanges();
            }
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
