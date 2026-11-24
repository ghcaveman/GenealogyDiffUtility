using System;
using System.IO;
using System.Text;
using GenealogyDiffUtility;
using Xunit;

namespace GenealogyDiffUtility.Tests
{
    public class CitationMediaHealthReportTests
    {
        private static GedcomTreeContext ParseText(string gedcom)
        {
            string path = Path.Combine(Path.GetTempPath(), $"gdu-test-{Guid.NewGuid():N}.ged");
            File.WriteAllText(path, gedcom, new UTF8Encoding(false));
            try
            {
                return GedcomParser.Parse(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private const string Head = "0 HEAD\n1 SOUR Test\n1 CHAR UTF-8\n";

        [Fact]
        public void EmptyTree_ReportsCleanScoreAndZeroCounts()
        {
            var ctx = ParseText(Head + "0 TRLR\n");
            var stats = CitationMediaHealthReport.Analyze(ctx);

            Assert.Equal(0, stats.IndividualsCount);
            Assert.Equal(0, stats.CitationsCount);
            Assert.Equal(0, stats.MediaCount);
            Assert.Equal(100, stats.HealthScore);
            Assert.Equal("Excellent", stats.Rating);

            string report = CitationMediaHealthReport.Generate(ctx);
            Assert.Contains("CITATION & MEDIA HEALTH REPORT", report);
            Assert.Contains("Database Totals: 0 people", report);
            Assert.Contains("Overall Health Score: 100/100 (Excellent)", report);
        }

        [Fact]
        public void CitationImageCoverage_CalculatesAccuratePercentages()
        {
            string gedcom = Head +
                "0 @I1@ INDI\n" +
                "1 NAME George /Washington/\n" +
                "1 BIRT\n" +
                "2 DATE 22 FEB 1732\n" +
                "2 SOUR @S1@\n" +
                "3 PAGE Page 12\n" +
                "3 OBJE @O1@\n" +
                "1 DEAT\n" +
                "2 DATE 14 DEC 1799\n" +
                "2 SOUR @S1@\n" +
                "3 PAGE Page 99\n" +
                "0 @S1@ SOUR\n" +
                "1 TITL Mount Vernon Records\n" +
                "0 @O1@ OBJE\n" +
                "1 FILE George_Birth.jpg\n" +
                "0 TRLR\n";

            var ctx = ParseText(gedcom);
            var stats = CitationMediaHealthReport.Analyze(ctx);

            Assert.Equal(1, stats.IndividualsCount);
            Assert.Equal(1, stats.SourcedIndividualsCount);
            Assert.Equal(2, stats.CitationsCount);
            Assert.Equal(1, stats.CitationsWithMediaCount);
            Assert.Equal(1, stats.CitationsWithoutMediaCount);
            Assert.Equal(50.0, stats.CitationsWithMediaPercent, 1);
            Assert.Equal(50.0, stats.CitationsWithoutMediaPercent, 1);

            string report = CitationMediaHealthReport.Generate(ctx);
            Assert.Contains("Citation Image Coverage: 1 of 2 citations (50.0%)", report);
            Assert.Contains("Citations without images: 1 of 2 (50.0%)", report);
            Assert.Contains("Pinpoint Detail Quality: 2 of 2 citations (100.0%)", report);
        }

        [Fact]
        public void IndividualSourcing_CalculatesSourcedAndUnsourced()
        {
            string gedcom = Head +
                "0 @I1@ INDI\n" +
                "1 NAME John /Doe/\n" +
                "1 SOUR @S1@\n" +
                "0 @I2@ INDI\n" +
                "1 NAME Jane /Doe/\n" +
                "0 @S1@ SOUR\n" +
                "1 TITL Census Record\n" +
                "0 TRLR\n";

            var ctx = ParseText(gedcom);
            var stats = CitationMediaHealthReport.Analyze(ctx);

            Assert.Equal(2, stats.IndividualsCount);
            Assert.Equal(1, stats.SourcedIndividualsCount);
            Assert.Equal(1, stats.UnsourcedIndividualsCount);
            Assert.Equal(50.0, stats.SourcedIndividualsPercent, 1);
            Assert.Equal(50.0, stats.UnsourcedIndividualsPercent, 1);

            string report = CitationMediaHealthReport.Generate(ctx);
            Assert.Contains("Individual Sourcing: 1 of 2 people (50.0%)", report);
            Assert.Contains("Unsourced individuals: 1 of 2 (50.0%)", report);
        }

        [Fact]
        public void ConflatedSources_And_RepositoryLinkage_Detected()
        {
            string gedcom = Head +
                "0 @S1@ SOUR\n" +
                "1 TITL https://familysearch.org/ark:/61903/1:1:M9X9-123 1940 Census Page 4B\n" +
                "0 @S2@ SOUR\n" +
                "1 TITL Clean Collection Title\n" +
                "1 REPO @R1@\n" +
                "0 @R1@ REPO\n" +
                "1 NAME National Archives\n" +
                "1 ADDR Washington, DC\n" +
                "0 TRLR\n";

            var ctx = ParseText(gedcom);
            var stats = CitationMediaHealthReport.Analyze(ctx);

            Assert.Equal(2, stats.SourcesCount);
            Assert.Equal(1, stats.SourcesWithRepoCount);
            Assert.Equal(1, stats.SourcesWithoutRepoCount);
            Assert.Equal(1, stats.ConflatedSourcesCount);

            string report = CitationMediaHealthReport.Generate(ctx);
            Assert.Contains("Repository Linkage: 1 of 2 sources (50.0%)", report);
            Assert.Contains("Sources missing repository link: 1 of 2 (50.0%)", report);
            Assert.Contains("Conflated Sources: 1 sources mix repository", report);
        }

        [Fact]
        public void MediaPaths_PortabilityAndDiskChecking()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"gdu-media-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            string existingFile = Path.Combine(tempDir, "sample.jpg");
            File.WriteAllText(existingFile, "fake image");

            try
            {
                string gedcom = Head +
                    "0 @O1@ OBJE\n" +
                    "1 FILE sample.jpg\n" +
                    "0 @O2@ OBJE\n" +
                    "1 FILE missing.png\n" +
                    "0 @O3@ OBJE\n" +
                    "1 FILE C:\\Absolute\\Path\\image.jpg\n" +
                    "0 TRLR\n";

                var ctx = ParseText(gedcom);
                var stats = CitationMediaHealthReport.Analyze(ctx, "TestTree", tempDir);

                Assert.Equal(3, stats.MediaCount);
                Assert.True(stats.CheckedDisk);
                Assert.Equal(1, stats.MediaFoundOnDiskCount);
                Assert.Equal(2, stats.MediaMissingOnDiskCount);
                Assert.Equal(2, stats.MediaRelativePathsCount);
                Assert.Equal(1, stats.MediaAbsolutePathsCount);

                string report = CitationMediaHealthReport.Generate(ctx, "TestTree", tempDir);
                Assert.Contains("File Accessibility: 1 of 3 files (33.3%) verified on disk", report);
                Assert.Contains("Missing media files: 2 of 3 (66.7%)", report);
                Assert.Contains("Path Portability: 2 of 3 media records (66.7%) use portable relative paths", report);
                Assert.Contains("Non-portable absolute paths: 1 of 3 (33.3%)", report);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void GenerateComparison_OutputsComparativeTable()
        {
            string leftGed = Head +
                "0 @I1@ INDI\n" +
                "1 NAME Left /Person/\n" +
                "1 SOUR @S1@\n" +
                "2 PAGE Page 1\n" +
                "0 @S1@ SOUR\n" +
                "1 TITL Source 1\n" +
                "1 REPO @R1@\n" +
                "0 @R1@ REPO\n" +
                "1 NAME Archive\n" +
                "0 TRLR\n";

            string rightGed = Head +
                "0 @I1@ INDI\n" +
                "1 NAME Right /Person/\n" +
                "0 TRLR\n";

            var left = ParseText(leftGed);
            var right = ParseText(rightGed);

            string comparison = CitationMediaHealthReport.GenerateComparison(left, right, "LeftFile", "RightFile");

            Assert.Contains("CITATION & MEDIA HEALTH COMPARISON", comparison);
            Assert.Contains("LeftFile", comparison);
            Assert.Contains("RightFile", comparison);
            Assert.Contains("Individuals Sourced", comparison);
            Assert.Contains("Total Citations", comparison);
            Assert.Contains("Citations Without Images", comparison);
            Assert.Contains("Overall Health Score", comparison);
        }

        [Fact]
        public void TreeDiffGenerator_IncludesCitationMediaHealthReport()
        {
            string leftGed = Head + "0 @I1@ INDI\n1 NAME John /Doe/\n0 TRLR\n";
            string rightGed = Head + "0 @I1@ INDI\n1 NAME John /Doe/\n0 TRLR\n";

            var left = ParseText(leftGed);
            var right = ParseText(rightGed);

            string diff = TreeDiffGenerator.Generate(left, right, includeDetails: true);

            Assert.Contains("=== Overall Score ===", diff);
            Assert.Contains("CITATION & MEDIA HEALTH COMPARISON", diff);
            Assert.Contains("Citations Without Images", diff);
        }
    }
}
