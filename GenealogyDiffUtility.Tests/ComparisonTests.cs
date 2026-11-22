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
        Assert.Contains("[EXTRA] Event: Marital Status - Married (duplicate)", report);

        string reverse = TreeDiffGenerator.Generate(dup, single, includeDetails: true);
        Assert.Contains("[MISSING] Event: Marital Status - Married (duplicate)", reverse);
    }

    [Fact]
    public void Diff_DuplicateRecordsAreReported_NotHidden()
    {
        var one = ParseText(Head + "0 @I1@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1900\n0 TRLR\n");
        var two = ParseText(Head + "0 @I1@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1900\n" +
                                   "0 @I2@ INDI\n1 NAME John /Doe/\n1 BIRT\n2 DATE 1900\n0 TRLR\n");

        string report = TreeDiffGenerator.Generate(one, two, includeDetails: false);
        Assert.Contains("[EXTRA] Doe, John (b. 01/01/1900) (duplicate)", report);
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
