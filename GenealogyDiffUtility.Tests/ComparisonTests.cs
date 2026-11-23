using System.Text;
using GenealogyDiffUtility;
using Xunit;

namespace GenealogyDiffUtility.Tests;

/// <summary>
/// Small hand-written GEDCOM snippets covering parser edge cases, the Diff
/// report and mismatch navigation. No real family data is used.
/// </summary>
public class ComparisonTests
{
    private static GedcomTreeContext ParseText(string gedcom, Encoding? encoding = null)
    {
        string path = Path.Combine(Path.GetTempPath(), $"gdu-test-{Guid.NewGuid():N}.ged");
        File.WriteAllText(path, gedcom, encoding ?? new UTF8Encoding(false));
        try
        {
            return GedcomParser.Parse(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (MainWindowViewModel Main, GedcomTreeContext Left, GedcomTreeContext Right) LoadPair(string left, string right)
    {
        var main = new MainWindowViewModel();
        var l = ParseText(left);
        var r = ParseText(right);
        main.LeftTree.LoadTree(l, "left.ged", left);
        main.RightTree.LoadTree(r, "right.ged", right);
        return (main, l, r);
    }

    private const string Head = "0 HEAD\n1 SOUR Test\n1 CHAR UTF-8\n";

    // --- Parser ---

    [Fact]
    public void FirstNameLineIsPreferred_LaterOnesAreAlternates()
    {
        var ctx = ParseText(Head +
            "0 @I1@ INDI\n1 NAME Alden /Testerson/\n1 NAME Alan /Marley/\n0 TRLR\n");

        var p = ctx.Individuals["@I1@"];
        Assert.Equal("Alden Testerson", p.FullName);
        Assert.Equal("Testerson", p.LastName);
        Assert.Equal(new[] { "Alan Marley" }, p.AlternateNames);
    }

    [Fact]
    public void SourcePublicationInfoIsParsed_WithContinuations()
    {
        var ctx = ParseText(Head +
            "0 @S1@ SOUR\n1 TITL 1940 United States \n2 CONC Federal Census\n1 AUTH Bureau\n" +
            "1 PUBL Washington, \n2 CONC D.C.\n2 CONT NARA\n0 TRLR\n");

        var s = ctx.Sources["@S1@"];
        Assert.Equal("1940 United States Federal Census", s.Title);
        Assert.Equal("Bureau", s.Author);
        Assert.Equal("Washington, D.C.\nNARA", s.PublicationInfo);
    }

    [Fact]
    public void TrailingSpaceBeforeConcIsKept()
    {
        var ctx = ParseText(Head + "0 @N1@ NOTE This is \n1 CONC a test\n0 TRLR\n");
        Assert.Equal("This is a test", ctx.Notes["@N1@"].Text);
    }

    [Fact]
    public void InlineNotesAreCaptured_OnRecordsAndEvents()
    {
        var ctx = ParseText(Head +
            "0 @I1@ INDI\n1 NAME John /Doe/\n1 NOTE Person note\n2 CONT second line\n" +
            "1 BIRT\n2 DATE 1 JAN 1900\n2 NOTE Event note\n" +
            "1 NOTE @N1@\n" +
            "0 @F1@ FAM\n1 HUSB @I1@\n1 NOTE Family note\n" +
            "0 @S1@ SOUR\n1 TITL T\n1 NOTE Source note\n" +
            "0 @N1@ NOTE Linked\n0 TRLR\n");

        var p = ctx.Individuals["@I1@"];
        Assert.Equal(new[] { "Person note\nsecond line" }, p.InlineNotes);
        Assert.Equal(new[] { "@N1@" }, p.NoteIds);
        Assert.Equal(new[] { "Event note" }, p.Events[0].InlineNotes);
        Assert.Equal(new[] { "Family note" }, ctx.Families["@F1@"].InlineNotes);
        Assert.Equal(new[] { "Source note" }, ctx.Sources["@S1@"].InlineNotes);
    }

    [Fact]
    public void CitationDateDoesNotOverwriteEventDate()
    {
        var ctx = ParseText(Head +
            "0 @I1@ INDI\n1 NAME John /Doe/\n" +
            "1 BIRT\n2 DATE 24 AUG 1938\n2 PLAC Austin\n2 SOUR @S1@\n3 PAGE p. 4\n3 DATA\n4 DATE 1 MAR 2020\n" +
            "0 @S1@ SOUR\n1 TITL T\n0 TRLR\n");

        var p = ctx.Individuals["@I1@"];
        Assert.Equal("24 AUG 1938", p.BirthDate);
        Assert.Equal("24 AUG 1938", p.Events[0].Date);
        Assert.Equal("Austin", p.BirthPlace);
        Assert.Equal(new[] { "@S1@" }, p.Events[0].SourceIds);
    }

    [Fact]
    public void RepositoryAddressIgnoresNestedNoteContinuations()
    {
        var ctx = ParseText(Head +
            "0 @R1@ REPO\n1 NAME NARA\n1 ADDR NARA\n2 ADR1 NARA\n2 NOTE some note\n3 CONT more\n3 CONC text\n0 TRLR\n");

        Assert.Equal("NARA NARA", ctx.Repositories["@R1@"].Address);
    }

    [Fact]
    public void AnsiFileIsDecodedAsWindows1252()
    {
        var ctx = ParseText("0 HEAD\n1 CHAR ANSI\n0 @I1@ INDI\n1 NAME José /Muñoz/\n0 TRLR\n",
            CodePagesEncoding());

        Assert.Equal("José Muñoz", ctx.Individuals["@I1@"].FullName);
    }

    private static Encoding CodePagesEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    [Fact]
    public void HeaderDateIsParsedIndependentlyOfCulture()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var ctx = ParseText("0 HEAD\n1 DATE 15 JUL 2026\n0 TRLR\n");
            Assert.Equal(new DateTime(2026, 7, 15), ctx.Header.FileCreationDate);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    // --- Diff report ---

    private const string DuplicateEvents = Head +
        "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 BIRT\n2 DATE 1 JAN 1900\n" +
        "1 EVEN Married\n2 TYPE Marital Status\n1 EVEN Married\n2 TYPE Marital Status\n0 TRLR\n";

    private const string SingleEvent = Head +
        "0 @I9@ INDI\n1 NAME Mary /Rossi/\n1 BIRT\n2 DATE 1 JAN 1900\n" +
        "1 EVEN Married\n2 TYPE Marital Status\n0 TRLR\n";

    [Fact]
    public void DetailedDiff_DuplicateEventsDoNotThrow_AndExtraCopyIsReported()
    {
        var single = ParseText(SingleEvent);
        var dup = ParseText(DuplicateEvents);

        string report = TreeDiffGenerator.Generate(single, dup, includeDetails: true);
        Assert.Contains("[EXTRA]     Event: Marital Status - Married (duplicate)", report);

        string reverse = TreeDiffGenerator.Generate(dup, single, includeDetails: true);
        Assert.Contains("[MISSING]   Event: Marital Status - Married (duplicate)", reverse);
    }

    [Fact]
    public void DetailedDiff_NamesTheRecordAboveItsDetailDifferences()
    {
        var withEvent = ParseText(SingleEvent);
        var withoutEvent = ParseText(Head + "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 BIRT\n2 DATE 1 JAN 1900\n0 TRLR\n");

        string report = TreeDiffGenerator.Generate(withEvent, withoutEvent, includeDetails: true);
        string heading = "  [DIFFERENT] " + withEvent.Individuals.Values.Single().DisplayName;
        int headingAt = report.IndexOf(heading, StringComparison.Ordinal);
        int detailAt = report.IndexOf("[MISSING]   Event: Marital Status - Married", StringComparison.Ordinal);

        Assert.True(headingAt >= 0, "expected a heading naming the individual");
        Assert.True(detailAt > headingAt, "expected the detail line under the heading");
    }

    [Fact]
    public void DetailedDiff_NoHeadingWhenDetailsMatch()
    {
        var a = ParseText(SingleEvent);
        var b = ParseText(SingleEvent);

        string report = TreeDiffGenerator.Generate(a, b, includeDetails: true, out var summary);
        Assert.DoesNotContain("  [DIFFERENT] ", report);
        Assert.Equal(0, summary.Get("Individuals")!.Different);
    }

    [Fact]
    public void DetailedDiff_SummaryCountsRecordAndDetailDifferences()
    {
        var withEvent = ParseText(SingleEvent);
        var withoutEvent = ParseText(Head + "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 BIRT\n2 DATE 1 JAN 1900\n" +
                                            "0 @I2@ INDI\n1 NAME Ann /Rossi/\n0 TRLR\n");

        string report = TreeDiffGenerator.Generate(withEvent, withoutEvent, includeDetails: true, out var summary);
        var individuals = summary.Get("Individuals")!;

        Assert.Equal(0, individuals.Missing);
        Assert.Equal(1, individuals.Extra);             // Ann is only in the second file
        Assert.Equal(1, individuals.Different);         // Mary's events differ
        Assert.Equal(1, individuals.DetailDifferences); // one missing event
        Assert.Equal(2, individuals.RecordDifferences);
        Assert.StartsWith("=== Summary ===", report);
        Assert.Contains("Individuals: 2 differences", report);
    }

    // Same person exported by two programs: one writes "1 DEAT Y" and no
    // descriptions, the other leaves off the Y and adds generated descriptions
    // ("Birth of Altimare, Carmine") as TYPE lines or EVEN values.
    private const string FlagsNoDescriptions = Head +
        "0 @I1@ INDI\n1 NAME Carmine /Altimare/\n" +
        "1 BIRT\n2 DATE 1773\n2 PLAC Mangone, Cosenza, Calabria, Italia\n" +
        "1 DEAT Y\n" +
        "1 EVEN\n2 TYPE Alt. Birth\n2 DATE 10 JUL 1885\n2 PLAC Belsito\n0 TRLR\n";

    private const string NoFlagsWithDescriptions = Head +
        "0 @I7@ INDI\n1 NAME Carmine /Altimare/\n" +
        "1 BIRT\n2 TYPE Birth of Altimare, Carmine\n2 DATE 1773\n2 PLAC Mangone, Cosenza, Calabria, Italia\n" +
        "1 DEAT\n" +
        "1 EVEN Alt. Birth of Altimare, Carmine\n2 TYPE Alt. Birth\n2 DATE 10 JUL 1885\n2 PLAC Belsito\n0 TRLR\n";

    [Fact]
    public void DetailedDiff_YFlagsAndGeneratedDescriptionsAreNotDifferences()
    {
        var left = ParseText(FlagsNoDescriptions);
        var right = ParseText(NoFlagsWithDescriptions);

        string report = TreeDiffGenerator.Generate(left, right, includeDetails: true, out var summary);

        Assert.Equal(0, summary.DetailDifferences);
        Assert.Equal(0, summary.RecordDifferences);
        Assert.DoesNotContain("  [DIFFERENT] ", report);
    }

    [Fact]
    public void DetailedDiff_EditedEventIsOneChangedLineShowingOnlyTheDifference()
    {
        // A residence whose place was refined in one file. (A birth isn't used
        // because the birth date is part of how people are matched.)
        var left = ParseText(Head + "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 RESI\n2 DATE 1930\n2 PLAC Genova\n0 TRLR\n");
        var right = ParseText(Head + "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 RESI\n2 DATE 1930\n2 PLAC Genova, Liguria\n0 TRLR\n");

        string report = TreeDiffGenerator.Generate(left, right, includeDetails: true, out var summary);

        Assert.Contains("[CHANGED]   Event: Residence (1930) - Genova", report);
        Assert.Contains("Place: left:  Genova", report);
        Assert.Contains("right: Genova, Liguria", report);
        Assert.DoesNotContain("Date:", report);
        Assert.DoesNotContain("[MISSING]   Event", report);
        Assert.DoesNotContain("[EXTRA]     Event", report);
        Assert.Equal(1, summary.DetailDifferences);
    }

    [Fact]
    public void Diff_KeyExplainsMissingAndExtraBySide()
    {
        var a = ParseText(SingleEvent);
        string report = TreeDiffGenerator.Generate(a, a, includeDetails: false, out _, "left", "right", "a.ged", "b.ged");
        Assert.Contains("[MISSING] = only in the left file (a.ged); [EXTRA] = only in the right file (b.ged)", report);
    }

    [Fact]
    public void Diff_TagsArePaddedSoTextLinesUp()
    {
        var one = ParseText(Head + "0 @I1@ INDI\n1 NAME Ann /Doe/\n0 TRLR\n");
        var two = ParseText(Head + "0 @I1@ INDI\n1 NAME Bea /Doe/\n0 TRLR\n");
        string report = TreeDiffGenerator.Generate(one, two, includeDetails: false);

        var missing = report.Split('\n').Single(l => l.Contains("[MISSING]") && l.Contains("Doe, Ann"));
        var extra = report.Split('\n').Single(l => l.Contains("[EXTRA]") && l.Contains("Doe, Bea"));
        Assert.Equal(missing.IndexOf("Doe,"), extra.IndexOf("Doe,"));
    }

    [Fact]
    public void Diff_CorrectedBirthDateIsOneChangedPerson()
    {
        var left = ParseText(Head + "0 @I1@ INDI\n1 NAME Caterina /Altomare/\n1 BIRT\n2 DATE 25 JAN 1877\n0 TRLR\n");
        var right = ParseText(Head + "0 @I5@ INDI\n1 NAME Caterina /Altomare/\n1 BIRT\n2 DATE 1877\n0 TRLR\n");

        string report = TreeDiffGenerator.Generate(left, right, includeDetails: true, out var summary);
        var individuals = summary.Get("Individuals")!;

        Assert.Contains("  [CHANGED]   Altomare, Caterina", report);
        Assert.Contains("Birth date: left:  25 JAN 1877", report);
        Assert.Contains("right: 1877", report);
        Assert.DoesNotContain("[MISSING]   Altomare", report);
        Assert.DoesNotContain("[EXTRA]     Altomare", report);
        Assert.Equal(1, individuals.Changed);
        Assert.Equal(0, individuals.Missing);
        Assert.Equal(0, individuals.Extra);
        Assert.Equal(1, individuals.RecordDifferences);
        // The birth events themselves differ too, and are listed under the person.
        Assert.Contains("[CHANGED]   Event: Birth (25 JAN 1877)", report);
    }

    [Fact]
    public void Diff_BirthDateAddedOnOneSideIsOneChangedPerson()
    {
        var left = ParseText(Head + "0 @I1@ INDI\n1 NAME John /Doe/\n0 TRLR\n");
        var right = ParseText(Head + "0 @I1@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1900\n0 TRLR\n");

        TreeDiffGenerator.Generate(left, right, includeDetails: false, out var summary);
        Assert.Equal(1, summary.Get("Individuals")!.Changed);
        Assert.Equal(1, summary.RecordDifferences);
    }

    [Fact]
    public void Diff_AmbiguousSameNamePeopleAreNotPaired()
    {
        // Two unmatched John Does on the left, one on the right: no telling which is which.
        var left = ParseText(Head + "0 @I1@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1900\n" +
                                    "0 @I2@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1925\n0 TRLR\n");
        var right = ParseText(Head + "0 @I1@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1901\n0 TRLR\n");

        TreeDiffGenerator.Generate(left, right, includeDetails: false, out var summary);
        var individuals = summary.Get("Individuals")!;
        Assert.Equal(0, individuals.Changed);
        Assert.Equal(2, individuals.Missing);
        Assert.Equal(1, individuals.Extra);
    }

    [Fact]
    public void Diff_FamilyWhoseSpouseBirthDateChangedIsOneChangedFamily()
    {
        // Loaded into the trees (not just parsed) so spouses are linked to the family.
        const string couple = "0 @F1@ FAM\n1 HUSB @I1@\n1 WIFE @I2@\n0 TRLR\n";
        var (_, left, right) = LoadPair(
            Head + "0 @I1@ INDI\n1 NAME Carmine /Altimare/\n1 BIRT\n2 DATE 1773\n0 @I2@ INDI\n1 NAME Rosa /Bruno/\n" + couple,
            Head + "0 @I1@ INDI\n1 NAME Carmine /Altimare/\n1 BIRT\n2 DATE ABT 1773\n0 @I2@ INDI\n1 NAME Rosa /Bruno/\n" + couple);

        string report = TreeDiffGenerator.Generate(left, right, includeDetails: false, out var summary);
        var families = summary.Get("Families")!;

        Assert.Equal(1, families.Changed);
        Assert.Equal(0, families.Missing);
        Assert.Equal(0, families.Extra);
        Assert.Contains("Husband: left:", report);
    }

    [Fact]
    public void Navigation_StopsAtPersonWhoseOnlyDifferenceIsAnEvent()
    {
        // Same person, same birth; the right file adds a residence.
        var (main, _, _) = LoadPair(
            Head + "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 BIRT\n2 DATE 1 JAN 1900\n0 TRLR\n",
            Head + "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 BIRT\n2 DATE 1 JAN 1900\n1 RESI\n2 DATE 1930\n0 TRLR\n");

        main.NavigateToNextMismatch();
        Assert.Equal("Individual:mary rossi|1 jan 1900", main.LeftTree.GetSelectedNodeKey());
    }

    [Fact]
    public void Navigation_StopsOnceAtAChangedPerson()
    {
        var (main, _, _) = LoadPair(
            Head + "0 @I1@ INDI\n1 NAME Caterina /Altomare/\n1 BIRT\n2 DATE 25 JAN 1877\n0 TRLR\n",
            Head + "0 @I5@ INDI\n1 NAME Caterina /Altomare/\n1 BIRT\n2 DATE 1877\n0 TRLR\n");

        main.NavigateToNextMismatch();
        Assert.Equal("Individual:caterina altomare|25 jan 1877", main.LeftTree.GetSelectedNodeKey());

        // The right tree follows to its copy of the same person...
        Assert.Equal("Individual:caterina altomare|1877", main.RightTree.GetSelectedNodeKey());

        // ...and there's no second stop at that copy.
        main.NavigateToNextMismatch();
        Assert.Equal("Individual:caterina altomare|25 jan 1877", main.LeftTree.GetSelectedNodeKey());
        Assert.Equal("Individual:caterina altomare|1877", main.RightTree.GetSelectedNodeKey());
    }

    [Fact]
    public void Sync_SelectingAChangedPersonSelectsTheirCopyInTheOtherTree()
    {
        var (main, left, right) = LoadPair(
            Head + "0 @I1@ INDI\n1 NAME Caterina /Altomare/\n1 BIRT\n2 DATE 25 JAN 1877\n0 TRLR\n",
            Head + "0 @I5@ INDI\n1 NAME Caterina /Altomare/\n1 BIRT\n2 DATE 1877\n0 TRLR\n");

        right.Individuals.Values.Single().IsSelected = true;
        Assert.True(left.Individuals.Values.Single().IsSelected);

        right.Individuals.Values.Single().IsSelected = false;
        Assert.False(left.Individuals.Values.Single().IsSelected);
    }

    [Fact]
    public void Sync_DoesNotGuessBetweenSameNamePeople()
    {
        var (_, left, right) = LoadPair(
            Head + "0 @I1@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1900\n0 @I2@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1925\n0 TRLR\n",
            Head + "0 @I1@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1901\n0 TRLR\n");

        right.Individuals.Values.Single().IsSelected = true;
        Assert.DoesNotContain(left.Individuals.Values, p => p.IsSelected);
    }

    [Fact]
    public void Diff_MarksTheMoreSpecificDateAndPlace()
    {
        var left = ParseText(Head + "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 RESI\n2 DATE 1930\n2 PLAC Philadelphia\n" +
                                    "1 IMMI\n2 DATE ABT 1898\n2 PLAC Ellis Island\n0 TRLR\n");
        var right = ParseText(Head + "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 RESI\n2 DATE 1930\n2 PLAC Philadelphia, Pennsylvania\n" +
                                     "1 IMMI\n2 DATE 12 MAR 1898\n2 PLAC Ellis Island\n0 TRLR\n");

        string report = TreeDiffGenerator.Generate(left, right, includeDetails: true);
        var lines = report.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        Assert.Contains(lines, l => l.Contains("right: Philadelphia, Pennsylvania") && l.EndsWith("◀ more specific"));
        Assert.Contains(lines, l => l.Contains("left:  Philadelphia") && !l.Contains("more specific"));
        Assert.Contains(lines, l => l.Contains("right: 12 MAR 1898") && l.EndsWith("◀ more specific"));
    }

    [Fact]
    public void DateSpecificity_Rules()
    {
        Assert.Equal(1, DateSpecificity.Compare("25 JAN 1877", "1877"));
        Assert.Equal(1, DateSpecificity.Compare("JAN 1877", "1877"));
        Assert.Equal(-1, DateSpecificity.Compare("ABT 1885", "10 JUL 1885"));
        Assert.Equal(1, DateSpecificity.Compare("1885", "ABT 1885"));
        Assert.Equal(1, DateSpecificity.Compare("1877", "BET 1870 AND 1880"));
        Assert.Equal(1, DateSpecificity.Compare("1877", ""));
        Assert.Equal(0, DateSpecificity.Compare("1877", "1878"));            // disagree
        Assert.Equal(0, DateSpecificity.Compare("25 JAN 1877", "26 JAN 1877"));
        Assert.Equal(0, DateSpecificity.Compare("1890", "BET 1870 AND 1880")); // outside the range
        Assert.Equal(0, DateSpecificity.Compare("ABT 1877", "EST 1877"));
        Assert.Equal(0, DateSpecificity.Compare("some time", "1877"));        // unreadable
    }

    [Fact]
    public void PlaceSpecificity_Rules()
    {
        Assert.Equal(1, TreeDiffGenerator.PlaceSpecificity("Belsito, Cosenza, Calabria, Italy", "Italy"));
        Assert.Equal(-1, TreeDiffGenerator.PlaceSpecificity("Philadelphia", "Philadelphia, Pennsylvania"));
        Assert.Equal(0, TreeDiffGenerator.PlaceSpecificity("Belsito, Italy", "Mangone, Italy"));
        Assert.Equal(0, TreeDiffGenerator.PlaceSpecificity("Philadelphia", "Pittsburgh"));
        Assert.Equal(-1, TreeDiffGenerator.PlaceSpecificity(", ,", "Genova"));
    }

    [Fact]
    public void Navigation_IgnoresEventDifferencesTheReportIgnores()
    {
        var (main, _, _) = LoadPair(FlagsNoDescriptions, NoFlagsWithDescriptions);

        main.NavigateToNextMismatch();
        Assert.Null(main.LeftTree.GetSelectedNodeKey());
        Assert.Null(main.RightTree.GetSelectedNodeKey());
    }

    // One person whose birth citation has gained notes and media (as after a
    // citation merge that pooled other citations' notes onto it).
    private const string CitationPlain = Head +
        "0 @I1@ INDI\n1 NAME Josephine /Cerino/\n1 BIRT\n2 DATE 1890\n2 PLAC Italy\n2 SOUR @S1@\n" +
        "0 @S1@ SOUR\n1 TITL familysearch.org\n" +
        "0 @N1@ NOTE https://www.familysearch.org/ark:/61903/1:1:K5Q3-61L\n" +
        "0 @N2@ NOTE United States, Census, 1910, entry for Edward C Fuller\n" +
        "0 @O1@ OBJE\n1 FILE C:\\Media\\Fuller, Harry Jay - 1910Census.jpg\n0 TRLR\n";

    private const string CitationWithPooledNotes = Head +
        "0 @I9@ INDI\n1 NAME Josephine /Cerino/\n1 BIRT\n2 DATE 1890\n2 PLAC Italy\n2 SOUR @S7@\n" +
        "3 NOTE @N1@\n3 NOTE @N2@\n3 OBJE @O1@\n" +
        "0 @S7@ SOUR\n1 TITL familysearch.org\n" +
        "0 @N1@ NOTE https://www.familysearch.org/ark:/61903/1:1:K5Q3-61L\n" +
        "0 @N2@ NOTE United States, Census, 1910, entry for Edward C Fuller\n" +
        "0 @O1@ OBJE\n1 FILE /home/me/media/Fuller, Harry Jay - 1910Census.jpg\n0 TRLR\n";

    [Fact]
    public void Parser_ReadsCitationsOneByOne()
    {
        var ctx = ParseText(Head +
            "0 @I1@ INDI\n1 NAME Ann /Doe/\n1 SOUR @S1@\n2 PAGE p. 12\n2 QUAY 3\n2 DATA\n3 DATE 1 JAN 1950\n2 NOTE @N1@\n2 OBJE @O1@\n" +
            "1 NOTE @N2@\n1 OBJE\n2 FILE c:\\pics\\ann.jpg\n" +
            "1 BIRT\n2 DATE 1900\n2 SOUR @S1@\n3 PAGE p. 3\n3 NOTE inline citation note\n2 NOTE @N3@\n2 OBJE @O1@\n" +
            "0 @O1@ OBJE\n1 FILE c:\\pics\\census.jpg\n2 TITL Census\n0 TRLR\n");
        var ann = ctx.Individuals.Values.Single();

        var c = Assert.Single(ann.Citations);
        Assert.Equal("@S1@", c.SourceId);
        Assert.Equal("p. 12", c.Page.Trim());
        Assert.Equal("3", c.Quality);
        Assert.Equal("1 JAN 1950", c.Date);
        Assert.Equal(new[] { "@N1@" }, c.NoteIds);
        Assert.Equal(new[] { "@O1@" }, c.Media);
        Assert.Equal(new[] { "@N2@" }, ann.DirectNoteIds);
        Assert.Equal(new[] { @"file:c:\pics\ann.jpg" }, ann.Media);

        var birth = ann.Events.Single();
        var ec = Assert.Single(birth.Citations);
        Assert.Equal("p. 3", ec.Page.Trim());
        Assert.Equal(new[] { "inline citation note" }, ec.InlineNotes);
        Assert.Equal(new[] { "@N3@" }, birth.DirectNoteIds);
        Assert.Equal(new[] { "@O1@" }, birth.Media);
        Assert.Equal(@"c:\pics\census.jpg", ctx.Media["@O1@"].File);
    }

    [Fact]
    public void DetailedDiff_ReportsNotesAndMediaAddedToACitation()
    {
        string report = TreeDiffGenerator.Generate(ParseText(CitationWithPooledNotes), ParseText(CitationPlain),
            includeDetails: true, out var summary);

        Assert.Contains("  [DIFFERENT] Cerino, Josephine", report);
        Assert.Contains("      [DIFFERENT] Event: Birth (1890) - Italy", report);
        Assert.Contains("          [CHANGED]   Citation: familysearch.org", report);
        Assert.Contains("Notes: 2 only in the left file:", report);
        Assert.Contains("Media: 1 only in the left file: Fuller, Harry Jay - 1910Census.jpg", report);
        Assert.Equal(1, summary.DetailDifferences);
    }

    [Fact]
    public void DetailedDiff_SameCitationNotesInDifferentFoldersAreEqual()
    {
        // Same notes and the same media file name, different folders and record numbers.
        var other = CitationWithPooledNotes.Replace("/home/me/media/", "D:\\Old\\").Replace("@N1@", "@N99@");
        TreeDiffGenerator.Generate(ParseText(CitationWithPooledNotes), ParseText(other), includeDetails: true, out var summary);
        Assert.Equal(0, summary.DetailDifferences);
        Assert.Equal(0, summary.RecordDifferences);
    }

    [Fact]
    public void Diff_ReportsPersonFieldDifferencesEvenWithDetailsOff()
    {
        var a = ParseText(Head + "0 @I1@ INDI\n1 NAME Ann /Doe/\n1 SEX F\n1 BIRT\n2 DATE 1900\n1 DEAT\n2 DATE 1970\n0 TRLR\n");
        var b = ParseText(Head + "0 @I1@ INDI\n1 NAME Ann /Doe/\n1 SEX M\n1 BIRT\n2 DATE 1900\n1 DEAT\n2 DATE 1971\n0 TRLR\n");

        string report = TreeDiffGenerator.Generate(a, b, includeDetails: false, out var summary);
        Assert.Contains("  [DIFFERENT] Doe, Ann", report);
        Assert.Contains("Sex:", report);
        Assert.Contains("Death date:", report);
        Assert.Equal(2, summary.Get("Individuals")!.DetailDifferences);
    }

    [Fact]
    public void DetailedDiff_AdoptedOfDescriptionIsNotADifference()
    {
        var a = ParseText(Head + "0 @I1@ INDI\n1 NAME Kimberly W /White/\n1 ADOP\n2 TYPE Adopted of White, Kimberly W\n0 TRLR\n");
        var b = ParseText(Head + "0 @I1@ INDI\n1 NAME Kimberly W /White/\n1 ADOP\n0 TRLR\n");
        TreeDiffGenerator.Generate(a, b, includeDetails: true, out var summary);
        Assert.Equal(0, summary.RecordDifferences);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Navigation_StopsAtExactlyTheRecordsTheReportLists(bool details)
    {
        // A mix: pooled citation notes, a sex difference, an extra person, a matching person.
        string left = CitationWithPooledNotes.Replace("0 TRLR\n",
            "0 @I2@ INDI\n1 NAME Ann /Doe/\n1 SEX F\n0 @I3@ INDI\n1 NAME Bob /Roe/\n0 TRLR\n");
        string right = CitationPlain.Replace("0 TRLR\n",
            "0 @I2@ INDI\n1 NAME Ann /Doe/\n1 SEX M\n0 @I3@ INDI\n1 NAME Bob /Roe/\n0 @I4@ INDI\n1 NAME Cy /Poe/\n0 TRLR\n");
        var (main, _, _) = LoadPair(left, right);
        main.ShowDetails = details;

        var summary = main.LeftTree.DiffSummary!;
        Assert.Equal(summary.RecordDifferences, CountArrowStops(main));
        Assert.Equal(details ? 3 : 2, summary.RecordDifferences); // Josephine only differs in her citation's details
    }

    [Fact]
    public void Navigation_StopsAtEachExtraCopyOfARecord_LikeTheReport()
    {
        // Two copies of Ann on the left (one extra), and a note whose text is repeated on the right.
        var (main, _, _) = LoadPair(
            Head + "0 @I1@ INDI\n1 NAME Ann /Doe/\n0 @I2@ INDI\n1 NAME Ann /Doe/\n0 @N1@ NOTE Same text\n0 TRLR\n",
            Head + "0 @I1@ INDI\n1 NAME Ann /Doe/\n0 @N1@ NOTE Same text\n0 @N2@ NOTE Same text\n0 TRLR\n");

        var summary = main.LeftTree.DiffSummary!;
        Assert.Equal(2, summary.RecordDifferences);
        Assert.Equal(2, CountArrowStops(main));
    }

    /// <summary>Presses ▼ until the selection stops moving; returns how many records it stopped at.</summary>
    private static int CountArrowStops(MainWindowViewModel main)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        object? lastLeft = null, lastRight = null;
        for (int i = 0; i < 100; i++)
        {
            main.NavigateToNextMismatch();
            var left = main.LeftTree.GetSelectedNode();
            var right = main.RightTree.GetSelectedNode();
            if (i > 0 && ReferenceEquals(left, lastLeft) && ReferenceEquals(right, lastRight)) break;
            lastLeft = left;
            lastRight = right;
            // A stop selects a record in the left tree, or one only the right tree has.
            var stop = left != null && !seen.Contains(left) ? left : right;
            if (stop != null) seen.Add(stop);
        }
        return seen.Count;
    }

    [Fact]
    public void MainWindow_DetailsOnByDefault_AndTopBarShowsCounts()
    {
        var main = new MainWindowViewModel();
        Assert.True(main.ShowDetails);
        Assert.True(main.LeftTree.ShowDetails);
        Assert.True(main.RightTree.ShowDetails);

        var (loaded, _, _) = LoadPair(SingleEvent, Head + "0 @I1@ INDI\n1 NAME Mary /Rossi/\n1 BIRT\n2 DATE 1 JAN 1900\n0 TRLR\n");
        Assert.Contains("Individuals: 1 (1 details)", loaded.DiffSummaryText);
    }

    [Fact]
    public void Diff_DuplicateRecordsAreReported_NotHidden()
    {
        var one = ParseText(Head + "0 @I1@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1900\n0 TRLR\n");
        var two = ParseText(Head + "0 @I1@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1900\n" +
                                   "0 @I2@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1900\n0 TRLR\n");

        string report = TreeDiffGenerator.Generate(one, two, includeDetails: false);
        Assert.Contains("[EXTRA]     Doe, John (b. 01/01/1900) (duplicate)", report);
        Assert.Contains("Matches: 1 | Missing: 0 | Extra: 1", report);
    }

    [Fact]
    public void Match_PairsCopiesInOrder()
    {
        var result = TreeDiffGenerator.Match(new[] { "a", "a", "b" }, new[] { "a", "c", "a", "a" }, x => x);
        Assert.Equal(2, result.Matched.Count);
        Assert.Equal(new[] { "b" }, result.Missing);
        Assert.Equal(new[] { "c", "a" }, result.Extra);
    }

    // --- Mismatch navigation ---

    // The same tree exported by two programs: different record numbering and date case
    private const string ExportA = "0 HEAD\n1 SOUR Legacy\n1 DATE 1 JAN 2026\n" +
        "0 @I1@ INDI\n1 NAME John /Doe/\n1 SEX M\n1 SOUR @S55@\n1 BIRT\n2 DATE 24 Aug 1938\n2 SOUR @S55@\n1 NOTE @N1@\n" +
        "0 @I2@ INDI\n1 NAME Mary /Doe/\n1 SEX F\n" +
        "0 @F1@ FAM\n1 HUSB @I1@\n1 WIFE @I2@\n1 MARR\n2 DATE 6 May 1960\n" +
        "0 @S55@ SOUR\n1 TITL 1940 Census\n1 REPO @R1@\n" +
        "0 @R1@ REPO\n1 NAME NARA\n" +
        "0 @N1@ NOTE A note\n0 TRLR\n";

    private const string ExportB = "0 HEAD\n1 SOUR Legacy\n1 DATE 2 FEB 2026\n" +
        "0 @I00002@ INDI\n1 NAME Mary /Doe/\n1 SEX F\n" +
        "0 @I00001@ INDI\n1 NAME John /Doe/\n1 SEX M\n1 SOUR @S00055@\n1 BIRT\n2 DATE 24 AUG 1938\n2 SOUR @S00055@\n1 NOTE @N00001@\n" +
        "0 @F00001@ FAM\n1 HUSB @I00001@\n1 WIFE @I00002@\n1 MARR\n2 DATE 6 MAY 1960\n" +
        "0 @S00055@ SOUR\n1 TITL 1940 Census\n1 REPO @R00001@\n" +
        "0 @R00001@ REPO\n1 NAME NARA\n" +
        "0 @N00001@ NOTE A note\n0 TRLR\n";

    [Fact]
    public void Navigator_IgnoresRecordNumberingAndDateCase()
    {
        var (main, _, _) = LoadPair(ExportA, ExportB);

        main.NavigateToNextMismatch();

        Assert.Null(main.LeftTree.GetSelectedNodeKey());
        Assert.Null(main.RightTree.GetSelectedNodeKey());
    }

    [Fact]
    public void Navigator_TreatsUnknownSexAsBlank()
    {
        var unknown = ExportB.Replace("1 NAME Mary /Doe/\n1 SEX F", "1 NAME Mary /Doe/\n1 SEX U");
        var blank = ExportA.Replace("1 NAME Mary /Doe/\n1 SEX F", "1 NAME Mary /Doe/");
        var (main, _, _) = LoadPair(blank, unknown);

        main.NavigateToNextMismatch();
        Assert.Null(main.LeftTree.GetSelectedNodeKey());
        Assert.Null(main.RightTree.GetSelectedNodeKey());

        // A known sex against unknown is still a difference
        var (main2, _, _) = LoadPair(ExportA, unknown);
        main2.NavigateToNextMismatch();
        Assert.Equal("Individual:mary doe", main2.LeftTree.GetSelectedNodeKey());
    }

    // Two different people named John Doe with no birth date, told apart by
    // death date and parents. File A lists them in one order, file B in the
    // other and with different record numbers.
    private const string SameNameA = Head +
        "0 @I1@ INDI\n1 NAME John /Doe/\n1 DEAT\n2 DATE 1950\n" +
        "0 @I2@ INDI\n1 NAME John /Doe/\n" +
        "0 @I3@ INDI\n1 NAME Peter /Doe/\n1 BIRT\n2 DATE 1880\n" +
        "0 @F1@ FAM\n1 HUSB @I3@\n1 CHIL @I2@\n0 TRLR\n";

    private const string SameNameB = Head +
        "0 @I00007@ INDI\n1 NAME John /Doe/\n" +
        "0 @I00009@ INDI\n1 NAME John /Doe/\n1 DEAT\n2 DATE 1950\n" +
        "0 @I00003@ INDI\n1 NAME Peter /Doe/\n1 BIRT\n2 DATE 1880\n" +
        "0 @F00001@ FAM\n1 HUSB @I00003@\n1 CHIL @I00007@\n0 TRLR\n";

    [Fact]
    public void SameNamePeopleWithoutBirthDates_PairByContentNotRecordNumber()
    {
        var (main, left, right) = LoadPair(SameNameA, SameNameB);

        // The one who died in 1950 gets the same key in both files
        Assert.Equal(TreeNodeKeys.GetKey(left.Individuals["@I1@"]), TreeNodeKeys.GetKey(right.Individuals["@I00009@"]));
        Assert.Equal(TreeNodeKeys.GetKey(left.Individuals["@I2@"]), TreeNodeKeys.GetKey(right.Individuals["@I00007@"]));

        main.NavigateToNextMismatch();
        Assert.Null(main.LeftTree.GetSelectedNodeKey());
        Assert.Null(main.RightTree.GetSelectedNodeKey());

        string report = TreeDiffGenerator.Generate(left, right, includeDetails: true);
        Assert.Contains("Matches: 3 | Missing: 0 | Extra: 0", report);
    }

    [Fact]
    public void SameNamePeopleWithoutBirthDates_LoneMatchPairsWithBestDocumented()
    {
        // B only has the John Doe who died in 1950, under a low record number
        var onlyOne = Head + "0 @I0@ INDI\n1 NAME John /Doe/\n1 DEAT\n2 DATE 1950\n0 TRLR\n";
        var (_, left, right) = LoadPair(SameNameA, onlyOne);

        Assert.Equal(TreeNodeKeys.GetKey(left.Individuals["@I1@"]), TreeNodeKeys.GetKey(right.Individuals["@I0@"]));
        Assert.NotEqual(TreeNodeKeys.GetKey(left.Individuals["@I2@"]), TreeNodeKeys.GetKey(right.Individuals["@I0@"]));
    }

    [Fact]
    public void Navigator_FlagsChangedSourceContent()
    {
        var changed = ExportB.Replace("1 TITL 1940 Census\n1 REPO @R00001@", "1 TITL 1940 Census\n1 AUTH Someone\n1 REPO @R00001@");
        var (main, _, _) = LoadPair(ExportA, changed);

        main.NavigateToNextMismatch();

        // John's citation now points to a different source; that is the first difference
        Assert.Equal("Individual:john doe|24 aug 1938", main.LeftTree.GetSelectedNodeKey());
    }

    [Fact]
    public void Navigator_ReachesRecordsThatOnlyExistInTheRightTree()
    {
        var withExtra = ExportB.Replace("0 @F00001@ FAM",
            "0 @I00003@ INDI\n1 NAME Zoe /Doe/\n1 SEX F\n0 @F00001@ FAM");
        var (main, _, _) = LoadPair(ExportA, withExtra);

        main.NavigateToNextMismatch();
        Assert.Null(main.LeftTree.GetSelectedNodeKey());
        Assert.Equal("Individual:zoe doe", main.RightTree.GetSelectedNodeKey());

        // Nothing after it: a further press stays put rather than jumping back
        main.NavigateToNextMismatch();
        Assert.Equal("Individual:zoe doe", main.RightTree.GetSelectedNodeKey());
    }

    [Fact]
    public void Navigator_StepsThroughLeftOnlyAndRightOnlyRecords()
    {
        var leftExtra = ExportA.Replace("0 @F1@ FAM", "0 @I3@ INDI\n1 NAME Adam /Doe/\n0 @F1@ FAM");
        var rightExtra = ExportB.Replace("0 @F00001@ FAM", "0 @I00003@ INDI\n1 NAME Zoe /Doe/\n0 @F00001@ FAM");
        var (main, _, _) = LoadPair(leftExtra, rightExtra);

        string? Current() => main.LeftTree.GetSelectedNodeKey() ?? main.RightTree.GetSelectedNodeKey();

        main.NavigateToNextMismatch();
        var first = Current();
        main.NavigateToNextMismatch();
        var second = Current();

        Assert.Equal(new[] { "Individual:adam doe", "Individual:zoe doe" },
            new[] { first!, second! }.OrderBy(k => k));

        // Stepping back returns to the first one
        main.NavigateToPreviousMismatch();
        Assert.Equal(first, Current());

        // Each record is selected only in the tree that has it
        if (first == "Individual:zoe doe")
        {
            Assert.Null(main.LeftTree.GetSelectedNodeKey());
            Assert.Equal(first, main.RightTree.GetSelectedNodeKey());
        }
        else
        {
            Assert.Equal(first, main.LeftTree.GetSelectedNodeKey());
            Assert.Null(main.RightTree.GetSelectedNodeKey());
        }
    }
}
