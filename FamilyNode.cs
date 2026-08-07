using System.Collections.Generic;

namespace GenealogyDiffUtility
{
    internal class FamilyNode : TreeNodeBase
    {
        public string Id { get; set; } = string.Empty; // e.g., @F1@
        public string HusbandId { get; set; } = string.Empty; // Points to an IndividualNode ID
        public string WifeId { get; set; } = string.Empty;   // Points to an IndividualNode ID
        public List<string> ChildrenIds { get; set; } = new(); // Points to IndividualNode IDs

        public string MarriageDate { get; set; } = string.Empty;
        public string MarriagePlace { get; set; } = string.Empty;

        // Pointers to notes attached to this family
        public List<string> NoteIds { get; set; } = new();
    }
}