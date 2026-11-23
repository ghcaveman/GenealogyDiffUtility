using System.Collections.Generic;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// One citation: a reference from a person, family or event to a source
    /// ("2 SOUR @S1@"), with what it cites in that source and its own notes and
    /// media. Kept alongside the owner's flat SourceIds/NoteIds lists, which the
    /// tree view uses, so citations can be compared one by one.
    /// </summary>
    internal sealed class GedcomCitation
    {
        /// <summary>The source pointer ("@S1@"), or empty for a source written inline.</summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>Text of a source written inline ("1 SOUR Family Bible") instead of a pointer.</summary>
        public string InlineText { get; set; } = string.Empty;

        /// <summary>Where in the source (PAGE): a page, record URL, film number...</summary>
        public string Page { get; set; } = string.Empty;

        /// <summary>The citation's own date (DATA / DATE).</summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>The confidence level (QUAY, 0-3).</summary>
        public string Quality { get; set; } = string.Empty;

        public List<string> NoteIds { get; } = new();
        public List<string> InlineNotes { get; } = new();

        /// <summary>Media pointers ("@O1@"), or "file:&lt;path&gt;" for media written inline.</summary>
        public List<string> Media { get; } = new();
    }

    /// <summary>A multimedia record ("0 @O1@ OBJE") with its file and title.</summary>
    internal sealed class GedcomMedia
    {
        public string Id { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }
}
