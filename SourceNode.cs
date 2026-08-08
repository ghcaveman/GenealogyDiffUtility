using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GenealogyDiffUtility
{
    internal class SourceNode : TreeNodeBase
    {
        public string Id { get; set; } = string.Empty; // e.g., @S1@
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string PublicationInfo { get; set; } = string.Empty;
        public string RepositoryId { get; set; } = string.Empty;

        // Pointers to notes attached to this source
        public List<string> NoteIds { get; set; } = new();

        /// <summary>
        /// Display-only sub-nodes showing which individuals reference this source.
        /// These appear beneath this source when the Detail view (the "Details" checkbox)
        /// is enabled. These nodes are lightweight wrappers and do not participate in
        /// cross-tree sync or mismatch navigation.
        /// </summary>
        public ObservableCollection<SourceDetailNode> Details { get; } = new();
    }
}
