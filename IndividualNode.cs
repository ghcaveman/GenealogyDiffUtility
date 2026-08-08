using System;
using System.Collections.Generic;
using System.Globalization;

namespace GenealogyDiffUtility
{
    internal class IndividualNode : TreeNodeBase
    {
        public string Id { get; set; } = string.Empty; // The original ID like @I1@
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Used to disambiguate records that share the same name and have no birth
        /// date, so that the stable identity key remains unique within a tree.
        /// Set by <see cref="DiffTreeViewModel.LoadTree"/>.
        /// </summary>
        public int CollisionIndex { get; set; } = 1;

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

        // Pointers to notes attached to this person or their events
        public List<string> NoteIds { get; set; } = new();

        /// <summary>
        /// Displays the name in "LastName, FirstName (b. mm/dd/yyyy d. mm/dd/yyyy)" format.
        /// Only includes the birth/death date portions when the data is available.
        /// </summary>
        public string DisplayName
        {
            get
            {
                // Extract first name from FullName (which is "First Last" format)
                string firstName = FullName;
                if (!string.IsNullOrWhiteSpace(LastName) && FullName.EndsWith(LastName, StringComparison.OrdinalIgnoreCase))
                {
                    firstName = FullName.Substring(0, FullName.Length - LastName.Length).Trim();
                }

                string namePart = string.IsNullOrWhiteSpace(LastName)
                    ? FullName
                    : $"{LastName}, {firstName}";

                string birthPart = FormatDate(BirthDate);
                string deathPart = FormatDate(DeathDate);

                if (!string.IsNullOrEmpty(birthPart) && !string.IsNullOrEmpty(deathPart))
                    return $"{namePart} (b. {birthPart} d. {deathPart})";
                if (!string.IsNullOrEmpty(birthPart))
                    return $"{namePart} (b. {birthPart})";
                if (!string.IsNullOrEmpty(deathPart))
                    return $"{namePart} (d. {deathPart})";
                return namePart;
            }
        }

        /// <summary>
        /// Converts a GEDCOM date string (e.g., "24 AUG 1938", "1994", "ABT 1974")
        /// to mm/dd/yyyy format. Returns the original string if it cannot be parsed.
        /// </summary>
        internal static string FormatDate(string gedcomDate)
        {
            if (string.IsNullOrWhiteSpace(gedcomDate)) return string.Empty;

            string date = gedcomDate.Trim();

            // Strip GEDCOM qualifiers (ABT, BEF, AFT, etc.) for display purposes
            string[] qualifiers = { "ABT ", "BEF ", "AFT ", "BET ", "AND ", "CAL ", "EST " };
            foreach (var q in qualifiers)
            {
                if (date.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                {
                    date = date.Substring(q.Length).Trim();
                    break;
                }
            }

            // Try to parse the date in various GEDCOM formats
            string[] formats =
            {
                "d MMM yyyy",   // 24 AUG 1938
                "d MMMM yyyy",  // 24 August 1938
                "MMM yyyy",     // AUG 1938
                "MMMM yyyy",    // August 1938
                "yyyy",         // 1938
                "d MMM yy",     // 24 AUG 38
                "d MMMM yy",    // 24 August 38
                "MMM yy",       // AUG 38
                "MMMM yy",      // August 38
                "yy"            // 38
            };

            if (DateTime.TryParseExact(date, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed))
            {
                return parsed.ToString("MM/dd/yyyy");
            }

            // Fallback: try general parsing
            if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed2))
            {
                return parsed2.ToString("MM/dd/yyyy");
            }

            // Return the original string if we can't parse it
            return gedcomDate.Trim();
        }

        /// <summary>
        /// Notifies listeners that the display name has changed, typically after
        /// the individual's vital record data has been edited via the edit dialog.
        /// </summary>
        public void RefreshDisplay()
        {
            OnPropertyChanged(nameof(DisplayName));
        }
    }
}
