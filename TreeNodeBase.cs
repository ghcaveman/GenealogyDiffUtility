using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Base class for all tree node types. Provides bindable
    /// IsExpanded and IsSelected properties used for two-way
    /// synchronization between the left and right trees.
    /// </summary>
    internal abstract class TreeNodeBase : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}