using System;
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
            string currentSection = "";

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
