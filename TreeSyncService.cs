using System;
using System.Collections.Generic;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Mediates synchronization between the left and right tree views.
    /// When one tree selects or expands a node, the other tree follows
    /// to the equivalent node (matched by path) if it exists.
    /// </summary>
    internal class TreeSyncService
    {
        private DiffTreeViewModel? _left;
        private DiffTreeViewModel? _right;
        private bool _isSyncing;

        public void Attach(DiffTreeViewModel left, DiffTreeViewModel right)
        {
            _left = left;
            _right = right;

            _left.SyncRequested += OnSyncRequested;
            _right.SyncRequested += OnSyncRequested;
        }

        public void Detach()
        {
            if (_left != null) _left.SyncRequested -= OnSyncRequested;
            if (_right != null) _right.SyncRequested -= OnSyncRequested;
            _left = null;
            _right = null;
        }

        private void OnSyncRequested(object? sender, TreeSyncEventArgs e)
        {
            if (_isSyncing) return;

            _isSyncing = true;
            try
            {
                var target = ReferenceEquals(sender, _left) ? _right : _left;
                target?.ApplySync(e);
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    /// <summary>
    /// Describes a synchronization action to apply to the other tree.
    /// </summary>
    internal class TreeSyncEventArgs : EventArgs
    {
        public List<string> Path { get; set; } = new();

        /// <summary>True when the expansion state changed (expand or collapse).</summary>
        public bool ExpandedSet { get; set; }

        /// <summary>The new expansion value when <see cref="ExpandedSet"/> is true.</summary>
        public bool IsExpandedValue { get; set; }

        /// <summary>True when the selection state changed (selected or deselected).</summary>
        public bool SelectedSet { get; set; }

        /// <summary>The new selection value when <see cref="SelectedSet"/> is true.</summary>
        public bool IsSelectedValue { get; set; }

        /// <summary>
        /// True when the node was selected while other nodes in the source tree
        /// were already selected (Ctrl/Shift multi-select), so the receiving tree
        /// should add to its selection rather than replace it.
        /// </summary>
        public bool SelectionIsAdditive { get; set; }

        /// <summary>
        /// The record that changed in the sending tree, and that tree. Used when
        /// the receiving tree has no node with the same path: a person or family
        /// whose birth or marriage date differs between the files has a different
        /// key on each side, so the receiver looks for its one-to-one partner by
        /// name instead (the same pairing the Diff report uses for [CHANGED]).
        /// </summary>
        public object? SourceNode { get; set; }

        /// <inheritdoc cref="SourceNode"/>
        public DiffTreeViewModel? SourceTree { get; set; }
    }
}