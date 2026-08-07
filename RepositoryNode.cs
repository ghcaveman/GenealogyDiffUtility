using System.Collections.Generic;

namespace GenealogyDiffUtility
{
    internal class RepositoryNode : TreeNodeBase
    {
        public string Id { get; set; } = string.Empty; // e.g., @R1@
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        // Pointers to notes attached to this repository
        public List<string> NoteIds { get; set; } = new();
    }
}
