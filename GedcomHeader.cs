using System;

namespace GenealogyDiffUtility
{
    internal class GedcomHeader : TreeNodeBase
    {
        public string SourceSoftware { get; set; } = string.Empty; // e.g., Ancestry, RootsMagic
        public string SoftwareVersion { get; set; } = string.Empty;
        public string GedcomVersion { get; set; } = "5.5.1";       // Default standard
        public string CharacterEncoding { get; set; } = "UTF-8";    // Crucial for string diffing
        public DateTime? FileCreationDate { get; set; }
    }
}