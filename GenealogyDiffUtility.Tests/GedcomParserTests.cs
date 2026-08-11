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
}
