using System.Collections.Generic;

namespace GenealogyDiffUtility
{
    internal class IndividualNode
    {
        public string Id { get; set; } = string.Empty; // The original ID like @I1@
        public string FullName { get; set; } = string.Empty;

        // Added the missing property that the parser and tree groups need
        public string LastName { get; set; } = string.Empty;

        public string Gender { get; set; } = string.Empty;

        // Tracking vital events
        public string BirthDate { get; set; } = string.Empty;
        public string BirthPlace { get; set; } = string.Empty;
        public string DeathDate { get; set; } = string.Empty;
        public string DeathPlace { get; set; } = string.Empty;

        // Pointers to the sources that prove this person exists
        public List<string> SourceIds { get; set; } = new();
    }
}
