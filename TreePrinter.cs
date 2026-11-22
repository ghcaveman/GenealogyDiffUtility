using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace GenealogyDiffUtility
{
    /// <summary>What to print.</summary>
    internal enum PrintContent
    {
        BothTrees,
        LeftTree,
        RightTree,
        Differences
    }

    /// <summary>The choices made in the print dialog.</summary>
    internal sealed class PrintOptions
    {
        public PrintContent Content { get; set; } = PrintContent.BothTrees;

        /// <summary>Include the detail rows (spouses, children, events, notes, references).</summary>
        public bool IncludeDetails { get; set; }

        /// <summary>
        /// When true, print only the rows currently expanded on screen; when
        /// false, print every row in the tree.
        /// </summary>
        public bool ExpandedRowsOnly { get; set; }
    }

    /// <summary>
    /// Builds a printable HTML page from the trees or the diff report, and opens
    /// it in the default browser, which shows its print dialog (printer choice,
    /// preview, page range, and Save as PDF).
    /// </summary>
    internal static class TreePrinter
    {
        public static void Print(MainWindowViewModel main, PrintOptions options)
        {
            string html = BuildHtml(main, options);
            string path = Path.Combine(Path.GetTempPath(),
                $"GenealogyDiffUtility-print-{DateTime.Now:yyyyMMdd-HHmmss}.html");
            File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        public static string BuildHtml(MainWindowViewModel main, PrintOptions options)
        {
            // Detail rows only exist while Details is on. If the print choice differs
            // from the on-screen setting, switch it for the duration of the build.
            bool originalDetails = main.ShowDetails;
            bool switchDetails = options.Content != PrintContent.Differences &&
                                 options.IncludeDetails != originalDetails;
            if (switchDetails) main.ShowDetails = options.IncludeDetails;

            try
            {
                return options.Content switch
                {
                    PrintContent.Differences => BuildDiffPage(main, options),
                    _ => BuildTreePage(main, options)
                };
            }
            finally
            {
                if (switchDetails) main.ShowDetails = originalDetails;
            }
        }

        private static string BuildDiffPage(MainWindowViewModel main, PrintOptions options)
        {
            string body;
            if (main.LeftTree.Context.Individuals.Count == 0 || main.RightTree.Context.Individuals.Count == 0)
            {
                body = "<p>Load two files to compare.</p>";
            }
            else
            {
                string report = TreeDiffGenerator.Generate(main.LeftTree.Context, main.RightTree.Context, options.IncludeDetails,
                    out _, "left", "right", main.LeftTree.FileName, main.RightTree.FileName);
                body = $"<pre class=\"diff\">{Encode(report)}</pre>";
            }

            string subtitle = $"Left: {Encode(main.LeftTree.FileName)} &nbsp;·&nbsp; Right: {Encode(main.RightTree.FileName)}";
            return WrapPage("Differences", subtitle, options, body);
        }

        private static string BuildTreePage(MainWindowViewModel main, PrintOptions options)
        {
            var sb = new StringBuilder();
            string subtitle;

            switch (options.Content)
            {
                case PrintContent.LeftTree:
                    subtitle = $"Left: {Encode(main.LeftTree.FileName)}";
                    sb.Append("<div class=\"single\">");
                    AppendTree(sb, main.LeftTree, "Left", options);
                    sb.Append("</div>");
                    break;

                case PrintContent.RightTree:
                    subtitle = $"Right: {Encode(main.RightTree.FileName)}";
                    sb.Append("<div class=\"single\">");
                    AppendTree(sb, main.RightTree, "Right", options);
                    sb.Append("</div>");
                    break;

                default:
                    subtitle = $"Left: {Encode(main.LeftTree.FileName)} &nbsp;·&nbsp; Right: {Encode(main.RightTree.FileName)}";
                    sb.Append("<div class=\"cols\">");
                    AppendTree(sb, main.LeftTree, "Left", options);
                    AppendTree(sb, main.RightTree, "Right", options);
                    sb.Append("</div>");
                    break;
            }

            string title = options.Content switch
            {
                PrintContent.LeftTree => "Left Tree",
                PrintContent.RightTree => "Right Tree",
                _ => "Both Trees"
            };
            return WrapPage(title, subtitle, options, sb.ToString());
        }

        private static void AppendTree(StringBuilder sb, DiffTreeViewModel tree, string side, PrintOptions options)
        {
            sb.Append("<section>");
            sb.Append($"<h2>{side}: {Encode(tree.FileName)}</h2>");

            if (tree.TreeNodes.Count == 0)
            {
                sb.Append("<p class=\"empty\">No file loaded.</p>");
            }
            else
            {
                foreach (var group in tree.TreeNodes)
                {
                    AppendRow(sb, group, 0, options);
                }
            }

            sb.Append("</section>");
        }

        private static void AppendRow(StringBuilder sb, object item, int level, PrintOptions options)
        {
            string cls = TreeNodeText.IsGroupRow(item) ? "row group"
                       : TreeNodeText.IsDetailRow(item) ? "row detail"
                       : "row";

            foreach (var line in TreeNodeText.GetLines(item))
            {
                sb.Append($"<div class=\"{cls}\" style=\"padding-left:{level * 18}px\">{Encode(line)}</div>");
            }

            if (options.ExpandedRowsOnly && !(item is TreeNodeBase { IsExpanded: true })) return;

            foreach (var child in TreeNodeText.GetDisplayedChildren(item).ToList())
            {
                AppendRow(sb, child, level + 1, options);
            }
        }

        private static string WrapPage(string title, string subtitle, PrintOptions options, string body)
        {
            var notes = new List<string>();
            if (options.Content != PrintContent.Differences)
                notes.Add(options.ExpandedRowsOnly ? "Expanded rows only" : "All rows");
            notes.Add(options.IncludeDetails ? "Details included" : "No details");
            notes.Add($"Printed {DateTime.Now:yyyy-MM-dd HH:mm}");

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<title>Genealogy Diff Utility - {Encode(title)}</title>
<style>
  body {{ font-family: 'Segoe UI', Arial, sans-serif; font-size: 10pt; margin: 1.5em; color: #000; }}
  h1 {{ font-size: 14pt; margin: 0 0 0.2em 0; }}
  h2 {{ font-size: 11pt; margin: 0 0 0.5em 0; border-bottom: 1px solid #999; padding-bottom: 0.2em; }}
  .meta {{ color: #444; margin-bottom: 1em; }}
  .cols {{ display: grid; grid-template-columns: 1fr 1fr; column-gap: 2em; }}
  .cols section {{ min-width: 0; }}
  .row {{ padding-top: 1px; padding-bottom: 1px; overflow-wrap: anywhere; break-inside: avoid; }}
  .group {{ font-weight: bold; margin-top: 0.6em; }}
  .detail {{ font-style: italic; color: #555; }}
  .empty {{ color: #777; font-style: italic; }}
  pre.diff {{ font-family: 'Courier New', monospace; font-size: 9pt; white-space: pre-wrap; overflow-wrap: anywhere; }}
  @page {{ margin: 1.5cm; }}
  @media print {{ body {{ margin: 0; }} }}
</style>
</head>
<body>
<h1>Genealogy Diff Utility - {Encode(title)}</h1>
<div class=""meta"">{subtitle}<br>{Encode(string.Join(" · ", notes))}</div>
{body}
<script>window.addEventListener('load', function () {{ window.print(); }});</script>
</body>
</html>";
        }

        private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);
    }
}
