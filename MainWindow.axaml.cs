using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace GenealogyDiffUtility
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OnBrowseLeftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await BrowseForFileAsync(LeftFileTextBox, LeftContentTextBlock);
        }

        private async void OnBrowseRightClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await BrowseForFileAsync(RightFileTextBox, RightContentTextBlock);
        }

        private async Task BrowseForFileAsync(TextBox fileNameTextBox, TextBlock contentTextBlock)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a file",
                AllowMultiple = false
            });

            if (files.Count == 0)
                return;

            var file = files[0];
            fileNameTextBox.Text = file.TryGetLocalPath();

            try
            {
                contentTextBlock.Text = await File.ReadAllTextAsync(fileNameTextBox.Text!);
            }
            catch (Exception ex)
            {
                contentTextBlock.Text = $"Failed to read the file:\n{ex.Message}";
            }
        }
    }
}