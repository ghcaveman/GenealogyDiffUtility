using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GenealogyDiffUtility
{
    internal static class GedcomParser
    {
        // Standard GEDCOM event tags that can appear on individuals or families
        private static readonly HashSet<string> EventTags = new(StringComparer.Ordinal)
        {
            // Individual events
            "BIRT", "DEAT", "BURI", "CREM", "ADOP", "BAPM", "BARM", "BASM", "BLES",
            "CHR", "CHRA", "CONF", "EMIG", "FCOM", "GRAD", "IMMI", "NATU", "ORDN",
            "RETI", "PROB", "WILL", "CENS", "EDUC", "OCCU", "RESI",
            // Family events
            "ANUL", "DIV", "DIVF", "ENGA", "MARR", "MARB", "MARC", "MARL", "MARS",
            // Generic event
            "EVEN"
        };

        // Tags whose values are free text. Their trailing whitespace is kept while
        // parsing because a following CONC line continues the text directly, so a
        // trailing space can be the only separator between two words.
        private static readonly HashSet<string> TextTags = new(StringComparer.Ordinal)
        {
            "NOTE", "CONC", "CONT", "TITL", "AUTH", "PUBL", "TEXT", "ADDR", "ADR1", "ADR2", "ADR3", "PAGE"
        };

        static GedcomParser()
        {
            // Makes Windows-1252 (GEDCOM "ANSI") available on .NET.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// A free-text value that later CONC/CONT lines (one level deeper) continue.
        /// </summary>
        private sealed class TextTarget
        {
            public int Level;
            public Func<string> Get = () => string.Empty;
            public Action<string> Set = _ => { };
            public string ContSeparator = "\n";
        }

        public static GedcomTreeContext Parse(string filePath)
        {
            if (!File.Exists(filePath)) return new GedcomTreeContext();
            return ParseLines(File.ReadLines(filePath, DetectEncoding(filePath)));
        }

        /// <summary>
        /// Picks the text encoding for a GEDCOM file: a byte-order mark wins,
        /// otherwise the header's CHAR value. UTF-8 is the default.
        /// ANSEL has no .NET decoder, so it is read as Windows-1252, which is
        /// correct for plain ASCII text but not for ANSEL's accented characters.
        /// </summary>
        public static Encoding DetectEncoding(string filePath)
        {
            byte[] head = new byte[4096];
            int read;
            using (var fs = File.OpenRead(filePath))
            {
                read = fs.Read(head, 0, head.Length);
            }

            if (read >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF) return new UTF8Encoding(false);
            if (read >= 2 && head[0] == 0xFF && head[1] == 0xFE) return Encoding.Unicode;
            if (read >= 2 && head[0] == 0xFE && head[1] == 0xFF) return Encoding.BigEndianUnicode;

            string text = Encoding.Latin1.GetString(head, 0, read);
            foreach (var rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.StartsWith("0 ") && !line.StartsWith("0 HEAD")) break; // past the header
                if (!line.StartsWith("1 CHAR", StringComparison.Ordinal)) continue;

                string charset = line.Substring(6).Trim().ToUpperInvariant();
                return charset switch
                {
                    "ANSI" or "ASCII" or "IBMPC" or "IBM WINDOWS" or "ANSEL" => Encoding.GetEncoding(1252),
                    "UNICODE" => Encoding.Unicode,
                    _ => new UTF8Encoding(false)
                };
            }
            return new UTF8Encoding(false);
        }

        internal static GedcomTreeContext ParseLines(IEnumerable<string> lines)
        {
            var context = new GedcomTreeContext();

            IndividualNode? currentIndividual = null;
            FamilyNode? currentFamily = null;
            SourceNode? currentSource = null;
            RepositoryNode? currentRepository = null;
            NoteNode? currentNote = null;
            GedcomEvent? currentEvent = null;
            TextTarget? text = null;
            string currentSection = "";
            bool hasPrimaryName = false;

            // Sub-context flags specifically for the Header block
            bool insideHeadSour = false;
            bool insideHeadGedc = false;

            // Citations and media, tracked one by one for the comparison (see GedcomCitation)
            GedcomMedia? currentMedia = null;
            GedcomCitation? citation = null;
            int citationLevel = 0;
            List<string>? inlineMediaList = null;
            int inlineMediaIndex = 0, inlineMediaLevel = 0;

            foreach (string rawLine in lines)
            {
                // Leading whitespace is not significant; trailing whitespace can be
                // (see TextTags), so only line terminators are removed from the end.
                string line = rawLine.TrimStart().TrimEnd('\r', '\n');
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] parts = line.Split(' ', 3);
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int level)) continue;

                string tag = parts[1].Trim();
                string data = parts.Length > 2 ? parts[2] : string.Empty;
                // Level-0 lines keep their raw value too: "0 @N1@ NOTE text" carries note text.
                if (level != 0 && !TextTags.Contains(tag)) data = data.Trim();

                // CONC/CONT continue the text value of the line directly above them.
                if ((tag == "CONC" || tag == "CONT") && text != null && level == text.Level + 1)
                {
                    text.Set(text.Get() + (tag == "CONT" ? text.ContSeparator : string.Empty) + data);
                    continue;
                }
                text = null;

                // A line at or above a citation's (or inline media's) level ends it.
                if (citation != null && level <= citationLevel) citation = null;
                if (inlineMediaList != null && level <= inlineMediaLevel) inlineMediaList = null;

                if (level == 0)
                {
                    currentMedia = null;
                    currentIndividual = null;
                    currentFamily = null;
                    currentSource = null;
                    currentRepository = null;
                    currentNote = null;
                    currentEvent = null;
                    hasPrimaryName = false;
                    insideHeadSour = false;
                    insideHeadGedc = false;

                    if (!tag.StartsWith("@"))
                    {
                        // e.g. "0 HEAD", "0 TRLR"
                        currentSection = tag;
                        continue;
                    }

                    string id = tag;
                    string rest = data.TrimStart();
                    string type = rest.Trim().Split(' ', 2)[0];
                    currentSection = type;

                    switch (type)
                    {
                        case "INDI":
                            currentIndividual = new IndividualNode { Id = id };
                            context.Individuals[id] = currentIndividual;
                            break;
                        case "FAM":
                            currentFamily = new FamilyNode { Id = id };
                            context.Families[id] = currentFamily;
                            break;
                        case "SOUR":
                            currentSource = new SourceNode { Id = id };
                            context.Sources[id] = currentSource;
                            break;
                        case "REPO":
                            currentRepository = new RepositoryNode { Id = id };
                            context.Repositories[id] = currentRepository;
                            break;
                        case "OBJE":
                            currentMedia = new GedcomMedia { Id = id };
                            context.Media[id] = currentMedia;
                            break;
                        case "NOTE":
                            var note = new NoteNode { Id = id };
                            currentNote = note;
                            context.Notes[id] = note;

                            // The text portion that follows "NOTE" on the same line
                            int noteAt = rest.IndexOf("NOTE", StringComparison.Ordinal);
                            string noteText = rest.Substring(noteAt + 4);
                            if (noteText.StartsWith(" ")) noteText = noteText.Substring(1);
                            note.Text = noteText;
                            text = new TextTarget { Level = 0, Get = () => note.Text, Set = v => note.Text = v };
                            break;
                    }
                    continue;
                }

                switch (currentSection)
                {
                    case "HEAD":
                        ParseHeaderLine(context.Header, level, tag, data, ref insideHeadSour, ref insideHeadGedc);
                        break;

                    case "INDI" when currentIndividual != null:
                        text = ParseIndividualLine(currentIndividual, level, tag, data, ref currentEvent, ref hasPrimaryName);
                        break;

                    case "FAM" when currentFamily != null:
                        text = ParseFamilyLine(currentFamily, level, tag, data, ref currentEvent);
                        break;

                    case "SOUR" when currentSource != null:
                        text = ParseSourceLine(currentSource, level, tag, data);
                        break;

                    case "REPO" when currentRepository != null:
                        text = ParseRepositoryLine(currentRepository, level, tag, data);
                        break;

                    case "OBJE" when currentMedia != null:
                        if (tag == "FILE" && level == 1) currentMedia.File = data.Trim();
                        else if (tag == "TITL" && currentMedia.Title.Length == 0) currentMedia.Title = data.Trim();
                        break;
                }

                // People and families: also record citations, direct notes and media
                // one by one, for the comparison.
                if ((currentSection == "INDI" && currentIndividual != null) ||
                    (currentSection == "FAM" && currentFamily != null))
                {
                    var recordCitations = currentIndividual?.Citations ?? currentFamily!.Citations;
                    var recordNotes = currentIndividual?.DirectNoteIds ?? currentFamily!.DirectNoteIds;
                    var recordMedia = currentIndividual?.Media ?? currentFamily!.Media;
                    bool inEvent = currentEvent != null && level >= 2;

                    if (inlineMediaList != null && tag == "FILE" && level == inlineMediaLevel + 1)
                    {
                        inlineMediaList[inlineMediaIndex] = "file:" + data.Trim();
                    }
                    else if (citation != null)
                    {
                        // A line inside the current citation.
                        int depth = level - citationLevel;
                        var c = citation;
                        if (depth == 1 && tag == "PAGE")
                        {
                            c.Page = data;
                            text ??= new TextTarget { Level = level, Get = () => c.Page, Set = v => c.Page = v };
                        }
                        else if (depth == 1 && tag == "QUAY") c.Quality = data;
                        else if (depth == 2 && tag == "DATE") c.Date = data;
                        else if (depth == 1 && tag == "NOTE") text ??= AddNote(c.NoteIds, c.InlineNotes, data, level);
                        else if (depth == 1 && tag == "OBJE")
                            AddMedia(c.Media, data, level, ref inlineMediaList, ref inlineMediaIndex, ref inlineMediaLevel);
                    }
                    else if (tag == "SOUR")
                    {
                        var c = new GedcomCitation();
                        string value = data.Trim();
                        if (value.Length > 1 && value.StartsWith("@") && value.EndsWith("@")) c.SourceId = value;
                        else
                        {
                            c.InlineText = data;
                            text ??= new TextTarget { Level = level, Get = () => c.InlineText, Set = v => c.InlineText = v };
                        }
                        (inEvent ? currentEvent!.Citations : recordCitations).Add(c);
                        citation = c;
                        citationLevel = level;
                    }
                    else if (tag == "NOTE" && level == 1) AddPointer(recordNotes, data);
                    else if (tag == "NOTE" && level == 2 && inEvent) AddPointer(currentEvent!.DirectNoteIds, data);
                    else if (tag == "OBJE" && level == 1)
                        AddMedia(recordMedia, data, level, ref inlineMediaList, ref inlineMediaIndex, ref inlineMediaLevel);
                    else if (tag == "OBJE" && level == 2 && inEvent)
                        AddMedia(currentEvent!.Media, data, level, ref inlineMediaList, ref inlineMediaIndex, ref inlineMediaLevel);
                }
            }

            TrimTextFields(context);
            SetEventOwnerNames(context);
            return context;
        }

        /// <summary>
        /// Records each event's owner names (see <see cref="GedcomEvent.OwnerNameKeys"/>):
        /// a person's names, or a family's spouse names, alone and in pairs.
        /// </summary>
        private static void SetEventOwnerNames(GedcomTreeContext context)
        {
            static List<string> NamesOf(IndividualNode? p) =>
                p == null ? new List<string>()
                          : new[] { p.FullName }.Concat(p.AlternateNames).Where(n => n.Length > 0).ToList();

            foreach (var person in context.Individuals.Values)
            {
                var keys = NamesOf(person).Select(GedcomEvent.NameKey).Distinct().ToList();
                foreach (var evt in person.Events) evt.OwnerNameKeys.AddRange(keys);
            }

            foreach (var family in context.Families.Values)
            {
                context.Individuals.TryGetValue(family.HusbandId, out var husband);
                context.Individuals.TryGetValue(family.WifeId, out var wife);
                var husbandNames = NamesOf(husband);
                var wifeNames = NamesOf(wife);

                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var h in husbandNames) keys.Add(GedcomEvent.NameKey(h));
                foreach (var w in wifeNames) keys.Add(GedcomEvent.NameKey(w));
                foreach (var h in husbandNames)
                    foreach (var w in wifeNames)
                        keys.Add(GedcomEvent.NameKey(h + " " + w));

                foreach (var evt in family.Events) evt.OwnerNameKeys.AddRange(keys);
            }
        }

        private static void ParseHeaderLine(GedcomHeader header, int level, string tag, string data,
            ref bool insideHeadSour, ref bool insideHeadGedc)
        {
            if (level == 1)
            {
                // Reset level 1 flags when changing sub-structures
                insideHeadSour = tag == "SOUR";
                insideHeadGedc = tag == "GEDC";

                if (tag == "SOUR")
                    header.SourceSoftware = data;
                else if (tag == "CHAR")
                    header.CharacterEncoding = data;
                else if (tag == "DATE" && TryParseGedcomDate(data, out DateTime parsedDate))
                    header.FileCreationDate = parsedDate;
            }
            else if (level == 2 && tag == "VERS")
            {
                if (insideHeadSour) header.SoftwareVersion = data;
                else if (insideHeadGedc) header.GedcomVersion = data;
            }
        }

        /// <summary>
        /// Parses an exact GEDCOM date such as "15 JUL 2026" independently of the
        /// machine's regional settings.
        /// </summary>
        internal static bool TryParseGedcomDate(string value, out DateTime date)
        {
            return DateTime.TryParseExact(value.Trim(), new[] { "d MMM yyyy", "dd MMM yyyy" },
                       CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date) ||
                   DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
        }

        /// <summary>
        /// Handles one line inside an INDI record. Returns the text target that
        /// CONC/CONT lines following this line should continue, if any.
        /// </summary>
        private static TextTarget? ParseIndividualLine(IndividualNode person, int level, string tag, string data,
            ref GedcomEvent? currentEvent, ref bool hasPrimaryName)
        {
            if (level == 1)
            {
                currentEvent = null;

                if (tag == "NAME")
                {
                    string cleaned = CleanName(data);
                    if (!hasPrimaryName)
                    {
                        // GEDCOM lists the preferred name first; later NAME lines
                        // are alternate names (married names, AKAs, translations).
                        hasPrimaryName = true;
                        person.FullName = cleaned;
                        person.LastName = ExtractSurname(data);
                        if (string.IsNullOrEmpty(person.LastName))
                            person.LastName = "Unknown";
                    }
                    else if (cleaned.Length > 0)
                    {
                        person.AlternateNames.Add(cleaned);
                    }
                }
                else if (tag == "SEX") person.Gender = data;
                else if (EventTags.Contains(tag))
                {
                    currentEvent = new GedcomEvent { Type = tag, Data = data.Trim() };
                    person.Events.Add(currentEvent);
                }
                else if (tag == "SOUR") AddPointer(person.SourceIds, data);
                else if (tag == "NOTE") return AddNote(person.NoteIds, person.InlineNotes, data, level);
                return null;
            }

            if (currentEvent != null)
                return ParseEventLine(currentEvent, level, tag, data, person);

            // Pointers nested under other level-1 structures (e.g. a citation under
            // NAME) still belong to the person.
            if (tag == "SOUR") AddPointer(person.SourceIds, data);
            else if (tag == "NOTE") AddPointer(person.NoteIds, data);
            return null;
        }

        /// <summary>
        /// Handles one line inside a FAM record. Returns the text target that
        /// CONC/CONT lines following this line should continue, if any.
        /// </summary>
        private static TextTarget? ParseFamilyLine(FamilyNode family, int level, string tag, string data,
            ref GedcomEvent? currentEvent)
        {
            if (level == 1)
            {
                currentEvent = null;

                if (tag == "HUSB") family.HusbandId = data;
                else if (tag == "WIFE") family.WifeId = data;
                else if (tag == "CHIL") family.ChildrenIds.Add(data);
                else if (EventTags.Contains(tag))
                {
                    currentEvent = new GedcomEvent { Type = tag, Data = data.Trim() };
                    family.Events.Add(currentEvent);
                }
                else if (tag == "SOUR") AddPointer(family.SourceIds, data);
                else if (tag == "NOTE") return AddNote(family.NoteIds, family.InlineNotes, data, level);
                return null;
            }

            if (currentEvent != null)
                return ParseEventLine(currentEvent, level, tag, data, family);

            if (tag == "SOUR") AddPointer(family.SourceIds, data);
            else if (tag == "NOTE") AddPointer(family.NoteIds, data);
            return null;
        }

        /// <summary>
        /// Handles a line below a level-1 event. Only level-2 DATE/PLAC/TYPE belong
        /// to the event itself; deeper ones (e.g. a citation's "4 DATE") describe
        /// sub-structures and must not overwrite the event's own values.
        /// </summary>
        private static TextTarget? ParseEventLine(GedcomEvent evt, int level, string tag, string data, object owner)
        {
            if (level == 2)
            {
                if (tag == "DATE")
                {
                    evt.Date = data;
                    if (owner is IndividualNode p)
                    {
                        if (evt.Type == "BIRT" && p.BirthDate.Length == 0) p.BirthDate = data;
                        else if (evt.Type == "DEAT" && p.DeathDate.Length == 0) p.DeathDate = data;
                    }
                    else if (owner is FamilyNode f && evt.Type == "MARR" && f.MarriageDate.Length == 0)
                    {
                        f.MarriageDate = data;
                    }
                }
                else if (tag == "PLAC")
                {
                    evt.Place = data;
                    if (owner is IndividualNode p)
                    {
                        if (evt.Type == "BIRT" && p.BirthPlace.Length == 0) p.BirthPlace = data;
                        else if (evt.Type == "DEAT" && p.DeathPlace.Length == 0) p.DeathPlace = data;
                    }
                    else if (owner is FamilyNode f && evt.Type == "MARR" && f.MarriagePlace.Length == 0)
                    {
                        f.MarriagePlace = data;
                    }
                }
                else if (tag == "TYPE") evt.SubType = data;
                else if (tag == "SOUR") AddPointer(evt.SourceIds, data);
                else if (tag == "NOTE") return AddNote(evt.NoteIds, evt.InlineNotes, data, level);
                return null;
            }

            // Deeper pointers (e.g. a note on the event's citation) still relate to the event
            if (tag == "SOUR") AddPointer(evt.SourceIds, data);
            else if (tag == "NOTE") AddPointer(evt.NoteIds, data);
            return null;
        }

        private static TextTarget? ParseSourceLine(SourceNode source, int level, string tag, string data)
        {
            if (level == 1)
            {
                switch (tag)
                {
                    case "TITL":
                        source.Title = data;
                        return new TextTarget { Level = 1, Get = () => source.Title, Set = v => source.Title = v };
                    case "AUTH":
                        source.Author = data;
                        return new TextTarget { Level = 1, Get = () => source.Author, Set = v => source.Author = v };
                    case "PUBL":
                        source.PublicationInfo = data;
                        return new TextTarget { Level = 1, Get = () => source.PublicationInfo, Set = v => source.PublicationInfo = v };
                    case "REPO":
                        source.RepositoryId = data.Trim();
                        return null;
                    case "NOTE":
                        return AddNote(source.NoteIds, source.InlineNotes, data, level);
                }
                return null;
            }

            if (tag == "NOTE") AddPointer(source.NoteIds, data);
            return null;
        }

        private static TextTarget? ParseRepositoryLine(RepositoryNode repo, int level, string tag, string data)
        {
            if (level == 1)
            {
                switch (tag)
                {
                    case "NAME":
                        repo.Name = data.Trim();
                        return new TextTarget { Level = 1, Get = () => repo.Name, Set = v => repo.Name = v };
                    case "ADDR":
                        repo.Address = data;
                        return new TextTarget { Level = 1, Get = () => repo.Address, Set = v => repo.Address = v, ContSeparator = ", " };
                    case "NOTE":
                        AddPointer(repo.NoteIds, data);
                        return null;
                }
                return null;
            }

            if (level == 2 && tag == "ADR1")
            {
                repo.Address = string.IsNullOrEmpty(repo.Address) ? data : repo.Address.TrimEnd() + " " + data;
                return new TextTarget { Level = 2, Get = () => repo.Address, Set = v => repo.Address = v, ContSeparator = ", " };
            }
            return null;
        }

        /// <summary>
        /// Records a NOTE line: a pointer ("@N1@") goes in the pointer list, any
        /// other value is inline note text whose CONC/CONT lines are joined to it.
        /// </summary>
        private static TextTarget? AddNote(List<string> noteIds, List<string> inlineNotes, string data, int level)
        {
            string trimmed = data.Trim();
            if (trimmed.StartsWith("@") && trimmed.EndsWith("@"))
            {
                noteIds.Add(trimmed);
                return null;
            }

            int index = inlineNotes.Count;
            inlineNotes.Add(data);
            return new TextTarget { Level = level, Get = () => inlineNotes[index], Set = v => inlineNotes[index] = v };
        }

        /// <summary>
        /// Adds a cross-reference pointer (e.g., "@S1@") to a list; other values are ignored.
        /// </summary>
        /// <summary>
        /// Adds a media pointer, or for media written inline ("1 OBJE" with a
        /// "2 FILE" line below) a placeholder the FILE line fills in.
        /// </summary>
        private static void AddMedia(List<string> media, string data, int level,
            ref List<string>? inlineList, ref int inlineIndex, ref int inlineLevel)
        {
            string trimmed = data.Trim();
            if (trimmed.Length > 1 && trimmed.StartsWith("@") && trimmed.EndsWith("@"))
            {
                media.Add(trimmed);
                return;
            }
            inlineList = media;
            inlineIndex = media.Count;
            inlineLevel = level;
            media.Add("file:");
        }

        private static void AddPointer(List<string> ids, string data)
        {
            string trimmed = data.Trim();
            if (trimmed.Length > 1 && trimmed.StartsWith("@") && trimmed.EndsWith("@"))
                ids.Add(trimmed);
        }

        private static string CleanName(string data)
        {
            var words = data.Replace("/", " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words);
        }

        private static string ExtractSurname(string data)
        {
            int start = data.IndexOf('/');
            int end = data.LastIndexOf('/');
            return start >= 0 && end > start ? data.Substring(start + 1, end - start - 1).Trim() : string.Empty;
        }

        /// <summary>
        /// Trailing whitespace was kept during parsing so CONC lines join correctly;
        /// once everything is joined it is no longer significant.
        /// </summary>
        private static void TrimTextFields(GedcomTreeContext context)
        {
            static void TrimList(List<string> list)
            {
                for (int i = 0; i < list.Count; i++) list[i] = list[i].Trim();
            }

            foreach (var p in context.Individuals.Values)
            {
                TrimList(p.InlineNotes);
                foreach (var e in p.Events) TrimList(e.InlineNotes);
            }
            foreach (var f in context.Families.Values)
            {
                TrimList(f.InlineNotes);
                foreach (var e in f.Events) TrimList(e.InlineNotes);
            }
            foreach (var s in context.Sources.Values)
            {
                s.Title = s.Title.Trim();
                s.Author = s.Author.Trim();
                s.PublicationInfo = s.PublicationInfo.Trim();
                TrimList(s.InlineNotes);
            }
            foreach (var r in context.Repositories.Values)
            {
                r.Name = r.Name.Trim();
                r.Address = r.Address.Trim();
            }
            foreach (var n in context.Notes.Values)
            {
                n.Text = n.Text.Trim();
            }
        }
    }
}
