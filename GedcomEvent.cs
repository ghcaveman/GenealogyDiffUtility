using System;
using System.Collections.Generic;

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
        /// Formats the event for display in the details view, e.g.
        /// "Birth (24 AUG 1938) - New Rochelle, New York" or
        /// "Ethnicity - White".
        /// </summary>
        public string DisplayName
        {
            get
            {
                string eventName = Type switch
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

                string display = eventName;
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