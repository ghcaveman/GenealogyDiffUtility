using System;
using System.Collections.Generic;
using System.Linq;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Shared helpers for computing stable keys and children for tree nodes.
    /// Used by both the ViewModel (path matching) and the UI (container lookup).
    /// </summary>
    internal static class TreeNodeKeys
    {
        public static string? GetKey(object? node)
        {
            return node switch
            {
                TreeGroupNode g => g.Key,
                SurnameGroupNode s => "Surname:" + s.Surname,
                IndividualNode i => "Individual:" + BuildPersonKey(i),
                FamilyNode f => "Family:" + f.Id,
                SourceNode s => "Source:" + s.Id,
                RepositoryNode r => "Repository:" + r.Id,
                NoteNode n => "Note:" + n.Id,
                GedcomHeader => "Header",
                _ => null
            };
        }

        /// <summary>
        /// Builds a stable identity key for an individual that is independent of the
        /// GEDCOM file's record numbering (which can differ between exports, e.g.
        /// @I0000@ vs @I00001@ for the same person).
        /// <para>
        /// Primary key: normalized full name + birth date (this is unique in the
        /// sample data). For records without a birth date we fall back to the
        /// normalized name alone, qualified with a "#n" collision suffix when the
        /// name is shared by multiple individuals in the same tree.
        /// </para>
        /// </summary>
        private static string BuildPersonKey(IndividualNode person)
        {
            string name = NormalizeName(person.FullName);
            string birth = NormalizeForKey(person.BirthDate);

            if (!string.IsNullOrEmpty(birth))
            {
                return $"{name}|{birth}";
            }

            // Records without a birth date are keyed by name alone. If multiple
            // people share the name, the LoadTree pass assigns a "#2", "#3", ... suffix.
            int collision = person.CollisionIndex;
            return collision > 1 ? $"{name}#{collision}" : name;
        }

        /// <summary>
        /// Normalizes a person's full name for use as part of a stable identity key:
        /// trims, collapses whitespace, and lower-cases so that cosmetic differences
        /// between two GEDCOM exports do not break matching.
        /// </summary>
        internal static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts).ToLowerInvariant();
        }

        /// <summary>
        /// Normalizes a date for use as part of a stable identity key.
        /// "24 AUG 1938" and "24 Aug 1938" should match; qualifiers such as
        /// "ABT" are kept so approximate vs exact dates are not conflated.
        /// </summary>
        private static string NormalizeForKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts).ToLowerInvariant();
        }

        public static IEnumerable<object>? GetChildren(object? node)
        {
            return node switch
            {
                TreeGroupNode g => g.Children,
                SurnameGroupNode s => s.People.Cast<object>(),
                _ => null
            };
        }
    }
}