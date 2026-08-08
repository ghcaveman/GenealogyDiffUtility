using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GenealogyDiffUtility
{
    internal class RepositoryNode : TreeNodeBase
    {
        public string Id { get; set; } = string.Empty; // e.g., @R1@
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        // Pointers to notes attached to this repository
        public List<string> NoteIds { get; set; } = new();

        /// <summary>
        /// Display-only sub-nodes showing which records (Source, etc.) reference
        /// this repository. These appear beneath this repository when the Detail
        /// view (the "Details" checkbox) is enabled. These nodes are lightweight
        /// wrappers and do not participate in cross-tree sync or mismatch navigation.
        /// </summary>
        public ObservableCollection<RepositoryDetailNode> Details { get; } = new();
    }
}