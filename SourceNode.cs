using System.Collections.Generic;

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
    }
}