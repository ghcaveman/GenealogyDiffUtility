using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;

namespace GenealogyDiffUtility
{
    public partial class GenealogyControl : UserControl
    {
        private DiffTreeViewModel? _currentViewModel;

        public GenealogyControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            // Unsubscribe from the previous ViewModel, if any
            if (_currentViewModel != null)
            {
                _currentViewModel.ScrollToNodeRequested -= OnScrollToNodeRequested;
                _currentViewModel = null;
            }

            if (DataContext is DiffTreeViewModel vm)
            {
                _currentViewModel = vm;
                vm.ScrollToNodeRequested += OnScrollToNodeRequested;
            }
        }

        private void OnScrollToNodeRequested(object? sender, TreeSyncEventArgs e)
        {
            // Use the dispatcher to give the tree time to realize item containers
            // after the property changes from ApplySync have been processed.
            Dispatcher.UIThread.Post(() => ScrollToNode(e.Path), DispatcherPriority.Background);
        }

        /// <summary>
        /// Walks the visual tree of TreeViewItem containers along the given path
        /// and calls BringIntoView on the deepest container.
        /// </summary>
        private void ScrollToNode(List<string> path)
        {
            if (path.Count == 0 || TreeView is null) return;

            // Start at the TreeView's top-level item generator
            ItemsControl? currentHost = TreeView;
            TreeViewItem? currentContainer = null;

            for (int i = 0; i < path.Count; i++)
            {
                if (currentHost is null) return;

                // Find the item whose key matches this path segment
                object? matchedItem = null;
                foreach (var item in currentHost.Items)
                {
                    var key = TreeNodeKeys.GetKey(item);
                    if (key == path[i])
                    {
                        matchedItem = item;
                        break;
                    }
                }

                if (matchedItem == null) return;

                // Get the container for that item by index
                // (Avalonia's ItemsControl provides ContainerFromIndex directly)
                int index = currentHost.Items.IndexOf(matchedItem);
                if (index < 0) return;

                currentContainer = currentHost.ContainerFromIndex(index) as TreeViewItem;
                if (currentContainer == null) return;

                // For the next level, the TreeViewItem becomes the host
                currentHost = currentContainer;
            }

            // currentContainer is the TreeViewItem for the target node
            currentContainer?.BringIntoView();
        }

        private void OnNavigateUpClick(object? sender, RoutedEventArgs e)
        {
            Navigate(-1);
        }

        private void OnNavigateDownClick(object? sender, RoutedEventArgs e)
        {
            Navigate(1);
        }

        private void Navigate(int direction)
        {
            // Find the parent window's ViewModel (MainWindowViewModel)
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.DataContext is MainWindowViewModel mainVm)
            {
                if (direction < 0)
                    mainVm.NavigateToPreviousMismatch();
                else
                    mainVm.NavigateToNextMismatch();
            }
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