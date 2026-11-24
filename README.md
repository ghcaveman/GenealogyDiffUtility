# Genealogy Diff Utility

A cross-platform desktop app for comparing two GEDCOM family-tree files side by side.

Load one GEDCOM on the left and another on the right. You see both trees next to each other, can step through the differences, and get a report of what matches, what is missing, and what is extra. It's useful for checking an export from one program (Ancestry, FamilySearch, RootsMagic, Gramps and so on) against another, or for seeing what changed between two versions of the same tree.

Built with .NET 10 and [Avalonia UI](https://avaloniaui.net/). Runs on Windows, Linux and macOS.

> **Viewer only.** Genealogy Diff Utility reads your GEDCOM files and never changes them. It has no Save command, and nothing you do in the app (including edits in the individual dialog) is written back to either file. To fix a difference it finds, make the change in your genealogy program (Gramps, RootsMagic and so on) and export a new GEDCOM.

## Features

- **Side-by-side trees.** Each file is shown as a tree of Header, Individuals (grouped by surname), Families, Sources, Repositories and Notes. Dates are shown only as precisely as the file gives them: 08/24/1938, 08/1938, 1938 or abt 1773.
- **Synchronized navigation.** Expanding or selecting a record on one side does the same to the matching record on the other side, including a person or family reported as `[CHANGED]`. The two trees scroll together with a single scrollbar.
- **Mismatch navigation.** The ▲ / ▼ buttons jump to the previous or next record that differs between the two files. They use the same comparison as the Diff report, so they stop at exactly the records it lists, including each extra copy of a duplicated record.
- **Details view.** One **Details** checkbox, on by default, applies to both trees. It shows spouses, children, events (with their sources) and notes under individuals and families, and back-references under notes, sources and repositories.
- **Difference counts.** The top bar shows how many individuals, families, sources and repositories differ and, with Details on, how many event and source differences there are.
- **Diff report.** The **Diff** tab starts with a summary of the counts, then lists matches, missing records and extra records for each category, with an overall score. With Details on, it also compares events and sources and marks each person or family whose details differ as `[DIFFERENT]`, with the differences listed beneath. A key at the top says which file `[MISSING]` and `[EXTRA]` refer to. An event that was edited (a refined date or place, say) appears once as `[CHANGED]` with just the differing values for each side. So does a person whose birth date was corrected or added, and a family whose spouse changed that way, as long as the name pairs them up unambiguously. Where one side's date or place is clearly more specific (25 JAN 1877 vs 1877, or "Belsito, Cosenza, Italy" vs "Italy"), it is marked `◀ more specific`. Things that differ only in how programs write them are ignored: case and spacing, the GEDCOM "Y" event flag, and descriptions a program generated from the event and the record's own name, such as "Birth of Smith, John" on John Smith's birth or "Marriage of Smith, John and Rossi, Mary" on their marriage, including ones the program cut off partway through a name. A description that says anything else still counts.
- **Citation & Media Health report.** The **Health** tab (and the summary comparison at the end of the Diff report) analyzes evidence and media health: count and percentage of citations with vs. without attached images/scans, individual sourcing coverage, pinpoint Volume/Page detail quality, repository links, conflated source titles, media file accessibility on disk, relative vs. absolute paths, and an overall health score (0–100) with rating.
- **What is compared.** For every person: name, sex, birth, death and alternate names; for every family: spouses, marriage and children; for sources and repositories: repository and notes. With Details on, also each record's events, citations (source, page, date, confidence), notes and media, and the notes and media attached to each event and each citation. Linked records are compared by content (a source by its title, a note by its text, a media item by its file name), never by GEDCOM record number, so two exports that number things differently still match.
- **Raw text view.** The **Text** tab shows the GEDCOM file as loaded.
- **Copy.** Select one or more rows (Ctrl+click / Shift+click), then press Ctrl+C or right-click → **Copy**. You get indented plain text, in tree order.
- **Print.** **Print…** lets you print both trees side by side, a single tree, or the differences report. You can include or leave out details, and print all rows or only the expanded ones. The page opens in your browser's print dialog, so you can also save it as a PDF.
- **Edit an individual.** Double-click a person to view or adjust their details in the open session. Changes last only until you close the app and are never written back to the GEDCOM file.

## How records are matched

GEDCOM record IDs (`@I0001@` and so on) usually differ between exports of the same tree, so records are matched by content rather than by ID:

- **Individuals:** normalized full name plus birth date. People with no birth date are matched by name, with a numbered suffix when several people share a name.
- **Families:** the matched identities of the husband and wife, plus the marriage date.
- **Sources:** title, author and publication info.
- **Repositories:** name and address.
- **Notes:** the note text.
- **Citations:** the source and the page; citations of the same source whose page differs are paired up and shown as `[CHANGED]`.
- **Media:** the file name (the folder is ignored, since it differs between computers).

Names and text are normalized before comparing, so differences in case and spacing don't count as mismatches.

Matching counts duplicates. If a record appears twice in one file and once in the other, one pair matches and the second copy is reported as a duplicate. When both files have several copies of a record, the copies are paired so that the fewest differences remain.

## Installing

Download the latest build from the [Releases](../../releases) page.

| Platform | File | Notes |
|---|---|---|
| Windows (x64) | `GenealogyDiffUtility-Setup-<version>.exe` | Installer. Needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). |
| Linux (x64) | `GenealogyDiffUtility-<version>.AppImage` | Self-contained. Run `chmod +x` on it, then run it. |
| macOS | `GenealogyDiffUtility-<version>.dmg` | Self-contained Intel (x64) build; runs on Apple silicon through Rosetta. The app isn't signed, so the first time, right-click the app and choose **Open**. |

## Using it

1. Click **Select File** on the left and choose a `.ged` file.
2. Do the same on the right with the file you want to compare against.
3. Browse the **Tree** tabs. Selecting or expanding a record on one side follows on the other.
4. Use **▲ / ▼** in the top bar to step through differences, and turn on **Details** for a closer look.
5. Open the **Diff** tab for the full comparison report, or click **Print…** to print it.

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone <this repository>
cd GenealogyDiffUtility
dotnet run --project GenealogyDiffUtility.csproj
```

You can also open `GenealogyDiffUtility.slnx` in Visual Studio or Rider.

### Running the tests

```bash
dotnet test GenealogyDiffUtility.Tests/GenealogyDiffUtility.Tests.csproj
```

The tests cover GEDCOM parsing and tree comparison. `FamilyTreeTest.ged` is a sample file you can also load in the app.

### Release builds

The GitHub Actions workflow (`.github/workflows/nsis-installer.yml`) builds the Windows installer (NSIS), the Linux AppImage and the macOS DMG on every push to `main`. The builds are kept as workflow artifacts.

To publish a release, push a version tag:

```bash
git tag v1.0.0
git push origin v1.0.0
```

A tag push runs the same builds, then creates a GitHub release named after the tag, with all three packages attached and release notes generated automatically.

## Project layout

| File | Purpose |
|---|---|
| `GedcomParser.cs` | Reads a GEDCOM file (with encoding detection) into a `GedcomTreeContext`. |
| `IndividualNode.cs`, `FamilyNode.cs`, `SourceNode.cs`, `RepositoryNode.cs`, `NoteNode.cs`, `GedcomEvent.cs` | Record types. |
| `TreeNodeKeys.cs` | Builds the stable keys used to match records across files. |
| `DiffTreeViewModel.cs` | One side's tree, including detail sub-nodes and cross-tree sync. |
| `TreeSyncService.cs` | Keeps selection and expansion in step between the two trees. |
| `MismatchNavigator.cs` | Finds the previous or next differing record. |
| `TreeDiffGenerator.cs` | Produces the Diff report. |
| `TreePrinter.cs`, `PrintDialog.axaml` | Print options and the printable page. |
| `MainWindow.axaml`, `GenealogyControl.axaml` | The main window and the per-side tree panel. |
| `installer.nsi` | Windows installer script. |

## License

Licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE).
