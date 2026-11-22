using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace GenealogyDiffUtility
{
    public partial class MainWindow : Window
    {
        private ScrollViewer? _leftScroll;
        private ScrollViewer? _rightScroll;
        private bool _syncingScroll;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();

            LeftControl.TreeScrollViewerReady += (_, sv) => AttachScroll(ref _leftScroll, sv);
            RightControl.TreeScrollViewerReady += (_, sv) => AttachScroll(ref _rightScroll, sv);
        }

        private void OnNavigateUpClick(object? sender, RoutedEventArgs e)
        {
            (DataContext as MainWindowViewModel)?.NavigateToPreviousMismatch();
        }

        private void OnNavigateDownClick(object? sender, RoutedEventArgs e)
        {
            (DataContext as MainWindowViewModel)?.NavigateToNextMismatch();
        }

        private async void OnPrintClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var dialog = new PrintDialog(vm.ShowDetails);
            var options = await dialog.ShowDialog<PrintOptions?>(this);
            if (options == null) return;

            try
            {
                TreePrinter.Print(vm, options);
            }
            catch (Exception ex)
            {
                var error = new Window
                {
                    Title = "Print",
                    Width = 420,
                    SizeToContent = SizeToContent.Height,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new TextBlock
                    {
                        Text = $"Couldn't open the print page:\n{ex.Message}",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Margin = new Thickness(20)
                    }
                };
                await error.ShowDialog(this);
            }
        }

        private void AttachScroll(ref ScrollViewer? slot, ScrollViewer sv)
        {
            if (slot != null) slot.ScrollChanged -= OnTreeScrollChanged;
            slot = sv;
            slot.ScrollChanged += OnTreeScrollChanged;
        }

        /// <summary>
        /// Keeps the two trees' vertical scroll positions in step, so the
        /// right tree's scrollbar (and the mouse wheel over either tree)
        /// moves both. Horizontal scrolling stays independent.
        /// </summary>
        private void OnTreeScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_syncingScroll || _leftScroll == null || _rightScroll == null) return;
            if (e.OffsetDelta.Y == 0) return;

            var source = ReferenceEquals(sender, _leftScroll) ? _leftScroll : _rightScroll;
            var target = ReferenceEquals(source, _leftScroll) ? _rightScroll : _leftScroll;

            _syncingScroll = true;
            try
            {
                target.Offset = new Vector(target.Offset.X, source.Offset.Y);
            }
            finally
            {
                _syncingScroll = false;
            }
        }
    }
}
