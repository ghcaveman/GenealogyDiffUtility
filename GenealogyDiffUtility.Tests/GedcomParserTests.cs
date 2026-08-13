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
}