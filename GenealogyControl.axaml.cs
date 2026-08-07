using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.IO;

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
            if (DataContext is not DiffTreeViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select GEDCOM File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("GEDCOM Files") { Patterns = new[] { "*.ged" } }
                }
            });

            if (files.Count > 0)
            {
                string? filePath = files[0].TryGetLocalPath();
                if (string.IsNullOrEmpty(filePath)) return;

                try
                {
                    string rawText = await File.ReadAllTextAsync(filePath);
                    GedcomTreeContext context = GedcomParser.Parse(filePath);
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