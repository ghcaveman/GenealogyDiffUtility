using System;
using System.Collections.Generic;
using System.IO;

namespace GenealogyDiffUtility
{
    internal static class GedcomParser
    {
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
                        if (tag == "NAME")
                        {
                            currentIndividual.FullName = data.Replace("/", "").Trim();
                            if (data.Contains("/"))
                            {
                                int start = data.IndexOf('/') + 1;
                                int end = data.LastIndexOf('/');
                                if (end > start)
                                    currentIndividual.LastName = data.Substring(start, end - start).Trim();
                            }
                            if (string.IsNullOrEmpty(currentIndividual.LastName))
                                currentIndividual.LastName = "Unknown";
                        }
                        else if (tag == "SEX") currentIndividual.Gender = data;
                        else if (tag == "BIRT") currentSection = "INDI_BIRT";
                        else if (tag == "DEAT") currentSection = "INDI_DEAT";
                        else if (tag == "NOTE")
                        {
                            AddNoteAssociation(currentIndividual.NoteIds, data);
                        }
                    }
                    else if (currentSection == "INDI_BIRT" && currentIndividual != null)
                    {
                        if (level == "1")
                        {
                            currentSection = "INDI";
                            // Re-process line if it stepped out of BIRT back into INDI
                            if (tag == "DEAT") currentSection = "INDI_DEAT";
                        }
                        else if (tag == "DATE") currentIndividual.BirthDate = data;
                        else if (tag == "PLAC") currentIndividual.BirthPlace = data;
                        else if (tag == "NOTE")
                        {
                            AddNoteAssociation(currentIndividual.NoteIds, data);
                        }
                    }
                    else if (currentSection == "INDI_DEAT" && currentIndividual != null)
                    {
                        if (level == "1")
                        {
                            currentSection = "INDI";
                            // Re-process line if it stepped out of DEAT back into INDI
                            if (tag == "BIRT") currentSection = "INDI_BIRT";
                        }
                        else if (tag == "DATE") currentIndividual.DeathDate = data;
                        else if (tag == "PLAC") currentIndividual.DeathPlace = data;
                        else if (tag == "NOTE")
                        {
                            AddNoteAssociation(currentIndividual.NoteIds, data);
                        }
                    }
                    else if (currentSection == "FAM" && currentFamily != null)
                    {
                        if (tag == "HUSB") currentFamily.HusbandId = data;
                        else if (tag == "WIFE") currentFamily.WifeId = data;
                        else if (tag == "CHIL") currentFamily.ChildrenIds.Add(data);
                        else if (tag == "MARR") currentSection = "FAM_MARR";
                        else if (tag == "NOTE")
                        {
                            AddNoteAssociation(currentFamily.NoteIds, data);
                        }
                    }
                    else if (currentSection == "FAM_MARR" && currentFamily != null)
                    {
                        if (level == "2")
                        {
                            if (tag == "DATE") currentFamily.MarriageDate = data;
                            else if (tag == "PLAC") currentFamily.MarriagePlace = data;
                        }
                        else if (level == "1")
                        {
                            // Stepped out of MARR back into FAM — process the FAM-level tags on this line
                            currentSection = "FAM";
                            if (tag == "HUSB") currentFamily.HusbandId = data;
                            else if (tag == "WIFE") currentFamily.WifeId = data;
                            else if (tag == "CHIL") currentFamily.ChildrenIds.Add(data);
                            else if (tag == "MARR") currentSection = "FAM_MARR";
                            else if (tag == "NOTE")
                            {
                                AddNoteAssociation(currentFamily.NoteIds, data);
                            }
                        }
                    }
                    else if (currentSection == "SOUR" && currentSource != null)
                    {
                        if (tag == "TITL") currentSource.Title = data;
                        else if (tag == "AUTH") currentSource.Author = data;
                        else if (tag == "NOTE")
                        {
                            AddNoteAssociation(currentSource.NoteIds, data);
                        }
                    }
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
        /// Adds a note cross-reference ID (e.g., "@N1@") to an association list.
        /// Ignores inline note text since only pointer references are tracked here.
        /// </summary>
        private static void AddNoteAssociation(List<string> noteIds, string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return;
            if (data.StartsWith("@") && data.EndsWith("@"))
                noteIds.Add(data);
        }
    }
}
