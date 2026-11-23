using System;
using System.Collections.Generic;
using System.Linq;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Represents a GEDCOM event (birth, death, marriage, census, residence, etc.)
    /// associated with an individual or family record.
    /// </summary>
    internal class GedcomEvent
    {
        /// <summary>
        /// The GEDCOM event tag (e.g., "BIRT", "DEAT", "MARR", "EVEN", "CENS", "RESI").
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The value of the TYPE sub-tag, used primarily by generic EVEN events
        /// (e.g., "Ethnicity", "Pension", "MilitaryService").
        /// </summary>
        public string SubType { get; set; } = string.Empty;

        /// <summary>
        /// Inline data on the event line itself (e.g., "1 EVEN White" → "White").
        /// </summary>
        public string Data { get; set; } = string.Empty;

        public string Date { get; set; } = string.Empty;
        public string Place { get; set; } = string.Empty;

        // Pointers to sources that prove this event
        public List<string> SourceIds { get; set; } = new();

        // Pointers to notes attached to this event
        public List<string> NoteIds { get; set; } = new();

        // Note text written directly on the event ("2 NOTE some text") rather than as a pointer
        public List<string> InlineNotes { get; } = new();

        /// <summary>Citations, one by one, with their page, date, confidence, notes and media.</summary>
        public List<GedcomCitation> Citations { get; } = new();

        /// <summary>Note pointers attached directly here (not via a citation or event).</summary>
        public List<string> DirectNoteIds { get; } = new();

        /// <summary>Media attached directly here: pointers ("@O1@") or "file:&lt;path&gt;".</summary>
        public List<string> Media { get; } = new();

        /// <summary>
        /// The names of the record this event belongs to, each as a <see cref="NameKey"/>:
        /// every name of the person, or every husband-and-wife name pair of the family
        /// (and each spouse alone). Filled in by the parser, and used to recognize
        /// descriptions a program generated from the event and the owner's name
        /// ("Birth of Shebroe, David Paul", "Marriage of Crisara, Philip and Pittaluga, Tina Marie").
        /// </summary>
        public List<string> OwnerNameKeys { get; } = new();

        /// <summary>
        /// A name reduced to its words, lower-cased and sorted, ignoring punctuation and
        /// the word "and", so "Shebroe, David Paul" and "David Paul Shebroe" are equal.
        /// </summary>
        internal static string NameKey(string text) =>
            string.Join(" ", NameWords(text).OrderBy(w => w, StringComparer.Ordinal));

        /// <summary>The words of a name, lower-cased, in order, without punctuation or "and".</summary>
        internal static List<string> NameWords(string text) =>
            System.Text.RegularExpressions.Regex
                .Split(text.ToLowerInvariant(), @"[^\p{L}\p{N}']+")
                .Where(w => w.Length > 0 && w != "and")
                .ToList();

        /// <summary>
        /// True when this event is an internal system tag (e.g., "_PPEXCLUDE",
        /// "_FSLINK") rather than a real genealogical event. Internal tags start
        /// with an underscore per GEDCOM conventions.
        /// </summary>
        public bool IsInternal =>
            Type.StartsWith("_") ||
            SubType.StartsWith("_") ||
            Data.StartsWith("_") ||
            (Type == "EVEN" && Data.StartsWith("http", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The friendly event name, e.g. "Birth" for BIRT, or the TYPE value
        /// ("Alt. Birth", "Military Draft Registration") for a generic EVEN.
        /// </summary>
        public string EventName =>
            Type switch
            {
                "BIRT" => "Birth",
                "DEAT" => "Death",
                "BURI" => "Burial",
                "BAPM" => "Baptism",
                "MARR" => "Marriage",
                "DIV" => "Divorce",
                "CENS" => "Census",
                "RESI" => "Residence",
                "OCCU" => "Occupation",
                "IMMI" => "Immigration",
                "EMIG" => "Emigration",
                "NATU" => "Naturalization",
                "GRAD" => "Graduation",
                "EDUC" => "Education",
                "RETI" => "Retirement",
                "PROB" => "Probate",
                "WILL" => "Will",
                "ADOP" => "Adoption",
                "CHR" => "Christening",
                "CREM" => "Cremation",
                "CONF" => "Confirmation",
                "EVEN" => !string.IsNullOrEmpty(SubType) ? SubType : (!string.IsNullOrEmpty(Data) ? Data : "Event"),
                _ => Type
            };

        /// <summary>
        /// Formats the event for display in the details view, e.g.
        /// "Birth (3 MAR 1901) - Springfield, Ohio" or
        /// "Ethnicity - White".
        /// </summary>
        public string DisplayName
        {
            get
            {
                string display = EventName;
                if (!string.IsNullOrEmpty(Date))
                    display += $" ({Date})";
                if (!string.IsNullOrEmpty(Place))
                    display += $" - {Place}";
                if (!string.IsNullOrEmpty(Data) && (Type != "EVEN" || !string.IsNullOrEmpty(SubType)))
                    display += $" - {Data}";
                return display;
            }
        }
    }
}