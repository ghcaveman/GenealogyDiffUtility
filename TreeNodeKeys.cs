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
        /// name is shared by multiple individuals in the same tree (see
        /// <see cref="AssignCollisionIndices"/>).
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
        internal static string NormalizeForKey(string value) => NormalizeName(value);

        /// <summary>
        /// Numbers individuals who share a name and have no birth date, so their
        /// keys stay unique ("john doe", "john doe#2", ...).
        /// <para>
        /// The numbering follows each person's content (death date, then parents,
        /// spouses and children) rather than the GEDCOM record Id, because record
        /// Ids differ between exports. People with more known facts sort first, so
        /// when the other tree has only one person of that name, it pairs with the
        /// best-documented one. The record Id only breaks ties between people whose
        /// content is identical, where either pairing is equally valid.
        /// </para>
        /// </summary>
        public static void AssignCollisionIndices(GedcomTreeContext context)
        {
            foreach (var person in context.Individuals.Values)
            {
                person.CollisionIndex = 1;
            }

            var groups = context.Individuals.Values
                .Where(p => string.IsNullOrWhiteSpace(p.BirthDate))
                .GroupBy(p => NormalizeName(p.FullName))
                .Where(g => g.Count() > 1)
                .ToList();
            if (groups.Count == 0) return;

            var signatures = new Dictionary<IndividualNode, string[]>();
            foreach (var person in groups.SelectMany(g => g))
            {
                signatures[person] = BuildSignature(person, context);
            }

            foreach (var group in groups)
            {
                var ordered = group
                    .OrderBy(p => signatures[p], SignatureComparer.Instance)
                    .ThenBy(p => p.Id, StringComparer.Ordinal)
                    .ToList();
                for (int i = 0; i < ordered.Count; i++)
                {
                    ordered[i].CollisionIndex = i + 1;
                }
            }
        }

        /// <summary>
        /// Facts that tell same-named people apart, most decisive first:
        /// death date, father, mother, spouses, children.
        /// </summary>
        private static string[] BuildSignature(IndividualNode person, GedcomTreeContext context)
        {
            string father = string.Empty, mother = string.Empty;
            var spouses = new List<string>();
            var children = new List<string>();

            foreach (var family in context.Families.Values)
            {
                if (father.Length == 0 && mother.Length == 0 && family.ChildrenIds.Contains(person.Id))
                {
                    father = BasePersonKey(family.HusbandId, context);
                    mother = BasePersonKey(family.WifeId, context);
                }

                bool isHusband = family.HusbandId == person.Id;
                bool isWife = family.WifeId == person.Id;
                if (!isHusband && !isWife) continue;

                string spouse = BasePersonKey(isHusband ? family.WifeId : family.HusbandId, context);
                if (spouse.Length > 0) spouses.Add(spouse);
                children.AddRange(family.ChildrenIds
                    .Select(id => BasePersonKey(id, context))
                    .Where(k => k.Length > 0));
            }

            spouses.Sort(StringComparer.Ordinal);
            children.Sort(StringComparer.Ordinal);
            return new[]
            {
                NormalizeForKey(person.DeathDate),
                father,
                mother,
                string.Join(";", spouses),
                string.Join(";", children)
            };
        }

        /// <summary>
        /// Name plus birth date of a related person, without any collision suffix
        /// (relatives' own suffixes may not be assigned yet).
        /// </summary>
        private static string BasePersonKey(string id, GedcomTreeContext context)
        {
            if (string.IsNullOrEmpty(id) || !context.Individuals.TryGetValue(id, out var p)) return string.Empty;
            string name = NormalizeName(p.FullName);
            string birth = NormalizeForKey(p.BirthDate);
            return birth.Length > 0 ? $"{name}|{birth}" : name;
        }

        /// <summary>
        /// Compares signatures field by field; a known value sorts before an
        /// unknown (empty) one.
        /// </summary>
        private sealed class SignatureComparer : IComparer<string[]>
        {
            public static readonly SignatureComparer Instance = new();

            public int Compare(string[]? x, string[]? y)
            {
                if (x == null || y == null) return x == null ? (y == null ? 0 : 1) : -1;
                for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
                {
                    bool xEmpty = x[i].Length == 0, yEmpty = y[i].Length == 0;
                    if (xEmpty != yEmpty) return xEmpty ? 1 : -1;
                    int c = string.CompareOrdinal(x[i], y[i]);
                    if (c != 0) return c;
                }
                return x.Length.CompareTo(y.Length);
            }
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