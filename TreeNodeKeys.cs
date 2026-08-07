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
                FamilyNode f => "Family:" + BuildFamilyKey(f),
                SourceNode s => "Source:" + BuildSourceKey(s),
                RepositoryNode r => "Repository:" + BuildRepositoryKey(r),
                NoteNode n => "Note:" + BuildNoteKey(n),
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
        /// Builds a stable identity key for a family that is independent of the
        /// GEDCOM file's record numbering. The key is derived from the resolved
        /// husband and wife identities (which themselves use stable person keys)
        /// plus the marriage date, so that the same couple in two different
        /// exports produces the same family key.
        /// </summary>
        private static string BuildFamilyKey(FamilyNode family)
        {
            string husbandKey = family.Husband != null ? BuildPersonKey(family.Husband) : string.Empty;
            string wifeKey = family.Wife != null ? BuildPersonKey(family.Wife) : string.Empty;

            // If neither spouse could be resolved, fall back to the GEDCOM record Id
            if (string.IsNullOrEmpty(husbandKey) && string.IsNullOrEmpty(wifeKey))
            {
                return family.Id;
            }

            string marriageKey = NormalizeForKey(family.MarriageDate);
            return $"{husbandKey}|{wifeKey}|{marriageKey}";
        }

        /// <summary>
        /// Builds a stable identity key for a source based on its descriptive
        /// content (title, author, publication info) rather than the GEDCOM
        /// record Id, which can differ between exports.
        /// </summary>
        private static string BuildSourceKey(SourceNode source)
        {
            return $"{NormalizeName(source.Title)}|{NormalizeName(source.Author)}|{NormalizeName(source.PublicationInfo)}";
        }

        /// <summary>
        /// Builds a stable identity key for a repository based on its name and
        /// address rather than the GEDCOM record Id, which can differ between exports.
        /// </summary>
        private static string BuildRepositoryKey(RepositoryNode repository)
        {
            return $"{NormalizeName(repository.Name)}|{NormalizeName(repository.Address)}";
        }

        /// <summary>
        /// Builds a stable identity key for a note based on its text content
        /// rather than the GEDCOM record Id, which can differ between exports.
        /// </summary>
        private static string BuildNoteKey(NoteNode note)
        {
            return NormalizeName(note.Text);
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