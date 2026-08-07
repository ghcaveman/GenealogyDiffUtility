using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GenealogyDiffUtility
{
    public partial class GenealogyControl : UserControl
    {
        public GenealogyControl()
        {
            InitializeComponent();
        }

        private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        {
            // Safely verify the current data context matches our targeted ViewModel type
            if (DataContext is not DiffTreeViewModel vm) return;

            // Fetch the visual window context required to trigger file pickers safely
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select GEDCOM File",
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                string? filePath = files[0].TryGetLocalPath();
                if (string.IsNullOrEmpty(filePath)) return;

                try
                {
                    // Read the file context raw for our Text View Tab
                    string rawText = await File.ReadAllTextAsync(filePath);

                    // Run the file parser loop structure
                    GedcomTreeContext context = GedcomParser.Parse(filePath);

                    // Directly refresh our data model lists inside the view model
                    vm.LoadTree(context, Path.GetFileName(filePath), rawText);
                }
                catch (Exception ex)
                {
                    vm.RawFileText = $"Failed to parse GEDCOM file:\n{ex.Message}";
                }
            }
        }
    }
}
