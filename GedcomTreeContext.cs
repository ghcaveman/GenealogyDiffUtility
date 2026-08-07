using System;
using System.Collections.Generic;

namespace GenealogyDiffUtility
{
    internal class GedcomTreeContext
    {
        // The file metadata node you wanted to track data lineage
        public GedcomHeader Header { get; set; } = new();

        // Core records indexed by their original GEDCOM cross-reference ID (e.g., "@I1@")
        public Dictionary<string, IndividualNode> Individuals { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, FamilyNode> Families { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, SourceNode> Sources { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, RepositoryNode> Repositories { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, NoteNode> Notes { get; set; } = new(StringComparer.Ordinal);
    }
}
