using GenealogyDiffUtility;
using Xunit;

namespace GenealogyDiffUtility.Tests;

public class GedcomParserTests
{
    private const string GedFileName = "LegacyExport_ToGramps_07152026.ged";

    private static string GedFilePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", GedFileName);

    [Fact]
    public void LoadLegacyExport_AllIndividualsAddedToTree()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(669, context.Individuals.Count);
    }

    [Fact]
    public void LoadLegacyExport_AllFamiliesAddedToTree()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(250, context.Families.Count);
    }

    [Fact]
    public void LoadLegacyExport_AllSourcesAddedToTree()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(70, context.Sources.Count);
    }

    [Fact]
    public void LoadLegacyExport_AllRepositoriesAddedToTree()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(2, context.Repositories.Count);
    }

    [Fact]
    public void LoadLegacyExport_AllNotesAddedToTree()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(5, context.Notes.Count);
    }

    [Fact]
    public void LoadLegacyExport_AllIndividualEventsAddedToTree()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalIndividualEvents = context.Individuals.Values.Sum(i => i.Events.Count);
        Assert.Equal(1495, totalIndividualEvents);
    }

    [Fact]
    public void LoadLegacyExport_AllFamilyEventsAddedToTree()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalFamilyEvents = context.Families.Values.Sum(f => f.Events.Count);
        Assert.Equal(275, totalFamilyEvents);
    }

    [Fact]
    public void LoadLegacyExport_SourcesAssignedToIndividualEvents()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalIndividualEventSources = context.Individuals.Values
            .Sum(i => i.Events.Sum(e => e.SourceIds.Count));
        Assert.Equal(1087, totalIndividualEventSources);
    }

    [Fact]
    public void LoadLegacyExport_SourcesAssignedToFamilyEvents()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalFamilyEventSources = context.Families.Values
            .Sum(f => f.Events.Sum(e => e.SourceIds.Count));
        Assert.Equal(85, totalFamilyEventSources);
    }

    [Fact]
    public void LoadLegacyExport_RepositoriesAssignedToCorrectSources()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);

        // @S67@ should be assigned to @R1@
        Assert.True(context.Sources.TryGetValue("@S67@", out var source67));
        Assert.Equal("@R1@", source67.RepositoryId);

        // @S68@ should be assigned to @R2@
        Assert.True(context.Sources.TryGetValue("@S68@", out var source68));
        Assert.Equal("@R2@", source68.RepositoryId);

        // @S69@ should be assigned to @R2@
        Assert.True(context.Sources.TryGetValue("@S69@", out var source69));
        Assert.Equal("@R2@", source69.RepositoryId);
    }

    [Fact]
    public void LoadLegacyExport_NotesAssignedToIndividuals()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalIndividualNotes = context.Individuals.Values.Sum(i => i.NoteIds.Count);
        Assert.Equal(1, totalIndividualNotes);
    }

    [Fact]
    public void LoadLegacyExport_NotesAssignedToIndividualEvents()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalIndividualEventNotes = context.Individuals.Values
            .Sum(i => i.Events.Sum(e => e.NoteIds.Count));
        Assert.Equal(4, totalIndividualEventNotes);
    }

    [Fact]
    public void LoadLegacyExport_NotesAssignedToFamilies()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalFamilyNotes = context.Families.Values.Sum(f => f.NoteIds.Count);
        Assert.Equal(0, totalFamilyNotes);
    }

    [Fact]
    public void LoadLegacyExport_NotesAssignedToFamilyEvents()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalFamilyEventNotes = context.Families.Values
            .Sum(f => f.Events.Sum(e => e.NoteIds.Count));
        Assert.Equal(0, totalFamilyEventNotes);
    }

    [Fact]
    public void LoadLegacyExport_NotesAssignedToSources()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalSourceNotes = context.Sources.Values.Sum(s => s.NoteIds.Count);
        Assert.Equal(0, totalSourceNotes);
    }

    [Fact]
    public void LoadLegacyExport_NotesAssignedToRepositories()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        int totalRepositoryNotes = context.Repositories.Values.Sum(r => r.NoteIds.Count);
        Assert.Equal(0, totalRepositoryNotes);
    }

    [Fact]
    public void LoadLegacyExport_HeaderMetadataParsedCorrectly()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.Equal("Legacy", context.Header.SourceSoftware);
        Assert.Equal("10.0", context.Header.SoftwareVersion);
        Assert.Equal("5.5.1", context.Header.GedcomVersion);
        Assert.Equal("UTF-8", context.Header.CharacterEncoding);
        Assert.Equal(new DateTime(2026, 7, 15), context.Header.FileCreationDate);
    }

    [Fact]
    public void LoadLegacyExport_FirstIndividualParsedCorrectly()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Individuals.TryGetValue("@I1@", out var individual));
        Assert.Equal("Dottie Lucas", individual.FullName);
        Assert.Equal("Lucas", individual.LastName);
        Assert.Equal("F", individual.Gender);
        Assert.Equal("24 Aug 1938", individual.BirthDate);
        Assert.Empty(individual.BirthPlace);
    }

    [Fact]
    public void LoadLegacyExport_FirstIndividualHasBirthEvent()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Individuals.TryGetValue("@I1@", out var individual));
        Assert.Single(individual.Events);
        var birthEvent = individual.Events[0];
        Assert.Equal("BIRT", birthEvent.Type);
        Assert.Equal("24 Aug 1938", birthEvent.Date);
    }

    [Fact]
    public void LoadLegacyExport_IndividualWithNoteHasCorrectNoteReference()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Individuals.TryGetValue("@I500124@", out var individual));
        Assert.Equal("Louis Pittaluga", individual.FullName);
        Assert.Single(individual.NoteIds);
        Assert.Equal("@NI500124@", individual.NoteIds[0]);
    }

    [Fact]
    public void LoadLegacyExport_FirstFamilyParsedCorrectly()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Families.TryGetValue("@F1@", out var family));
        Assert.Equal("@I76@", family.HusbandId);
        Assert.Equal("@I1@", family.WifeId);
        Assert.Equal(4, family.ChildrenIds.Count);
        Assert.Contains("@I500256@", family.ChildrenIds);
        Assert.Contains("@I500257@", family.ChildrenIds);
        Assert.Contains("@I500258@", family.ChildrenIds);
        Assert.Contains("@I500259@", family.ChildrenIds);
        Assert.Empty(family.MarriageDate);
    }

    [Fact]
    public void LoadLegacyExport_SecondFamilyHasMarriageDetails()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Families.TryGetValue("@F2@", out var family));
        Assert.Equal("@I3@", family.HusbandId);
        Assert.Equal("@I500256@", family.WifeId);
        Assert.Equal("6 May 1989", family.MarriageDate);
        Assert.Equal("Austin, Texas", family.MarriagePlace);

        // The marriage should also be recorded as an event
        Assert.Single(family.Events);
        Assert.Equal("MARR", family.Events[0].Type);
        Assert.Equal("6 May 1989", family.Events[0].Date);
        Assert.Equal("Austin, Texas", family.Events[0].Place);
    }

    [Fact]
    public void LoadLegacyExport_SourceTitlesParsedCorrectly()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Sources.TryGetValue("@S2@", out var sourceS2));
        Assert.Equal("1940 United States Federal Census", sourceS2.Title);
        Assert.True(context.Sources.TryGetValue("@S55@", out var sourceS55));
        Assert.Equal("1860 United States Federal Census", sourceS55.Title);
    }

    [Fact]
    public void LoadLegacyExport_RepositoryDetailsParsedCorrectly()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Repositories.TryGetValue("@R1@", out var repo1));
        Assert.StartsWith("Local Government and Municipal Registries", repo1.Name);
        Assert.StartsWith("Local Government and Municipal Registries", repo1.Address);

        Assert.True(context.Repositories.TryGetValue("@R2@", out var repo2));
        Assert.Equal("The National Archives and Records Administration (NARA)", repo2.Name);
        Assert.StartsWith("The National Archives and Records Administration (NARA)", repo2.Address);
    }

    [Fact]
    public void LoadLegacyExport_NoteTextParsedCorrectly()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Notes.TryGetValue("@BI476@", out var note1));
        Assert.Equal("Birth type:Illinios, United States", note1.Text);
        Assert.True(context.Notes.TryGetValue("@DI500124@", out var note2));
        Assert.Equal("Passed away exactly on his 83rd birthday.", note2.Text);
        Assert.True(context.Notes.TryGetValue("@XI500124@", out var note3));
        Assert.Equal("Entombment located in Mausoleum 2 (Outside), side-by-side with his wife Angelina.", note3.Text);
    }

    [Fact]
    public void LoadLegacyExport_RepositoryTitlesAndNamesAreCorrect()
    {
        // Act
        var context = GedcomParser.Parse(GedFilePath);

        // Assert
        Assert.NotNull(context);

        // @R1@ is referenced by @S67@
        Assert.True(context.Sources.TryGetValue("@S67@", out var source67));
        Assert.Equal("@R1@", source67.RepositoryId);

        // @R2@ is referenced by @S68@ and @S69@
        Assert.True(context.Sources.TryGetValue("@S68@", out var source68));
        Assert.Equal("@R2@", source68.RepositoryId);
        Assert.True(context.Sources.TryGetValue("@S69@", out var source69));
        Assert.Equal("@R2@", source69.RepositoryId);
    }
}
