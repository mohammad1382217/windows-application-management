using System.IO;
using System.IO.Packaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using Microsoft.Win32;

namespace MilOps.Presentation.Services;

/// <summary>
/// Reporting: builds a FlowDocument report and supports both printing (WPF
/// print dialog) and XPS export (a portable, offline-friendly format that can
/// be converted to PDF by any viewer). All printing is local/offline.
/// </summary>
public interface IPrintService
{
    void Print(FlowDocument document, string description);
    bool ExportToXps(FlowDocument document, string suggestedFileName);
    FlowDocument BuildTableReport(string title, string subtitle,
        IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows);
}

public sealed class PrintService : IPrintService
{
    public void Print(FlowDocument document, string description)
    {
        var dialog = new PrintDialog();
        if (!dialog.ShowDialog() == true) return;

        // Clone so the original isn't consumed by the paginator.
        document = Clone(document);
        document.ColumnWidth = dialog.PrintableAreaWidth;
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
        dialog.PrintDocument(paginator, description);
    }

    public bool ExportToXps(FlowDocument document, string suggestedFileName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "XPS Document (*.xps)|*.xps",
            FileName = suggestedFileName
        };
        if (dlg.ShowDialog() != true) return false;

        document = Clone(document);
        document.ColumnWidth = 816; // ~8.5in at 96dpi
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(816, 1056);

        using var package = Package.Open(dlg.FileName, FileMode.Create);
        using var xps = new XpsDocument(package, CompressionOption.Normal);
        var writer = XpsDocument.CreateXpsDocumentWriter(xps);
        writer.Write(paginator);
        return true;
    }

    /// <summary>
    /// Builds a titled, printable table report from plain strings. This is the
    /// shared renderer for soldier lists, schedules, registers, leaves, etc.
    /// </summary>
    public FlowDocument BuildTableReport(string title, string subtitle,
        IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            PagePadding = new Thickness(48)
        };

        var titlePara = new Paragraph(new Run(title))
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        doc.Blocks.Add(titlePara);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            doc.Blocks.Add(new Paragraph(new Run(subtitle))
            {
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 12)
            });
        }

        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.DarkGray, BorderThickness = new Thickness(0.5) };
        var headerList = headers.ToList();
        for (var i = 0; i < headerList.Count; i++)
            table.Columns.Add(new TableColumn { Width = new GridLength(150) });

        var headerRow = new TableRowGroup { Background = (Brush)System.Windows.Application.Current.FindResource("PrimaryBrush")! };
        var hr = new TableRow();
        foreach (var h in headerList)
        {
            hr.Cells.Add(new Cell(new Paragraph(new Run(h))
            {
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            }));
        }
        headerRow.Rows.Add(hr);
        table.RowGroups.Add(headerRow);

        var bodyGroup = new TableRowGroup();
        var alt = false;
        foreach (var row in rows)
        {
            var tr = new TableRow();
            if (alt) tr.Background = Brushes.WhiteSmoke;
            foreach (var cell in row)
                tr.Cells.Add(new Cell(new Paragraph(new Run(cell ?? string.Empty))));
            bodyGroup.Rows.Add(tr);
            alt = !alt;
        }
        table.RowGroups.Add(bodyGroup);
        doc.Blocks.Add(table);

        doc.Blocks.Add(new Paragraph(new Run(
            $"Generated {DateTime.Now:yyyy-MM-dd HH:mm} on {Environment.MachineName}"))
        {
            FontSize = 9,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 12, 0, 0)
        });

        return doc;
    }

    private static FlowDocument Clone(FlowDocument doc)
    {
        // Deep-clone via XAML round-trip so the same document can be re-paginated.
        var xaml = XamlWriter.Save(doc);
        using var reader = new StringReader(xaml);
        using var xml = new System.Xml.XmlTextReader(reader);
        return (FlowDocument)XamlReader.Load(xml);
    }

    private sealed class Cell : TableCell
    {
        public Cell(Block content)
        {
            Blocks.Add(content);
            BorderBrush = Brushes.DarkGray;
            BorderThickness = new Thickness(0.5);
            Padding = new Thickness(4, 2, 4, 2);
        }
    }
}
