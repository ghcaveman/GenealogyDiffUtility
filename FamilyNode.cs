using System;
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

        // Pointers to sources that prove this family exists
        public List<string> SourceIds { get; set; } = new();

        // Resolved references to the parent individuals (set by DiffTreeViewModel.LoadTree)
        public IndividualNode? Husband { get; set; }
        public IndividualNode? Wife { get; set; }

        /// <summary>
        /// Displays the family as "LastName Father, FirstName Father, LastName Mother,
        /// FirstName Mother (m. mm/dd/yyyy)". The marriage portion is only included
        /// when the marriage date is known.
        /// </summary>
        public string DisplayName
        {
            get
            {
                string husbandPart = FormatPerson(Husband);
                string wifePart = FormatPerson(Wife);

                string namePart;
                if (!string.IsNullOrEmpty(husbandPart) && !string.IsNullOrEmpty(wifePart))
                    namePart = $"{husbandPart}, {wifePart}";
                else if (!string.IsNullOrEmpty(husbandPart))
                    namePart = husbandPart;
                else if (!string.IsNullOrEmpty(wifePart))
                    namePart = wifePart;
                else
                    return $"Family ID: {Id}";

                string marriagePart = IndividualNode.FormatDate(MarriageDate);
                if (!string.IsNullOrEmpty(marriagePart))
                    return $"{namePart} (m. {marriagePart})";

                return namePart;
            }
        }

        /// <summary>
        /// Formats an individual as "LastName, FirstName" for the family display.
        /// Returns an empty string if the individual is null.
        /// </summary>
        private static string FormatPerson(IndividualNode? person)
        {
            if (person == null) return string.Empty;

            string firstName = person.FullName;
            if (!string.IsNullOrWhiteSpace(person.LastName) &&
                person.FullName.EndsWith(person.LastName, StringComparison.OrdinalIgnoreCase))
            {
                firstName = person.FullName.Substring(0, person.FullName.Length - person.LastName.Length).Trim();
            }

            return string.IsNullOrWhiteSpace(person.LastName)
                ? person.FullName
                : $"{person.LastName}, {firstName}";
        }
    }
}