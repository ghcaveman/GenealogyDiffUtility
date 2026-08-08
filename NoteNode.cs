using System.Collections.ObjectModel;

namespace GenealogyDiffUtility
{
    internal class NoteNode : TreeNodeBase
    {
        public string Id { get; set; } = string.Empty; // e.g., @N1@
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Display-only sub-nodes showing which records (Individual, Family,
        /// Source, Repository, etc.) reference this note. These appear beneath
        /// this note when the Detail view (the "Details" checkbox) is enabled.
        /// These nodes are lightweight wrappers and do not participate in
        /// cross-tree sync or mismatch navigation.
        /// </summary>
        public ObservableCollection<NoteDetailNode> Details { get; } = new();

        /// <summary>
        /// A short preview of the note text for display in the tree.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Text)) return Id;
                string singleLine = Text.Replace("\n", " ").Replace("\r", " ").Trim();
                const int maxLength = 80;
                if (singleLine.Length <= maxLength) return singleLine;
                return singleLine.Substring(0, maxLength - 3) + "...";
            }
        }
    }
}
