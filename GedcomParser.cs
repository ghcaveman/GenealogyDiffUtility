using System.IO;

namespace GenealogyDiffUtility
{
    internal static class GedcomParser
    {
        public static GedcomTreeContext Parse(string filePath)
        {
            var context = new GedcomTreeContext();

            if (!File.Exists(filePath)) return context;

            // Basic stream block to parse out core header parameters safely
            using (var reader = new StreamReader(filePath))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("1 SOUR"))
                        context.Header.SourceSoftware = trimmed.Replace("1 SOUR", "").Trim();
                    else if (trimmed.StartsWith("1 CHAR"))
                        context.Header.CharacterEncoding = trimmed.Replace("1 CHAR", "").Trim();
                    else if (trimmed.StartsWith("2 VERS") && context.Header.GedcomVersion == "5.5.1")
                        context.Header.GedcomVersion = trimmed.Replace("2 VERS", "").Trim();
                }
            }

            return context;
        }
    }
}
