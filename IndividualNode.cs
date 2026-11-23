using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace GenealogyDiffUtility
{
    internal class IndividualNode : TreeNodeBase
    {
        public string Id { get; set; } = string.Empty; // The original ID like @I1@
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Names from the second and later NAME lines (married names, AKAs, etc.).
        /// The first NAME line is the preferred name and goes in <see cref="FullName"/>.
        /// </summary>
        public List<string> AlternateNames { get; } = new();

        /// <summary>
        /// Used to disambiguate records that share the same name and have no birth
        /// date, so that the stable identity key remains unique within a tree.
        /// Set by <see cref="TreeNodeKeys.AssignCollisionIndices"/>.
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

        // Note text written directly on the record ("1 NOTE some text") rather than as a pointer
        public List<string> InlineNotes { get; } = new();

        /// <summary>Citations, one by one, with their page, date, confidence, notes and media.</summary>
        public List<GedcomCitation> Citations { get; } = new();

        /// <summary>Note pointers attached directly here (not via a citation or event).</summary>
        public List<string> DirectNoteIds { get; } = new();

        /// <summary>Media attached directly here: pointers ("@O1@") or "file:&lt;path&gt;".</summary>
        public List<string> Media { get; } = new();

        /// <summary>
        /// All events (birth, death, burial, baptism, census, residence, etc.)
        /// associated with this individual, parsed from the GEDCOM file.
        /// </summary>
        public List<GedcomEvent> Events { get; set; } = new();

        /// <summary>
        /// Display-only sub-nodes (spouses, children, notes, events) that appear
        /// beneath this individual when the Detail view (the "Details" checkbox) is
        /// enabled. These nodes are lightweight wrappers and do not participate in
        /// cross-tree sync.
        /// </summary>
        public ObservableCollection<TreeNodeBase> Details { get; } = new();

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
        /// Formats a GEDCOM date for display without making it look more exact than it is:
        /// "24 AUG 1938" becomes 08/24/1938, "AUG 1938" becomes 08/1938, "1938" stays 1938,
        /// and qualifiers are kept ("ABT 1773" becomes "abt 1773", "BET 1850 AND 1860"
        /// becomes "bet 1850 and 1860"). Anything it can't read is shown as written.
        /// </summary>
        internal static string FormatDate(string gedcomDate)
        {
            if (string.IsNullOrWhiteSpace(gedcomDate)) return string.Empty;

            string[] words = gedcomDate.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<string>();
            var datePart = new List<string>();

            void FlushDate()
            {
                if (datePart.Count == 0) return;
                parts.Add(FormatDatePart(string.Join(" ", datePart)));
                datePart.Clear();
            }

            foreach (string word in words)
            {
                if (DateKeywords.Contains(word))
                {
                    FlushDate();
                    parts.Add(word.ToLowerInvariant());
                }
                else
                {
                    datePart.Add(word);
                }
            }
            FlushDate();

            return string.Join(" ", parts);
        }

        private static readonly HashSet<string> DateKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "ABT", "BEF", "AFT", "BET", "AND", "CAL", "EST", "FROM", "TO", "INT"
        };

        /// <summary>
        /// Formats one plain date (no qualifiers) at the precision it was written with.
        /// </summary>
        private static string FormatDatePart(string date)
        {
            if (DateTime.TryParseExact(date, new[] { "d MMM yyyy", "d MMMM yyyy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day))
                return day.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

            if (DateTime.TryParseExact(date, new[] { "MMM yyyy", "MMMM yyyy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime month))
                return month.ToString("MM/yyyy", CultureInfo.InvariantCulture);

            // A year, or anything else (dual dates like 1750/51, date phrases), as written.
            return date;
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
