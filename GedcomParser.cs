using System.IO;

namespace GenealogyDiffUtility
{
    internal static class GedcomParser
    {
        public static GedcomTreeContext Parse(string filePath)
        {
            var context = new GedcomTreeContext();
            if (!File.Exists(filePath)) return context;

            IndividualNode? currentIndividual = null;
            FamilyNode? currentFamily = null;
            SourceNode? currentSource = null;
            string currentSection = ""; // Tracks if we are in HEAD, INDI, FAM, etc.

            using (var reader = new StreamReader(filePath))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    // Split into maximum 3 parts: Level, Tag/ID, Value
                    string[] parts = trimmed.Split(' ', 3);
                    if (parts.Length < 2) continue;

                    string level = parts[0];

                    // Handle Top-Level Level 0 Records
                    if (level == "0")
                    {
                        // Reset all active contexts
                        currentIndividual = null;
                        currentFamily = null;
                        currentSource = null;

                        if (parts.Length == 2)
                        {
                            currentSection = parts[1]; // e.g., "0 HEAD" or "0 TRLR"
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
                        continue;
                    }

                    // Handle Sub-Level tags based on current level 0 context record
                    string tag = parts[1];
                    string data = parts.Length > 2 ? parts[2] : string.Empty;

                    if (currentSection == "HEAD")
                    {
                        if (tag == "SOUR") context.Header.SourceSoftware = data;
                        else if (tag == "CHAR") context.Header.CharacterEncoding = data;
                        else if (tag == "VERS" && trimmed.Contains("GEDC")) context.Header.GedcomVersion = data;
                    }
                    else if (currentSection == "INDI" && currentIndividual != null)
                    {
                        if (tag == "NAME")
                        {
                            currentIndividual.FullName = data.Replace("/", "").Trim();
                            // Simple surname extraction logic looking between slashes
                            if (data.Contains("/"))
                            {
                                int start = data.IndexOf('/') + 1;
                                int end = data.LastIndexOf('/');
                                if (end > start)
                                {
                                    currentIndividual.LastName = data.Substring(start, end - start).Trim();
                                }
                            }
                            if (string.IsNullOrEmpty(currentIndividual.LastName))
                            {
                                currentIndividual.LastName = "Unknown";
                            }
                        }
                        else if (tag == "SEX") currentIndividual.Gender = data;
                        else if (tag == "BIRT") currentSection = "INDI_BIRT";
                        else if (tag == "DEAT") currentSection = "INDI_DEAT";
                    }
                    else if (currentSection == "INDI_BIRT" && currentIndividual != null)
                    {
                        if (level == "1") // Popped back up out of birth context
                        {
                            currentSection = "INDI"; // Re-evaluate tag under parent context
                        }
                        else if (tag == "DATE") currentIndividual.BirthDate = data;
                        else if (tag == "PLAC") currentIndividual.BirthPlace = data;
                    }
                    else if (currentSection == "FAM" && currentFamily != null)
                    {
                        if (tag == "HUSB") currentFamily.HusbandId = data;
                        else if (tag == "WIFE") currentFamily.WifeId = data;
                        else if (tag == "CHIL") currentFamily.ChildrenIds.Add(data);
                    }
                    else if (currentSection == "SOUR" && currentSource != null)
                    {
                        if (tag == "TITL") currentSource.Title = data;
                        else if (tag == "AUTH") currentSource.Author = data;
                    }
                }
            }
            return context;
        }
    }
}
