using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GenealogyDiffUtility
{
    public partial class GenealogyControl : UserControl
    {
        private DiffTreeViewModel? _currentViewModel;
        private bool _hideVerticalScrollBar;

        /// <summary>
        /// Raised once the TreeView's internal ScrollViewer is available,
        /// so the window can synchronise scrolling between the two trees.
        /// </summary>
        public event EventHandler<ScrollViewer>? TreeScrollViewerReady;

        /// <summary>
        /// Which pane this control is ("Left" or "Right"); shown in the
        /// file chooser title so it's clear which side is being loaded.
        /// </summary>
        public string? Side { get; set; }

        /// <summary>The TreeView's internal ScrollViewer, once its template is applied.</summary>
        public ScrollViewer? TreeScrollViewer { get; private set; }

        /// <summary>
        /// When true, the tree's vertical scrollbar is hidden (scrolling still
        /// works via the mouse wheel and keyboard). Used so that only one of the
        /// two side-by-side trees shows a scrollbar.
        /// </summary>
        public bool HideVerticalScrollBar
        {
            get => _hideVerticalScrollBar;
            set
            {
                _hideVerticalScrollBar = value;
                ApplyScrollBarVisibility();
            }
        }

        public GenealogyControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            TreeView.TemplateApplied += OnTreeViewTemplateApplied;
            ApplyScrollBarVisibility();
        }

        private void ApplyScrollBarVisibility()
        {
            if (TreeView is null) return;
            ScrollViewer.SetVerticalScrollBarVisibility(TreeView,
                _hideVerticalScrollBar ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto);
        }

        private void OnTreeViewTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            var sv = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer")
                     ?? TreeView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (sv == null) return;

            TreeScrollViewer = sv;
            TreeScrollViewerReady?.Invoke(this, sv);
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


        private void OnTreeKeyDown(object? sender, KeyEventArgs e)
        {
            // Ctrl+C (Cmd+C on macOS) copies every selected tree row.
            if (e.Key == Key.C &&
                (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            {
                _ = CopySelectionToClipboardAsync();
                e.Handled = true;
            }
        }

        private void OnCopyClick(object? sender, RoutedEventArgs e)
        {
            _ = CopySelectionToClipboardAsync();
        }

        /// <summary>
        /// Copies the text of all selected tree rows to the clipboard, one per
        /// line, in the order they appear in the tree and indented by depth.
        /// </summary>
        private async System.Threading.Tasks.Task CopySelectionToClipboardAsync()
        {
            if (TreeView is null) return;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;

            var selected = TreeView.SelectedItems?.Cast<object>().ToList() ?? new List<object>();
            if (selected.Count == 0) return;

            var lines = new List<string>();
            var copied = new HashSet<object>(ReferenceEqualityComparer.Instance);

            // Walk the realized rows top to bottom so the copied text keeps tree order.
            var containers = TreeView.GetVisualDescendants().OfType<TreeViewItem>().ToList();
            int minLevel = containers.Where(c => c.IsSelected).Select(c => c.Level).DefaultIfEmpty(0).Min();

            foreach (var container in containers)
            {
                if (!container.IsSelected || container.DataContext is not { } item) continue;
                if (!copied.Add(item)) continue;
                AppendNodeText(lines, item, new string(' ', (container.Level - minLevel) * 2));
            }

            // Anything selected but not currently realized (e.g. a collapsed parent).
            foreach (var item in selected)
            {
                if (copied.Add(item)) AppendNodeText(lines, item, string.Empty);
            }

            if (lines.Count > 0)
            {
                await clipboard.SetTextAsync(string.Join(Environment.NewLine, lines));
            }
        }

        private static void AppendNodeText(List<string> lines, object item, string indent)
        {
            // Keep multi-line values (e.g. the header, wrapped notes) aligned under the indent.
            foreach (var line in TreeNodeText.GetLines(item))
            {
                lines.Add(indent + line);
            }
        }

        private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not DiffTreeViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = string.IsNullOrEmpty(Side) ? "Select GEDCOM File" : $"Select {Side} GEDCOM File",
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
                    string rawText = await File.ReadAllTextAsync(filePath, GedcomParser.DetectEncoding(filePath));
                    GedcomTreeContext context = GedcomParser.Parse(filePath);
                    vm.LoadTree(context, Path.GetFileName(filePath), rawText);
                }
                catch (Exception ex)
                {
                    vm.RawFileText = $"Failed to load GEDCOM file:\n{ex.GetType().Name}: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// Opens the individual edit dialog when the user double-clicks an
        /// individual node in the TreeView.
        /// </summary>
        private async void OnIndividualDoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBlock textBlock) return;
            if (textBlock.DataContext is not IndividualNode individual) return;
            if (_currentViewModel == null) return;

            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null) return;

            var editVm = new IndividualEditViewModel(individual, _currentViewModel.Context);
            var dialog = new IndividualEditDialog
            {
                DataContext = editVm
            };

            await dialog.ShowDialog(parentWindow);
        }
    }
}
