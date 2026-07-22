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
using MilOps.Presentation.Common;
using WpfApp = System.Windows.Application;

namespace MilOps.Presentation.Services;

/// <summary>
/// Builds A5-sized FlowDocument reports and prints or exports them to XPS.
/// All layout is pre-calculated for A5 (148 × 210 mm) so content fits without
/// scaling regardless of the printer's default paper size.
/// </summary>
public interface IPrintService
{
    void Print(FlowDocument document, string description);
    bool ExportToXps(FlowDocument document, string suggestedFileName);

    /// <summary>
    /// Saves the report as a real PDF (A5 pages rendered at 300 DPI) with no
    /// dependency on any installed printer, then opens it in the default
    /// viewer. Returns false if the user cancelled the save dialog.
    /// </summary>
    bool ExportToPdf(FlowDocument document, string suggestedFileName);

    /// <summary>Dialog-free PDF export straight to an exact path — no SaveFileDialog prompt.</summary>
    void ExportToPdfFile(FlowDocument document, string path);

    FlowDocument BuildTableReport(string title, string subtitle,
        IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows);

    /// <summary>
    /// Builds a multi-section A5 report: free-text header lines (e.g. a bio
    /// block), followed by one heading + table per section. Sections with no
    /// rows still render a "بدون سابقه" placeholder row rather than vanishing.
    /// </summary>
    FlowDocument BuildMultiSectionReport(string title, string subtitle,
        IEnumerable<string>? headerLines,
        IEnumerable<(string SectionTitle, string[] Headers, IEnumerable<string[]> Rows)> sections);
}

public sealed class PrintService : IPrintService
{
    // A5 in WPF device-independent units (96 dpi; 1 mm = 96/25.4 ≈ 3.7795 units)
    private const double A5Width   = 559.37;   // 148 mm
    private const double A5Height  = 793.70;   // 210 mm
    private const double PageMargin = 44.0;    // ~11.6 mm — all four sides

    // Estedad embedded font (handles Persian + Latin glyphs)
    private static readonly FontFamily AppFont =
        new(new Uri("pack://application:,,,/"), "./Fonts/#Estedad");

    private readonly IAppSettingsStore _settings;

    public PrintService(IAppSettingsStore settings) => _settings = settings;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Print(FlowDocument document, string description)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;   // null = cancelled, false = closed

        document = Clone(document);
        ApplyA5(document);

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(A5Width, A5Height);
        dialog.PrintDocument(paginator, description);
    }

    public bool ExportToPdf(FlowDocument document, string suggestedFileName)
    {
        var folder = _settings.Load().ExportFolder;
        var dlg = new SaveFileDialog
        {
            Filter   = "PDF (*.pdf)|*.pdf",
            FileName = suggestedFileName,
            InitialDirectory = folder is not null && Directory.Exists(folder) ? folder : null
        };
        if (dlg.ShowDialog() != true) return false;

        ExportToPdfFile(document, dlg.FileName);

        // Open the result so the user immediately sees the output exists.
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName)
            { UseShellExecute = true });
        }
        catch
        {
            // No PDF viewer registered — the file is still saved; not an error.
        }
        return true;
    }

    /// <summary>Dialog-free core of the PDF export (also used by tests).</summary>
    public void ExportToPdfFile(FlowDocument document, string path)
    {
        document = Clone(document);
        ApplyA5(document);
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(A5Width, A5Height);
        paginator.ComputePageCount();

        // Render every page at 300 DPI; on paper this is indistinguishable
        // from vector output and Persian shaping is exactly what WPF shows.
        const double dpi = 300.0;
        var pxW = (int)Math.Round(A5Width  * dpi / 96.0);
        var pxH = (int)Math.Round(A5Height * dpi / 96.0);
        var pages = new List<(byte[] Jpeg, int PixelWidth, int PixelHeight)>();

        for (var i = 0; i < paginator.PageCount; i++)
        {
            using var page = paginator.GetPage(i);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                pxW, pxH, dpi, dpi, PixelFormats.Pbgra32);

            var bg = new DrawingVisual();
            using (var dc = bg.RenderOpen())
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, A5Width, A5Height));
            rtb.Render(bg);
            rtb.Render(page.Visual);

            var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            pages.Add((ms.ToArray(), pxW, pxH));
        }

        // A5 in PDF points (1 pt = 1/72"): 148 mm × 210 mm.
        SimplePdfWriter.Write(path, 419.53, 595.28, pages);
    }

    public bool ExportToXps(FlowDocument document, string suggestedFileName)
    {
        var dlg = new SaveFileDialog
        {
            Filter   = "XPS Document (*.xps)|*.xps",
            FileName = suggestedFileName
        };
        if (dlg.ShowDialog() != true) return false;

        document = Clone(document);
        ApplyA5(document);
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(A5Width, A5Height);

        using var package = Package.Open(dlg.FileName, FileMode.Create);
        using var xps     = new XpsDocument(package, CompressionOption.Normal);
        XpsDocument.CreateXpsDocumentWriter(xps).Write(paginator);
        return true;
    }

    /// <summary>
    /// Builds a right-to-left A5 table report with the app font and a Jalali
    /// date footer.  Column widths are shared equally across the usable width.
    /// </summary>
    public FlowDocument BuildTableReport(
        string title,
        string subtitle,
        IEnumerable<string> headers,
        IEnumerable<IEnumerable<string>> rows)
    {
        double usable = A5Width - PageMargin * 2;

        var doc = new FlowDocument
        {
            FontFamily    = AppFont,
            FontSize      = 10,
            PageWidth     = A5Width,
            PageHeight    = A5Height,
            PagePadding   = new Thickness(PageMargin),
            ColumnWidth   = usable,
            FlowDirection = FlowDirection.RightToLeft,
        };

        // Title
        doc.Blocks.Add(new Paragraph(new Run(title))
        {
            FontSize      = 15,
            FontWeight    = FontWeights.Bold,
            TextAlignment = TextAlignment.Right,
            Margin        = new Thickness(0, 0, 0, 2),
        });

        // Subtitle / description
        if (!string.IsNullOrWhiteSpace(subtitle))
            doc.Blocks.Add(new Paragraph(new Run(subtitle))
            {
                FontSize      = 10,
                Foreground    = Brushes.DimGray,
                TextAlignment = TextAlignment.Right,
                Margin        = new Thickness(0, 0, 0, 10),
            });

        // Table
        var headerList = headers.ToList();
        double colWidth = usable / Math.Max(headerList.Count, 1);

        var table = new Table
        {
            CellSpacing     = 0,
            BorderBrush     = Brushes.DarkSlateGray,
            BorderThickness = new Thickness(0.5),
        };

        foreach (var _ in headerList)
            table.Columns.Add(new TableColumn { Width = new GridLength(colWidth) });

        // Header row (primary-colour background)
        var headerGroup = new TableRowGroup
        {
            Background = (Brush)WpfApp.Current.FindResource("PrimaryBrush")!
        };
        var headerRow = new TableRow();
        foreach (var h in headerList)
            headerRow.Cells.Add(MakeCell(h, isBold: true, Brushes.White));
        headerGroup.Rows.Add(headerRow);
        table.RowGroups.Add(headerGroup);

        // Body rows (alternating stripe)
        var bodyGroup = new TableRowGroup();
        var stripe    = false;
        var stripeBg  = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xFA));
        foreach (var row in rows)
        {
            var tr = new TableRow { Background = stripe ? stripeBg : Brushes.White };
            foreach (var cell in row)
                tr.Cells.Add(MakeCell(cell ?? "—"));
            bodyGroup.Rows.Add(tr);
            stripe = !stripe;
        }
        table.RowGroups.Add(bodyGroup);
        doc.Blocks.Add(table);

        // Footer: Jalali print date + machine name
        string jalaliNow = PersianDate.ToJalali(DateTime.Now);
        doc.Blocks.Add(new Paragraph(
            new Run($"تاریخ چاپ: {jalaliNow}  —  {Environment.MachineName}"))
        {
            FontSize      = 8,
            Foreground    = Brushes.Gray,
            TextAlignment = TextAlignment.Right,
            Margin        = new Thickness(0, 10, 0, 0),
        });

        return doc;
    }

    public FlowDocument BuildMultiSectionReport(
        string title,
        string subtitle,
        IEnumerable<string>? headerLines,
        IEnumerable<(string SectionTitle, string[] Headers, IEnumerable<string[]> Rows)> sections)
    {
        double usable = A5Width - PageMargin * 2;

        var doc = new FlowDocument
        {
            FontFamily    = AppFont,
            FontSize      = 10,
            PageWidth     = A5Width,
            PageHeight    = A5Height,
            PagePadding   = new Thickness(PageMargin),
            ColumnWidth   = usable,
            FlowDirection = FlowDirection.RightToLeft,
        };

        doc.Blocks.Add(new Paragraph(new Run(title))
        {
            FontSize      = 15,
            FontWeight    = FontWeights.Bold,
            TextAlignment = TextAlignment.Right,
            Margin        = new Thickness(0, 0, 0, 2),
        });

        if (!string.IsNullOrWhiteSpace(subtitle))
            doc.Blocks.Add(new Paragraph(new Run(subtitle))
            {
                FontSize      = 10,
                Foreground    = Brushes.DimGray,
                TextAlignment = TextAlignment.Right,
                Margin        = new Thickness(0, 0, 0, 6),
            });

        if (headerLines is not null)
            foreach (var line in headerLines)
                doc.Blocks.Add(new Paragraph(new Run(line))
                {
                    FontSize      = 11,
                    TextAlignment = TextAlignment.Right,
                    Margin        = new Thickness(0, 0, 0, 2),
                });

        var primaryBrush = (Brush)WpfApp.Current.FindResource("PrimaryBrush")!;

        foreach (var section in sections)
        {
            doc.Blocks.Add(new Paragraph(new Run(section.SectionTitle))
            {
                FontSize      = 12,
                FontWeight    = FontWeights.Bold,
                Foreground    = primaryBrush,
                TextAlignment = TextAlignment.Right,
                Margin        = new Thickness(0, 14, 0, 4),
            });

            double colWidth = usable / Math.Max(section.Headers.Length, 1);
            var table = new Table
            {
                CellSpacing     = 0,
                BorderBrush     = Brushes.DarkSlateGray,
                BorderThickness = new Thickness(0.5),
            };
            foreach (var _ in section.Headers)
                table.Columns.Add(new TableColumn { Width = new GridLength(colWidth) });

            var headerGroup = new TableRowGroup { Background = primaryBrush };
            var headerRow = new TableRow();
            foreach (var h in section.Headers)
                headerRow.Cells.Add(MakeCell(h, isBold: true, Brushes.White));
            headerGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerGroup);

            var bodyGroup = new TableRowGroup();
            var stripe = false;
            var stripeBg = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xFA));
            var rowList = section.Rows.ToList();
            if (rowList.Count == 0)
            {
                var placeholder = new TableRow { Background = Brushes.White };
                var cell = MakeCell("بدون سابقه");
                cell.ColumnSpan = section.Headers.Length;
                placeholder.Cells.Add(cell);
                bodyGroup.Rows.Add(placeholder);
            }
            else
            {
                foreach (var row in rowList)
                {
                    var tr = new TableRow { Background = stripe ? stripeBg : Brushes.White };
                    foreach (var cell in row)
                        tr.Cells.Add(MakeCell(cell ?? "—"));
                    bodyGroup.Rows.Add(tr);
                    stripe = !stripe;
                }
            }
            table.RowGroups.Add(bodyGroup);
            doc.Blocks.Add(table);
        }

        string jalaliNow = PersianDate.ToJalali(DateTime.Now);
        doc.Blocks.Add(new Paragraph(
            new Run($"تاریخ چاپ: {jalaliNow}  —  {Environment.MachineName}"))
        {
            FontSize      = 8,
            Foreground    = Brushes.Gray,
            TextAlignment = TextAlignment.Right,
            Margin        = new Thickness(0, 10, 0, 0),
        });

        return doc;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ApplyA5(FlowDocument doc)
    {
        doc.PageWidth   = A5Width;
        doc.PageHeight  = A5Height;
        doc.ColumnWidth = A5Width - PageMargin * 2;
    }

    private static TableCell MakeCell(string text,
        bool   isBold    = false,
        Brush? foreground = null)
    {
        var para = new Paragraph(new Run(text))
        {
            TextAlignment = TextAlignment.Center,
        };
        if (isBold)      para.FontWeight = FontWeights.Bold;
        if (foreground != null) para.Foreground = foreground;

        return new TableCell(para)
        {
            BorderBrush     = Brushes.DarkSlateGray,
            BorderThickness = new Thickness(0.5),
            Padding         = new Thickness(5, 4, 5, 4),
        };
    }

    private static FlowDocument Clone(FlowDocument doc)
    {
        // Deep-clone via XAML round-trip so the same document can be re-paginated.
        var xaml = XamlWriter.Save(doc);
        using var sr  = new StringReader(xaml);
        using var xml = new System.Xml.XmlTextReader(sr);
        return (FlowDocument)XamlReader.Load(xml);
    }
}
