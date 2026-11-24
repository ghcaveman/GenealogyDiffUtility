using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GenealogyDiffUtility
{
    /// <summary>
    /// Collected statistics evaluating citation and media health in a GEDCOM tree.
    /// </summary>
    internal sealed class CitationMediaHealthStats
    {
        public string Title { get; set; } = string.Empty;

        // Individuals & Families
        public int IndividualsCount { get; set; }
        public int SourcedIndividualsCount { get; set; }
        public int UnsourcedIndividualsCount => Math.Max(0, IndividualsCount - SourcedIndividualsCount);
        public double SourcedIndividualsPercent => IndividualsCount > 0 ? (100.0 * SourcedIndividualsCount / IndividualsCount) : 0.0;
        public double UnsourcedIndividualsPercent => IndividualsCount > 0 ? (100.0 * UnsourcedIndividualsCount / IndividualsCount) : 0.0;

        public int FamiliesCount { get; set; }
        public int SourcedFamiliesCount { get; set; }

        public int EventsCount { get; set; }
        public int SourcedEventsCount { get; set; }

        // Citations
        public int CitationsCount { get; set; }
        public int CitationsWithMediaCount { get; set; }
        public int CitationsWithoutMediaCount => Math.Max(0, CitationsCount - CitationsWithMediaCount);
        public double CitationsWithMediaPercent => CitationsCount > 0 ? (100.0 * CitationsWithMediaCount / CitationsCount) : 0.0;
        public double CitationsWithoutMediaPercent => CitationsCount > 0 ? (100.0 * CitationsWithoutMediaCount / CitationsCount) : 0.0;

        public int CitationsWithPageCount { get; set; }
        public int CitationsWithoutPageCount => Math.Max(0, CitationsCount - CitationsWithPageCount);
        public double CitationsWithPagePercent => CitationsCount > 0 ? (100.0 * CitationsWithPageCount / CitationsCount) : 0.0;
        public double CitationsWithoutPagePercent => CitationsCount > 0 ? (100.0 * CitationsWithoutPageCount / CitationsCount) : 0.0;

        public int CitationsWithDateCount { get; set; }
        public int CitationsWithQualityCount { get; set; }
        public int InlineCitationsCount { get; set; }
        public int DuplicateCitationsCount { get; set; }

        // Sources
        public int SourcesCount { get; set; }
        public int SourcesWithRepoCount { get; set; }
        public int SourcesWithoutRepoCount => Math.Max(0, SourcesCount - SourcesWithRepoCount);
        public double SourcesWithRepoPercent => SourcesCount > 0 ? (100.0 * SourcesWithRepoCount / SourcesCount) : 0.0;

        public int UnusedSourcesCount { get; set; }
        public int ConflatedSourcesCount { get; set; }
        public List<string> ConflatedSourceExamples { get; } = new();
        public int PlaceholderSourcesCount { get; set; }

        // Repositories
        public int RepositoriesCount { get; set; }
        public int RepositoriesWithAddressCount { get; set; }
        public int UnusedRepositoriesCount { get; set; }

        // Media (Media Fix metrics)
        public int MediaCount { get; set; }
        public int MediaFoundOnDiskCount { get; set; }
        public int MediaMissingOnDiskCount { get; set; }
        public bool CheckedDisk { get; set; }
        public double MediaFoundPercent => (CheckedDisk && MediaCount > 0) ? (100.0 * MediaFoundOnDiskCount / MediaCount) : 0.0;

        public int MediaRelativePathsCount { get; set; }
        public int MediaAbsolutePathsCount { get; set; }
        public double MediaRelativePercent => MediaCount > 0 ? (100.0 * MediaRelativePathsCount / MediaCount) : 0.0;
        public double MediaAbsolutePercent => MediaCount > 0 ? (100.0 * MediaAbsolutePathsCount / MediaCount) : 0.0;

        public int MediaOnCitationsCount { get; set; }
        public int MediaOnIndividualsCount { get; set; }
        public int MediaOnFamiliesCount { get; set; }
        public int MediaOnEventsCount { get; set; }
        public int OrphanedMediaCount { get; set; }
        public int DuplicateMediaFilesCount { get; set; }

        // Overall Score
        public int HealthScore { get; set; }
        public string Rating { get; set; } = string.Empty;
    }

    /// <summary>
    /// Analyzes a GEDCOM tree context and generates a Citation and Media Health report.
    /// </summary>
    internal static class CitationMediaHealthReport
    {
        private static readonly Regex UrlRegex = new(@"https?://|www\.", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RepoDomainRegex = new(@"familysearch|ancestry|find\s?a\s?grave|myheritage|newspapers\.com|fold3|national\s+archives", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PinpointRegex = new(@"\b(sheet|page|pg|pp|roll|film|image|frame|vol|volume|book|line|certificate\s+no)\b|#", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly HashSet<string> PlaceholderTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            "", "unknown", "untitled", "none", "n/a", "na", "source", "unspecified"
        };

        /// <summary>
        /// Analyzes the given tree context and returns structured health metrics.
        /// </summary>
        public static CitationMediaHealthStats Analyze(GedcomTreeContext context, string? title = null, string? baseDirectory = null)
        {
            var stats = new CitationMediaHealthStats
            {
                Title = title ?? "GEDCOM Tree",
                IndividualsCount = context.Individuals.Count,
                FamiliesCount = context.Families.Count,
                SourcesCount = context.Sources.Count,
                RepositoriesCount = context.Repositories.Count,
            };

            var usedSourceIds = new HashSet<string>(StringComparer.Ordinal);
            var allCitations = new List<GedcomCitation>();
            var allMediaPaths = new List<string>();
            var mediaReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Individuals & Person Events
            foreach (var person in context.Individuals.Values)
            {
                bool hasCitation = person.Citations.Count > 0 || person.SourceIds.Count > 0;
                allCitations.AddRange(person.Citations);
                allMediaPaths.AddRange(person.Media);
                stats.MediaOnIndividualsCount += person.Media.Count;

                foreach (var m in person.Media)
                {
                    mediaReferences.Add(CleanMediaPath(m));
                }

                foreach (var sId in person.SourceIds)
                {
                    if (!string.IsNullOrWhiteSpace(sId)) usedSourceIds.Add(sId);
                }

                foreach (var evt in person.Events)
                {
                    stats.EventsCount++;
                    if (evt.Citations.Count > 0 || evt.SourceIds.Count > 0)
                    {
                        stats.SourcedEventsCount++;
                        hasCitation = true;
                    }
                    allCitations.AddRange(evt.Citations);
                    allMediaPaths.AddRange(evt.Media);
                    stats.MediaOnEventsCount += evt.Media.Count;

                    foreach (var m in evt.Media)
                    {
                        mediaReferences.Add(CleanMediaPath(m));
                    }

                    foreach (var sId in evt.SourceIds)
                    {
                        if (!string.IsNullOrWhiteSpace(sId)) usedSourceIds.Add(sId);
                    }
                }

                if (hasCitation)
                {
                    stats.SourcedIndividualsCount++;
                }
            }

            // 2. Families & Family Events
            foreach (var fam in context.Families.Values)
            {
                bool hasCitation = fam.Citations.Count > 0 || fam.SourceIds.Count > 0;
                allCitations.AddRange(fam.Citations);
                allMediaPaths.AddRange(fam.Media);
                stats.MediaOnFamiliesCount += fam.Media.Count;

                foreach (var m in fam.Media)
                {
                    mediaReferences.Add(CleanMediaPath(m));
                }

                foreach (var sId in fam.SourceIds)
                {
                    if (!string.IsNullOrWhiteSpace(sId)) usedSourceIds.Add(sId);
                }

                foreach (var evt in fam.Events)
                {
                    stats.EventsCount++;
                    if (evt.Citations.Count > 0 || evt.SourceIds.Count > 0)
                    {
                        stats.SourcedEventsCount++;
                        hasCitation = true;
                    }
                    allCitations.AddRange(evt.Citations);
                    allMediaPaths.AddRange(evt.Media);
                    stats.MediaOnEventsCount += evt.Media.Count;

                    foreach (var m in evt.Media)
                    {
                        mediaReferences.Add(CleanMediaPath(m));
                    }

                    foreach (var sId in evt.SourceIds)
                    {
                        if (!string.IsNullOrWhiteSpace(sId)) usedSourceIds.Add(sId);
                    }
                }

                if (hasCitation)
                {
                    stats.SourcedFamiliesCount++;
                }
            }

            // 3. Citations Analysis
            stats.CitationsCount = allCitations.Count;
            var citationKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in allCitations)
            {
                if (!string.IsNullOrWhiteSpace(c.SourceId))
                {
                    usedSourceIds.Add(c.SourceId);
                }
                else if (!string.IsNullOrWhiteSpace(c.InlineText))
                {
                    stats.InlineCitationsCount++;
                }

                if (c.Media.Count > 0)
                {
                    stats.CitationsWithMediaCount++;
                    stats.MediaOnCitationsCount += c.Media.Count;
                    allMediaPaths.AddRange(c.Media);
                    foreach (var m in c.Media)
                    {
                        mediaReferences.Add(CleanMediaPath(m));
                    }
                }

                if (!string.IsNullOrWhiteSpace(c.Page))
                {
                    stats.CitationsWithPageCount++;
                }

                if (!string.IsNullOrWhiteSpace(c.Date))
                {
                    stats.CitationsWithDateCount++;
                }

                if (!string.IsNullOrWhiteSpace(c.Quality))
                {
                    stats.CitationsWithQualityCount++;
                }

                string key = $"{c.SourceId}|{c.InlineText}|{c.Page}".Trim();
                if (key.Length > 2)
                {
                    citationKeys.TryGetValue(key, out int count);
                    citationKeys[key] = count + 1;
                }
            }

            stats.DuplicateCitationsCount = citationKeys.Values.Where(count => count > 1).Sum(count => count - 1);

            // 4. Sources Analysis
            var usedRepoIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in context.Sources.Values)
            {
                if (!string.IsNullOrWhiteSpace(s.RepositoryId))
                {
                    stats.SourcesWithRepoCount++;
                    usedRepoIds.Add(s.RepositoryId);
                }

                if (!usedSourceIds.Contains(s.Id))
                {
                    stats.UnusedSourcesCount++;
                }

                string titleText = s.Title.Trim();
                if (PlaceholderTitles.Contains(titleText))
                {
                    stats.PlaceholderSourcesCount++;
                }

                // Conflation detection: title includes URLs, repositories, or page/pinpoint info
                bool hasUrl = UrlRegex.IsMatch(titleText) || UrlRegex.IsMatch(s.PublicationInfo);
                bool hasRepo = RepoDomainRegex.IsMatch(titleText);
                bool hasPinpoint = PinpointRegex.IsMatch(titleText);

                if ((hasUrl && hasRepo) || (hasRepo && hasPinpoint) || (hasUrl && hasPinpoint))
                {
                    stats.ConflatedSourcesCount++;
                    if (stats.ConflatedSourceExamples.Count < 3)
                    {
                        stats.ConflatedSourceExamples.Add(titleText);
                    }
                }
            }

            // 5. Repositories Analysis
            foreach (var r in context.Repositories.Values)
            {
                if (!string.IsNullOrWhiteSpace(r.Address))
                {
                    stats.RepositoriesWithAddressCount++;
                }
                if (!usedRepoIds.Contains(r.Id))
                {
                    stats.UnusedRepositoriesCount++;
                }
            }

            // 6. Media Analysis (Media Fix metrics)
            // Combine standalone context.Media and inline media
            var mediaFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fileCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in context.Media.Values)
            {
                string clean = CleanMediaPath(m.File);
                if (!string.IsNullOrWhiteSpace(clean))
                {
                    mediaFiles.Add(clean);
                    fileCounts.TryGetValue(clean, out int count);
                    fileCounts[clean] = count + 1;
                }

                if (!mediaReferences.Contains(m.Id) && !mediaReferences.Contains(clean))
                {
                    stats.OrphanedMediaCount++;
                }
            }

            foreach (var raw in allMediaPaths)
            {
                string clean = CleanMediaPath(raw);
                if (!string.IsNullOrWhiteSpace(clean) && !context.Media.ContainsKey(raw))
                {
                    mediaFiles.Add(clean);
                    fileCounts.TryGetValue(clean, out int count);
                    fileCounts[clean] = count + 1;
                }
            }

            stats.MediaCount = mediaFiles.Count;
            stats.DuplicateMediaFilesCount = fileCounts.Values.Where(c => c > 1).Sum(c => c - 1);

            stats.CheckedDisk = !string.IsNullOrEmpty(baseDirectory) && Directory.Exists(baseDirectory);

            foreach (var file in mediaFiles)
            {
                bool isAbs = IsAbsolutePath(file);
                if (isAbs)
                {
                    stats.MediaAbsolutePathsCount++;
                }
                else
                {
                    stats.MediaRelativePathsCount++;
                }

                if (stats.CheckedDisk)
                {
                    string fullPath = isAbs ? file : Path.Combine(baseDirectory!, file);
                    if (File.Exists(fullPath))
                    {
                        stats.MediaFoundOnDiskCount++;
                    }
                    else
                    {
                        stats.MediaMissingOnDiskCount++;
                    }
                }
            }

            // 7. Overall Score Calculation
            // Weights: Individuals Sourced (25%), Citation Images (20%), Pinpoint (20%), Repos (15%), Media Portability & Presence (20%)
            double points = 0.0;
            double weights = 0.0;

            if (stats.IndividualsCount > 0)
            {
                weights += 25.0;
                points += 25.0 * (stats.SourcedIndividualsCount / (double)stats.IndividualsCount);
            }

            if (stats.CitationsCount > 0)
            {
                weights += 20.0;
                points += 20.0 * (stats.CitationsWithMediaCount / (double)stats.CitationsCount);

                weights += 20.0;
                points += 20.0 * (stats.CitationsWithPageCount / (double)stats.CitationsCount);
            }

            if (stats.SourcesCount > 0)
            {
                weights += 15.0;
                points += 15.0 * (stats.SourcesWithRepoCount / (double)stats.SourcesCount);
            }

            if (stats.MediaCount > 0)
            {
                weights += 20.0;
                double mediaFactor = stats.CheckedDisk
                    ? ((stats.MediaFoundOnDiskCount / (double)stats.MediaCount * 0.6) + (stats.MediaRelativePathsCount / (double)stats.MediaCount * 0.4))
                    : (stats.MediaRelativePathsCount / (double)stats.MediaCount);
                points += 20.0 * mediaFactor;
            }

            stats.HealthScore = weights > 0 ? (int)Math.Round(points / weights * 100.0) : 100;

            if (stats.HealthScore >= 90) stats.Rating = "Excellent";
            else if (stats.HealthScore >= 75) stats.Rating = "Good";
            else if (stats.HealthScore >= 50) stats.Rating = "Fair (Needs Attention)";
            else stats.Rating = "Poor (Action Recommended)";

            return stats;
        }

        /// <summary>
        /// Generates a comprehensive plain-text health report for a single GEDCOM tree.
        /// </summary>
        public static string Generate(GedcomTreeContext context, string? fileName = null, string? baseDirectory = null)
        {
            var stats = Analyze(context, fileName, baseDirectory);
            var sb = new StringBuilder();

            sb.AppendLine("========================================================================");
            sb.AppendLine("               CITATION & MEDIA HEALTH REPORT");
            sb.AppendLine("========================================================================");
            sb.AppendLine($"File: {stats.Title}");
            sb.AppendLine($"Database Totals: {stats.IndividualsCount} people, {stats.FamiliesCount} families, {stats.SourcesCount} sources, {stats.RepositoriesCount} repositories, {stats.CitationsCount} citations, {stats.MediaCount} media items.");
            sb.AppendLine();

            // 1. Citation Health Highlights
            sb.AppendLine("--- Citation Health Highlights ---");
            if (stats.CitationsCount > 0)
            {
                sb.AppendLine($"* Citation Image Coverage: {stats.CitationsWithMediaCount} of {stats.CitationsCount} citations ({stats.CitationsWithMediaPercent:F1}%) have attached media/scans.");
                sb.AppendLine($"  ↳ Citations without images: {stats.CitationsWithoutMediaCount} of {stats.CitationsCount} ({stats.CitationsWithoutMediaPercent:F1}%).");
                sb.AppendLine($"* Pinpoint Detail Quality: {stats.CitationsWithPageCount} of {stats.CitationsCount} citations ({stats.CitationsWithPagePercent:F1}%) cite specific Volume/Page/Entry.");
                if (stats.CitationsWithoutPageCount > 0)
                {
                    sb.AppendLine($"  ↳ Citations lacking pinpoint detail: {stats.CitationsWithoutPageCount} of {stats.CitationsCount} ({stats.CitationsWithoutPagePercent:F1}%).");
                }
                if (stats.CitationsWithDateCount > 0 || stats.CitationsWithQualityCount > 0)
                {
                    sb.AppendLine($"* Evidence Metadata: {stats.CitationsWithDateCount} citations have dates; {stats.CitationsWithQualityCount} have confidence (QUAY) ratings.");
                }
                if (stats.DuplicateCitationsCount > 0)
                {
                    sb.AppendLine($"  ⚠ Redundant Citations: {stats.DuplicateCitationsCount} duplicate citations pointing to identical source & page.");
                }
            }
            else
            {
                sb.AppendLine("* Citation Image Coverage: No citations recorded in file.");
            }

            // 2. Individual Sourcing
            if (stats.IndividualsCount > 0)
            {
                sb.AppendLine($"* Individual Sourcing: {stats.SourcedIndividualsCount} of {stats.IndividualsCount} people ({stats.SourcedIndividualsPercent:F1}%) have at least one citation.");
                if (stats.UnsourcedIndividualsCount > 0)
                {
                    sb.AppendLine($"  ↳ Unsourced individuals: {stats.UnsourcedIndividualsCount} of {stats.IndividualsCount} ({stats.UnsourcedIndividualsPercent:F1}%).");
                }
            }

            // 3. Source & Repository Hygiene
            if (stats.SourcesCount > 0)
            {
                sb.AppendLine($"* Repository Linkage: {stats.SourcesWithRepoCount} of {stats.SourcesCount} sources ({stats.SourcesWithRepoPercent:F1}%) link to a repository.");
                if (stats.SourcesWithoutRepoCount > 0)
                {
                    sb.AppendLine($"  ↳ Sources missing repository link: {stats.SourcesWithoutRepoCount} of {stats.SourcesCount} ({100.0 - stats.SourcesWithRepoPercent:F1}%).");
                }
                if (stats.ConflatedSourcesCount > 0)
                {
                    sb.AppendLine($"  ⚠ Conflated Sources: {stats.ConflatedSourcesCount} sources mix repository, collection, or citation text.");
                    foreach (var example in stats.ConflatedSourceExamples)
                    {
                        sb.AppendLine($"    ↳ Example: \"{example}\"");
                    }
                }
                if (stats.PlaceholderSourcesCount > 0)
                {
                    sb.AppendLine($"  ⚠ Placeholder Sources: {stats.PlaceholderSourcesCount} sources have blank or \"Unknown\" titles.");
                }
                if (stats.UnusedSourcesCount > 0)
                {
                    sb.AppendLine($"  ↳ Unused Sources: {stats.UnusedSourcesCount} sources are never cited in the tree.");
                }
            }

            // 4. Media Health Highlights (from Media Fix)
            sb.AppendLine();
            sb.AppendLine("--- Media Health Highlights (from Media Fix) ---");
            if (stats.MediaCount > 0)
            {
                if (stats.CheckedDisk)
                {
                    sb.AppendLine($"* File Accessibility: {stats.MediaFoundOnDiskCount} of {stats.MediaCount} files ({stats.MediaFoundPercent:F1}%) verified on disk.");
                    if (stats.MediaMissingOnDiskCount > 0)
                    {
                        sb.AppendLine($"  ↳ Missing media files: {stats.MediaMissingOnDiskCount} of {stats.MediaCount} ({100.0 - stats.MediaFoundPercent:F1}%).");
                    }
                }

                sb.AppendLine($"* Path Portability: {stats.MediaRelativePathsCount} of {stats.MediaCount} media records ({stats.MediaRelativePercent:F1}%) use portable relative paths.");
                if (stats.MediaAbsolutePathsCount > 0)
                {
                    sb.AppendLine($"  ↳ Non-portable absolute paths: {stats.MediaAbsolutePathsCount} of {stats.MediaCount} ({stats.MediaAbsolutePercent:F1}%).");
                }

                sb.AppendLine($"* Media Placement: {stats.MediaOnCitationsCount} attached to citations (evidence); {stats.MediaOnIndividualsCount} on individuals; {stats.MediaOnEventsCount} on events.");
                if (stats.DuplicateMediaFilesCount > 0 || stats.OrphanedMediaCount > 0)
                {
                    sb.AppendLine($"  ↳ Redundancy: {stats.DuplicateMediaFilesCount} duplicate media references; {stats.OrphanedMediaCount} orphaned media records.");
                }
            }
            else
            {
                sb.AppendLine("* Media Status: No multimedia records or attachments in file.");
            }

            // 5. Overall Score
            sb.AppendLine();
            sb.AppendLine("--- Overall Health Score ---");
            sb.AppendLine($"Overall Health Score: {stats.HealthScore}/100 ({stats.Rating})");
            sb.AppendLine("  [Based on citation coverage, image attachments, pinpoint detail, repository links, and media portability]");

            return sb.ToString();
        }

        /// <summary>
        /// Generates a side-by-side comparative health summary between two GEDCOM files.
        /// </summary>
        public static string GenerateComparison(GedcomTreeContext leftCtx, GedcomTreeContext rightCtx,
            string leftName = "Left", string rightName = "Right", string? leftDir = null, string? rightDir = null)
        {
            var left = Analyze(leftCtx, leftName, leftDir);
            var right = Analyze(rightCtx, rightName, rightDir);

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("========================================================================");
            sb.AppendLine("               CITATION & MEDIA HEALTH COMPARISON");
            sb.AppendLine("========================================================================");
            sb.AppendLine($"{"Metric",-38} | {left.Title,-14} | {right.Title,-14}");
            sb.AppendLine(new string('-', 72));

            sb.AppendLine($"{"Individuals Sourced",-38} | {left.SourcedIndividualsPercent,6:F1}% ({left.SourcedIndividualsCount,-4}) | {right.SourcedIndividualsPercent,6:F1}% ({right.SourcedIndividualsCount,-4})");
            sb.AppendLine($"{"Total Citations",-38} | {left.CitationsCount,13} | {right.CitationsCount,13}");
            sb.AppendLine($"{"Citations With Images/Media",-38} | {left.CitationsWithMediaPercent,6:F1}% ({left.CitationsWithMediaCount,-4}) | {right.CitationsWithMediaPercent,6:F1}% ({right.CitationsWithMediaCount,-4})");
            sb.AppendLine($"{"Citations Without Images",-38} | {left.CitationsWithoutMediaPercent,6:F1}% ({left.CitationsWithoutMediaCount,-4}) | {right.CitationsWithoutMediaPercent,6:F1}% ({right.CitationsWithoutMediaCount,-4})");
            sb.AppendLine($"{"Pinpoint Detail (Page/Volume)",-38} | {left.CitationsWithPagePercent,6:F1}% ({left.CitationsWithPageCount,-4}) | {right.CitationsWithPagePercent,6:F1}% ({right.CitationsWithPageCount,-4})");
            sb.AppendLine($"{"Sources Linked to Repository",-38} | {left.SourcesWithRepoPercent,6:F1}% ({left.SourcesWithRepoCount,-4}) | {right.SourcesWithRepoPercent,6:F1}% ({right.SourcesWithRepoCount,-4})");
            sb.AppendLine($"{"Conflated Source Titles",-38} | {left.ConflatedSourcesCount,13} | {right.ConflatedSourcesCount,13}");
            sb.AppendLine($"{"Total Media Files",-38} | {left.MediaCount,13} | {right.MediaCount,13}");
            sb.AppendLine($"{"Media Relative Paths (Portable)",-38} | {left.MediaRelativePercent,6:F1}% ({left.MediaRelativePathsCount,-4}) | {right.MediaRelativePercent,6:F1}% ({right.MediaRelativePathsCount,-4})");
            sb.AppendLine($"{"Media on Citations (Best Practice)",-38} | {left.MediaOnCitationsCount,13} | {right.MediaOnCitationsCount,13}");
            sb.AppendLine(new string('-', 72));
            sb.AppendLine($"{"Overall Health Score",-38} | {left.HealthScore,11}/100 | {right.HealthScore,11}/100");
            sb.AppendLine($"{"Health Rating",-38} | {left.Rating,-14} | {right.Rating,-14}");
            sb.AppendLine();

            return sb.ToString();
        }

        private static string CleanMediaPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string clean = path.Trim();
            if (clean.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(5).Trim();
            }
            return clean.Replace('\\', '/');
        }

        private static bool IsAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (Path.IsPathRooted(path)) return true;
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':') return true;
            if (path.StartsWith("//") || path.StartsWith(@"\\")) return true;
            return false;
        }
    }
}
