using System.Collections.Generic;
using System.Linq;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Shared helpers for turning tree rows into text (used by Copy and Print)
    /// and for walking the displayed tree, including detail sub-nodes.
    /// </summary>
    internal static class TreeNodeText
    {
        /// <summary>
        /// The text shown for a tree row. The header row produces several lines;
        /// every other row produces one (or more if its text contains line breaks).
        /// </summary>
        public static IEnumerable<string> GetLines(object item)
        {
            if (item is GedcomHeader header)
            {
                yield return $"Source Software: {header.SourceSoftware}";
                yield return $"Software Version: {header.SoftwareVersion}";
                yield return $"GEDCOM Version: {header.GedcomVersion}";
                yield return $"Character Encoding: {header.CharacterEncoding}";
                yield return $"Created Date: {header.FileCreationDate:yyyy-MM-dd}";
                yield break;
            }

            string text = item switch
            {
                TreeGroupNode g => g.Name,
                SurnameGroupNode s => s.DisplayName,
                SourceNode s => s.Title,
                RepositoryNode r => r.Name,
                _ => GetDisplayNameByReflection(item)
            };

            foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
            {
                yield return line;
            }
        }

        private static string GetDisplayNameByReflection(object item)
        {
            var type = item.GetType();
            var value = type.GetProperty("DisplayName")?.GetValue(item)
                        ?? type.GetProperty("Title")?.GetValue(item)
                        ?? type.GetProperty("Name")?.GetValue(item);
            return value?.ToString() ?? item.ToString() ?? string.Empty;
        }

        /// <summary>
        /// True for the lightweight detail rows (spouses, children, events, notes,
        /// references) that appear when Details is on.
        /// </summary>
        public static bool IsDetailRow(object item) => item is
            IndividualDetailNode or FamilyDetailNode or EventDetailNode or EventSourceDetailNode or
            SourceDetailNode or RepositoryDetailNode or NoteDetailNode;

        /// <summary>True for the top-level group rows (Header, Individuals, Families...).</summary>
        public static bool IsGroupRow(object item) => item is TreeGroupNode;

        /// <summary>
        /// The children shown under a row in the tree view, including detail rows.
        /// Mirrors the ItemsSource bindings in GenealogyControl.axaml.
        /// </summary>
        public static IEnumerable<object> GetDisplayedChildren(object item) => item switch
        {
            TreeGroupNode g => g.Children,
            SurnameGroupNode s => s.People.Cast<object>(),
            IndividualNode p => p.Details.Cast<object>(),
            FamilyNode f => f.Details.Cast<object>(),
            EventDetailNode e => e.Sources.Cast<object>(),
            SourceNode s => s.Details.Cast<object>(),
            RepositoryNode r => r.Details.Cast<object>(),
            NoteNode n => n.Details.Cast<object>(),
            _ => Enumerable.Empty<object>()
        };
    }
}
