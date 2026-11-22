using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Lets the user choose what to print. Closes with a <see cref="PrintOptions"/>
    /// when Print is pressed, or null when cancelled.
    /// </summary>
    public partial class PrintDialog : Window
    {
        public PrintDialog()
        {
            InitializeComponent();
        }

        /// <summary>Creates the dialog with Include details preset to the main window's setting.</summary>
        internal PrintDialog(bool includeDetails) : this()
        {
            DetailsCheckBox.IsChecked = includeDetails;
        }

        private void OnContentChanged(object? sender, RoutedEventArgs e)
        {
            // The row choice applies to tree printouts, not the diff report.
            if (RowsPanel != null && DifferencesRadio != null)
            {
                RowsPanel.IsEnabled = DifferencesRadio.IsChecked != true;
            }
        }

        private void OnPrintClick(object? sender, RoutedEventArgs e)
        {
            var content = DifferencesRadio.IsChecked == true ? PrintContent.Differences
                        : LeftTreeRadio.IsChecked == true ? PrintContent.LeftTree
                        : RightTreeRadio.IsChecked == true ? PrintContent.RightTree
                        : PrintContent.BothTrees;

            Close(new PrintOptions
            {
                Content = content,
                IncludeDetails = DetailsCheckBox.IsChecked == true,
                ExpandedRowsOnly = ExpandedRowsRadio.IsChecked == true
            });
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
