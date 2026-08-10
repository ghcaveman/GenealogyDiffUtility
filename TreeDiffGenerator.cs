using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Generates a plain-text comparison report between two parsed GEDCOM trees.
    /// Reports matches, records missing from the other tree, and records that
    /// exist only in the other tree, with roll-up counts per category and an
    /// overall score. When details are enabled, individual and family events
    /// and sources are also compared.
    /// </summary>
    internal static class TreeDiffGenerator
    {
        public static string Generate(GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, bool includeDetails)
        {
            var sb = new StringBuilder();
            int totalMatches = 0, totalMissing = 0, totalExtra = 0;

            AppendIndividuals(sb, thisCtx, otherCtx, includeDetails, ref totalMatches, ref totalMissing, ref totalExtra);
            AppendFamilies(sb, thisCtx, otherCtx, includeDetails, ref totalMatches, ref totalMissing, ref totalExtra);
            AppendSources(sb, thisCtx, otherCtx, ref totalMatches, ref totalMissing, ref totalExtra);
            AppendRepositories(sb, thisCtx, otherCtx, ref totalMatches, ref totalMissing, ref totalExtra);

            sb.AppendLine();
            sb.AppendLine("=== Overall Score ===");
            int total = totalMatches + totalMissing + totalExtra;
            if (total == 0)
            {
                sb.AppendLine("No data to compare.");
            }
            else
            {
                sb.AppendLine($"Total elements: {total}");
                sb.AppendLine($"Matches: {totalMatches} ({100.0 * totalMatches / total:F1}%)");
                sb.AppendLine($"Missing: {totalMissing} ({100.0 * totalMissing / total:F1}%)");
                sb.AppendLine($"Extra: {totalExtra} ({100.0 * totalExtra / total:F1}%)");
            }

            return sb.ToString();
        }

        private static void AppendIndividuals(StringBuilder sb, GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, bool includeDetails, ref int totalMatches, ref int totalMissing, ref int totalExtra)
        {
            sb.AppendLine("=== Individuals ===");
            var otherByKey = otherCtx.Individuals.Values
                .GroupBy(i => TreeNodeKeys.GetKey(i)!)
                .ToDictionary(g => g.Key, g => g.First());
            var matchedOtherKeys = new HashSet<string>();

            int matches = 0, missing = 0;
            foreach (var person in thisCtx.Individuals.Values.OrderBy(p => p.DisplayName).ThenBy(p => p.Id))
            {
                var key = TreeNodeKeys.GetKey(person)!;
                if (otherByKey.TryGetValue(key, out var otherPerson))
                {
                    matchedOtherKeys.Add(key);
                    matches++;
                    if (includeDetails)
                        AppendPersonDetails(sb, person, otherPerson, thisCtx, otherCtx, ref totalMatches, ref totalMissing, ref totalExtra);
                }
                else
                {
                    missing++;
                    sb.AppendLine($"  [MISSING] {person.DisplayName}");
                    if (includeDetails)
                        AppendPersonDetailsMissing(sb, person, thisCtx, ref totalMissing);
                }
            }

            int extra = 0;
            foreach (var otherPerson in otherCtx.Individuals.Values.OrderBy(p => p.DisplayName).ThenBy(p => p.Id))
            {
                if (!matchedOtherKeys.Contains(TreeNodeKeys.GetKey(otherPerson)!))
                {
                    extra++;
                    sb.AppendLine($"  [EXTRA] {otherPerson.DisplayName}");
                }
            }

            sb.AppendLine($"Matches: {matches} | Missing: {missing} | Extra: {extra}");
            totalMatches += matches;
            totalMissing += missing;
            totalExtra += extra;
        }

        private static void AppendFamilies(StringBuilder sb, GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, bool includeDetails, ref int totalMatches, ref int totalMissing, ref int totalExtra)
        {
            sb.AppendLine();
            sb.AppendLine("=== Families ===");
            var otherByKey = otherCtx.Families.Values
                .GroupBy(f => TreeNodeKeys.GetKey(f)!)
                .ToDictionary(g => g.Key, g => g.First());
            var matchedOtherKeys = new HashSet<string>();

            int matches = 0, missing = 0;
            foreach (var family in thisCtx.Families.Values.OrderBy(f => f.DisplayName).ThenBy(f => f.Id))
            {
                var key = TreeNodeKeys.GetKey(family)!;
                if (otherByKey.TryGetValue(key, out var otherFamily))
                {
                    matchedOtherKeys.Add(key);
                    matches++;
                    if (includeDetails)
                        AppendFamilyDetails(sb, family, otherFamily, thisCtx, otherCtx, ref totalMatches, ref totalMissing, ref totalExtra);
                }
                else
                {
                    missing++;
                    sb.AppendLine($"  [MISSING] {family.DisplayName}");
                    if (includeDetails)
                        AppendFamilyDetailsMissing(sb, family, thisCtx, ref totalMissing);
                }
            }

            int extra = 0;
            foreach (var otherFamily in otherCtx.Families.Values.OrderBy(f => f.DisplayName).ThenBy(f => f.Id))
            {
                if (!matchedOtherKeys.Contains(TreeNodeKeys.GetKey(otherFamily)!))
                {
                    extra++;
                    sb.AppendLine($"  [EXTRA] {otherFamily.DisplayName}");
                }
            }

            sb.AppendLine($"Matches: {matches} | Missing: {missing} | Extra: {extra}");
            totalMatches += matches;
            totalMissing += missing;
            totalExtra += extra;
        }

        private static void AppendSources(StringBuilder sb, GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, ref int totalMatches, ref int totalMissing, ref int totalExtra)
        {
            sb.AppendLine();
            sb.AppendLine("=== Sources ===");
            var otherByKey = otherCtx.Sources.Values
                .GroupBy(s => TreeNodeKeys.GetKey(s)!)
                .ToDictionary(g => g.Key, g => g.First());
            var matchedOtherKeys = new HashSet<string>();

            int matches = 0, missing = 0;
            foreach (var source in thisCtx.Sources.Values.OrderBy(s => s.Title).ThenBy(s => s.Id))
            {
                var key = TreeNodeKeys.GetKey(source)!;
                if (otherByKey.TryGetValue(key, out _))
                {
                    matchedOtherKeys.Add(key);
                    matches++;
                }
                else
                {
                    missing++;
                    sb.AppendLine($"  [MISSING] {source.Title}");
                }
            }

            int extra = 0;
            foreach (var otherSource in otherCtx.Sources.Values.OrderBy(s => s.Title).ThenBy(s => s.Id))
            {
                if (!matchedOtherKeys.Contains(TreeNodeKeys.GetKey(otherSource)!))
                {
                    extra++;
                    sb.AppendLine($"  [EXTRA] {otherSource.Title}");
                }
            }

            sb.AppendLine($"Matches: {matches} | Missing: {missing} | Extra: {extra}");
            totalMatches += matches;
            totalMissing += missing;
            totalExtra += extra;
        }

        private static void AppendRepositories(StringBuilder sb, GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, ref int totalMatches, ref int totalMissing, ref int totalExtra)
        {
            sb.AppendLine();
            sb.AppendLine("=== Repositories ===");
            var otherByKey = otherCtx.Repositories.Values
                .GroupBy(r => TreeNodeKeys.GetKey(r)!)
                .ToDictionary(g => g.Key, g => g.First());
            var matchedOtherKeys = new HashSet<string>();

            int matches = 0, missing = 0;
            foreach (var repo in thisCtx.Repositories.Values.OrderBy(r => r.Name).ThenBy(r => r.Id))
            {
                var key = TreeNodeKeys.GetKey(repo)!;
                if (otherByKey.TryGetValue(key, out _))
                {
                    matchedOtherKeys.Add(key);
                    matches++;
                }
                else
                {
                    missing++;
                    sb.AppendLine($"  [MISSING] {repo.Name}");
                }
            }

            int extra = 0;
            foreach (var otherRepo in otherCtx.Repositories.Values.OrderBy(r => r.Name).ThenBy(r => r.Id))
            {
                if (!matchedOtherKeys.Contains(TreeNodeKeys.GetKey(otherRepo)!))
                {
                    extra++;
                    sb.AppendLine($"  [EXTRA] {otherRepo.Name}");
                }
            }

            sb.AppendLine($"Matches: {matches} | Missing: {missing} | Extra: {extra}");
            totalMatches += matches;
            totalMissing += missing;
            totalExtra += extra;
        }

        private static void AppendPersonDetails(StringBuilder sb, IndividualNode person, IndividualNode otherPerson, GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, ref int totalMatches, ref int totalMissing, ref int totalExtra)
        {
            // Events
            var otherEvents = otherPerson.Events.Where(e => !e.IsInternal).ToDictionary(e => EventKey(e));
            var matchedEventKeys = new HashSet<string>();
            foreach (var evt in person.Events.Where(e => !e.IsInternal))
            {
                var key = EventKey(evt);
                if (otherEvents.ContainsKey(key))
                {
                    matchedEventKeys.Add(key);
                    totalMatches++;
                }
                else
                {
                    totalMissing++;
                    sb.AppendLine($"      [MISSING] Event: {evt.DisplayName}");
                }
            }
            foreach (var evt in otherPerson.Events.Where(e => !e.IsInternal))
            {
                if (!matchedEventKeys.Contains(EventKey(evt)))
                {
                    totalExtra++;
                    sb.AppendLine($"      [EXTRA] Event: {evt.DisplayName}");
                }
            }

            // Sources (direct + event-level)
            var thisSources = ResolveAllSources(person.SourceIds, person.Events, thisCtx).ToList();
            var otherSources = ResolveAllSources(otherPerson.SourceIds, otherPerson.Events, otherCtx).ToList();
            var otherSourceKeys = otherSources.Select(s => TreeNodeKeys.GetKey(s)!).ToHashSet();
            var matchedSourceKeys = new HashSet<string>();
            foreach (var src in thisSources)
            {
                var key = TreeNodeKeys.GetKey(src)!;
                if (otherSourceKeys.Contains(key))
                {
                    matchedSourceKeys.Add(key);
                    totalMatches++;
                }
                else
                {
                    totalMissing++;
                    sb.AppendLine($"      [MISSING] Source: {src.Title}");
                }
            }
            foreach (var src in otherSources)
            {
                var key = TreeNodeKeys.GetKey(src)!;
                if (!matchedSourceKeys.Contains(key))
                {
                    totalExtra++;
                    sb.AppendLine($"      [EXTRA] Source: {src.Title}");
                }
            }
        }

        private static void AppendPersonDetailsMissing(StringBuilder sb, IndividualNode person, GedcomTreeContext thisCtx, ref int totalMissing)
        {
            foreach (var evt in person.Events.Where(e => !e.IsInternal))
            {
                totalMissing++;
                sb.AppendLine($"      [MISSING] Event: {evt.DisplayName}");
            }
            foreach (var src in ResolveAllSources(person.SourceIds, person.Events, thisCtx))
            {
                totalMissing++;
                sb.AppendLine($"      [MISSING] Source: {src.Title}");
            }
        }

        private static void AppendFamilyDetails(StringBuilder sb, FamilyNode family, FamilyNode otherFamily, GedcomTreeContext thisCtx, GedcomTreeContext otherCtx, ref int totalMatches, ref int totalMissing, ref int totalExtra)
        {
            // Events
            var otherEvents = otherFamily.Events.Where(e => !e.IsInternal).ToDictionary(e => EventKey(e));
            var matchedEventKeys = new HashSet<string>();
            foreach (var evt in family.Events.Where(e => !e.IsInternal))
            {
                var key = EventKey(evt);
                if (otherEvents.ContainsKey(key))
                {
                    matchedEventKeys.Add(key);
                    totalMatches++;
                }
                else
                {
                    totalMissing++;
                    sb.AppendLine($"      [MISSING] Event: {evt.DisplayName}");
                }
            }
            foreach (var evt in otherFamily.Events.Where(e => !e.IsInternal))
            {
                if (!matchedEventKeys.Contains(EventKey(evt)))
                {
                    totalExtra++;
                    sb.AppendLine($"      [EXTRA] Event: {evt.DisplayName}");
                }
            }

            // Sources (direct + event-level)
            var thisSources = ResolveAllSources(family.SourceIds, family.Events, thisCtx).ToList();
            var otherSources = ResolveAllSources(otherFamily.SourceIds, otherFamily.Events, otherCtx).ToList();
            var otherSourceKeys = otherSources.Select(s => TreeNodeKeys.GetKey(s)!).ToHashSet();
            var matchedSourceKeys = new HashSet<string>();
            foreach (var src in thisSources)
            {
                var key = TreeNodeKeys.GetKey(src)!;
                if (otherSourceKeys.Contains(key))
                {
                    matchedSourceKeys.Add(key);
                    totalMatches++;
                }
                else
                {
                    totalMissing++;
                    sb.AppendLine($"      [MISSING] Source: {src.Title}");
                }
            }
            foreach (var src in otherSources)
            {
                var key = TreeNodeKeys.GetKey(src)!;
                if (!matchedSourceKeys.Contains(key))
                {
                    totalExtra++;
                    sb.AppendLine($"      [EXTRA] Source: {src.Title}");
                }
            }
        }

        private static void AppendFamilyDetailsMissing(StringBuilder sb, FamilyNode family, GedcomTreeContext thisCtx, ref int totalMissing)
        {
            foreach (var evt in family.Events.Where(e => !e.IsInternal))
            {
                totalMissing++;
                sb.AppendLine($"      [MISSING] Event: {evt.DisplayName}");
            }
            foreach (var src in ResolveAllSources(family.SourceIds, family.Events, thisCtx))
            {
                totalMissing++;
                sb.AppendLine($"      [MISSING] Source: {src.Title}");
            }
        }

        private static IEnumerable<SourceNode> ResolveAllSources(IEnumerable<string> directIds, IEnumerable<GedcomEvent> events, GedcomTreeContext ctx)
        {
            var ids = new HashSet<string>(directIds);
            foreach (var evt in events)
            {
                foreach (var sid in evt.SourceIds)
                {
                    ids.Add(sid);
                }
            }
            foreach (var id in ids)
            {
                if (ctx.Sources.TryGetValue(id, out var src))
                {
                    yield return src;
                }
            }
        }

        private static string EventKey(GedcomEvent e) =>
            $"{e.Type}|{e.SubType}|{Normalize(e.Date)}|{Normalize(e.Place)}|{Normalize(e.Data)}";

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts).ToLowerInvariant();
        }
    }
}