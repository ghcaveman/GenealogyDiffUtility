using System;
using System.Collections.Generic;
using System.IO;

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

        public static GedcomTreeContext Parse(string filePath)
        {
            return ParseFile(filePath);
        }

        private static GedcomTreeContext ParseFile(string filePath)
        {
            var context = new GedcomTreeContext();
            if (!File.Exists(filePath)) return context;

            IndividualNode? currentIndividual = null;
            FamilyNode? currentFamily = null;
            SourceNode? currentSource = null;
            RepositoryNode? currentRepository = null;
            NoteNode? currentNote = null;
            string currentSection = "";
            string currentRepoField = ""; // Tracks which field (NAME/ADDR) a CONC line belongs to
            GedcomEvent? currentEvent = null;

            // Sub-context flags specifically for the Header block
            bool insideHeadSour = false;
            bool insideHeadGedc = false;

            using (var reader = new StreamReader(filePath))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    string[] parts = trimmed.Split(' ', 3);
                    if (parts.Length < 2) continue;

                    string level = parts[0];

                    if (level == "0")
                    {
                        currentIndividual = null;
                        currentFamily = null;
                        currentSource = null;
                        currentRepository = null;
                        currentNote = null;
                        currentEvent = null;
                        currentRepoField = "";
                        insideHeadSour = false;
                        insideHeadGedc = false;

                        if (parts.Length == 2)
                        {
                            currentSection = parts[1];
                            continue;
                        }

                        string id = parts[1];
                        string type = parts[2];

                        if (type == "INDI")
                        {
                            currentSection = "INDI";
                            currentIndividual = new IndividualNode { Id = id };
                            context.Individuals[id] = currentIndividual;
                        }
                        else if (type == "FAM")
                        {
                            currentSection = "FAM";
                            currentFamily = new FamilyNode { Id = id };
                            context.Families[id] = currentFamily;
                        }
                        else if (type == "SOUR")
                        {
                            currentSection = "SOUR";
                            currentSource = new SourceNode { Id = id };
                            context.Sources[id] = currentSource;
                        }
                        else if (type == "REPO")
                        {
                            currentSection = "REPO";
                            currentRepository = new RepositoryNode { Id = id };
                            context.Repositories[id] = currentRepository;
                        }
                        else if (type.StartsWith("NOTE"))
                        {
                            currentSection = "NOTE";
                            currentNote = new NoteNode { Id = id };
                            context.Notes[id] = currentNote;

                            // Extract the text portion that follows "NOTE" on the same line
                            if (type.Length > 4)
                            {
                                string text = type.Substring(4).TrimStart();
                                if (!string.IsNullOrEmpty(text))
                                    currentNote.Text = text;
                            }
                        }
                        continue;
                    }

                    string tag = parts[1];
                    string data = parts.Length > 2 ? parts[2] : string.Empty;

                    // --- HEADER PARSING SYSTEM ---
                    if (currentSection == "HEAD")
                    {
                        if (level == "1")
                        {
                            // Reset level 1 flags when changing sub-structures
                            insideHeadSour = (tag == "SOUR");
                            insideHeadGedc = (tag == "GEDC");

                            if (tag == "CHAR")
                                context.Header.CharacterEncoding = data;
                            else if (tag == "DATE")
                            {
                                if (DateTime.TryParse(data, out DateTime parsedDate))
                                    context.Header.FileCreationDate = parsedDate;
                            }
                        }
                        else if (level == "2")
                        {
                            if (insideHeadSour && tag == "VERS")
                                context.Header.SoftwareVersion = data;
                            else if (insideHeadGedc && tag == "VERS")
                                context.Header.GedcomVersion = data;
                        }

                        if (insideHeadSour && level == "1")
                            context.Header.SourceSoftware = data;
                    }

                    // --- INDIVIDUALS PARSING SYSTEM ---
                    else if (currentSection == "INDI" && currentIndividual != null)
                    {
                        ProcessIndividualTag(currentIndividual, tag, data, ref currentSection, ref currentEvent);
                    }
                    else if (currentSection == "INDI_EVENT" && currentIndividual != null && currentEvent != null)
                    {
                        if (level == "1")
                        {
                            // Stepped out of event back to INDI — process this line as INDI-level
                            currentSection = "INDI";
                            currentEvent = null;
                            ProcessIndividualTag(currentIndividual, tag, data, ref currentSection, ref currentEvent);
                        }
                        else if (tag == "DATE")
                        {
                            currentEvent.Date = data;
                            if (currentEvent.Type == "BIRT") currentIndividual.BirthDate = data;
                            else if (currentEvent.Type == "DEAT") currentIndividual.DeathDate = data;
                        }
                        else if (tag == "PLAC")
                        {
                            currentEvent.Place = data;
                            if (currentEvent.Type == "BIRT") currentIndividual.BirthPlace = data;
                            else if (currentEvent.Type == "DEAT") currentIndividual.DeathPlace = data;
                        }
                        else if (tag == "TYPE") currentEvent.SubType = data;
                        else if (tag == "SOUR") AddSourceAssociation(currentEvent.SourceIds, data);
                        else if (tag == "NOTE") AddNoteAssociation(currentEvent.NoteIds, data);
                    }

                    // --- FAMILIES PARSING SYSTEM ---
                    else if (currentSection == "FAM" && currentFamily != null)
                    {
                        ProcessFamilyTag(currentFamily, tag, data, ref currentSection, ref currentEvent);
                    }
                    else if (currentSection == "FAM_EVENT" && currentFamily != null && currentEvent != null)
                    {
                        if (level == "1")
                        {
                            // Stepped out of event back to FAM — process this line as FAM-level
                            currentSection = "FAM";
                            currentEvent = null;
                            ProcessFamilyTag(currentFamily, tag, data, ref currentSection, ref currentEvent);
                        }
                        else if (tag == "DATE")
                        {
                            currentEvent.Date = data;
                            if (currentEvent.Type == "MARR") currentFamily.MarriageDate = data;
                        }
                        else if (tag == "PLAC")
                        {
                            currentEvent.Place = data;
                            if (currentEvent.Type == "MARR") currentFamily.MarriagePlace = data;
                        }
                        else if (tag == "TYPE") currentEvent.SubType = data;
                        else if (tag == "SOUR") AddSourceAssociation(currentEvent.SourceIds, data);
                        else if (tag == "NOTE") AddNoteAssociation(currentEvent.NoteIds, data);
                    }

                    // --- SOURCES PARSING SYSTEM ---
                    else if (currentSection == "SOUR" && currentSource != null)
                    {
                        if (tag == "TITL") currentSource.Title = data;
                        else if (tag == "AUTH") currentSource.Author = data;
                        else if (tag == "REPO") currentSource.RepositoryId = data;
                        else if (tag == "NOTE")
                        {
                            AddNoteAssociation(currentSource.NoteIds, data);
                        }
                    }

                    // --- REPOSITORIES PARSING SYSTEM ---
                    else if (currentSection == "REPO" && currentRepository != null)
                    {
                        if (level == "1" && tag == "NAME")
                        {
                            currentRepository.Name = data;
                            currentRepoField = "NAME";
                        }
                        else if (level == "1" && tag == "ADDR")
                        {
                            currentRepository.Address = data;
                            currentRepoField = "ADDR";
                        }
                        else if (level == "1" && tag == "NOTE")
                        {
                            AddNoteAssociation(currentRepository.NoteIds, data);
                        }
                        else if (level == "2" && tag == "CONC")
                        {
                            if (currentRepoField == "NAME")
                                currentRepository.Name += data;
                            else if (currentRepoField == "ADDR")
                                currentRepository.Address += data;
                        }
                        else if (level == "2" && tag == "ADR1")
                        {
                            if (!string.IsNullOrEmpty(currentRepository.Address))
                                currentRepository.Address += " " + data;
                            else
                                currentRepository.Address = data;
                        }
                        else if (level == "3" && tag == "CONC")
                        {
                            // Level 3 CONC under ADR1 also belongs to the address
                            currentRepository.Address += data;
                        }
                    }

                    // --- NOTES PARSING SYSTEM ---
                    else if (currentSection == "NOTE" && currentNote != null)
                    {
                        if (tag == "CONC")
                        {
                            // CONC concatenates directly without a separator
                            currentNote.Text += data;
                        }
                        else if (tag == "CONT")
                        {
                            // CONT starts a new line
                            currentNote.Text += "\n" + data;
                        }
                    }
                }
            }
            return context;
        }

        /// <summary>
        /// Processes a level-1 tag within an individual record. Handles name, sex,
        /// event tags (which create a new <see cref="GedcomEvent"/> and switch to
        /// the event sub-section), and source/note associations.
        /// </summary>
        private static void ProcessIndividualTag(IndividualNode person, string tag, string data,
            ref string currentSection, ref GedcomEvent? currentEvent)
        {
            if (tag == "NAME")
            {
                person.FullName = data.Replace("/", "").Trim();
                if (data.Contains("/"))
                {
                    int start = data.IndexOf('/') + 1;
                    int end = data.LastIndexOf('/');
                    if (end > start)
                        person.LastName = data.Substring(start, end - start).Trim();
                }
                if (string.IsNullOrEmpty(person.LastName))
                    person.LastName = "Unknown";
            }
            else if (tag == "SEX") person.Gender = data;
            else if (EventTags.Contains(tag))
            {
                currentEvent = new GedcomEvent { Type = tag, Data = data };
                person.Events.Add(currentEvent);
                currentSection = "INDI_EVENT";
            }
            else if (tag == "SOUR") AddSourceAssociation(person.SourceIds, data);
            else if (tag == "NOTE") AddNoteAssociation(person.NoteIds, data);
        }

        /// <summary>
        /// Processes a level-1 tag within a family record. Handles spouse/child
        /// references, event tags (which create a new <see cref="GedcomEvent"/> and
        /// switch to the event sub-section), and source/note associations.
        /// </summary>
        private static void ProcessFamilyTag(FamilyNode family, string tag, string data,
            ref string currentSection, ref GedcomEvent? currentEvent)
        {
            if (tag == "HUSB") family.HusbandId = data;
            else if (tag == "WIFE") family.WifeId = data;
            else if (tag == "CHIL") family.ChildrenIds.Add(data);
            else if (EventTags.Contains(tag))
            {
                currentEvent = new GedcomEvent { Type = tag, Data = data };
                family.Events.Add(currentEvent);
                currentSection = "FAM_EVENT";
            }
            else if (tag == "SOUR") AddSourceAssociation(family.SourceIds, data);
            else if (tag == "NOTE") AddNoteAssociation(family.NoteIds, data);
        }

        /// <summary>
        /// Adds a note cross-reference ID (e.g., "@N1@") to an association list.
        /// Ignores inline note text since only pointer references are tracked here.
        /// </summary>
        private static void AddNoteAssociation(List<string> noteIds, string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return;
            if (data.StartsWith("@") && data.EndsWith("@"))
                noteIds.Add(data);
        }

        /// <summary>
        /// Adds a source cross-reference ID (e.g., "@S1@") to an association list.
        /// </summary>
        private static void AddSourceAssociation(List<string> sourceIds, string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return;
            if (data.StartsWith("@") && data.EndsWith("@"))
                sourceIds.Add(data);
        }
    }
}