namespace GenealogyDiffUtility
{
    internal class NoteNode : TreeNodeBase
    {
        public string Id { get; set; } = string.Empty; // e.g., @N1@
        public string Text { get; set; } = string.Empty;

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
