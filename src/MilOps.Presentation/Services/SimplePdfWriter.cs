using System.IO;
using System.Text;

namespace MilOps.Presentation.Services;

/// <summary>
/// Minimal dependency-free PDF writer: one JPEG image per page, sized to the
/// given page dimensions (in PDF points). Used by <see cref="PrintService"/>
/// to export pixel-perfect A5 report pages — Persian text renders exactly as
/// WPF laid it out, with no printer, driver, or third-party library involved.
/// </summary>
internal static class SimplePdfWriter
{
    /// <param name="pageWidthPt">Page width in PDF points (1 pt = 1/72 inch).</param>
    /// <param name="pageHeightPt">Page height in PDF points.</param>
    /// <param name="jpegPages">One baseline JPEG per page, each with its pixel size.</param>
    public static void Write(string path, double pageWidthPt, double pageHeightPt,
        IReadOnlyList<(byte[] Jpeg, int PixelWidth, int PixelHeight)> jpegPages)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        var offsets = new List<long>();          // byte offset of every object, 1-based
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        void WriteRaw(byte[] bytes) => fs.Write(bytes, 0, bytes.Length);
        void WriteText(string s) => WriteRaw(Encoding.ASCII.GetBytes(s));
        void BeginObj(int id)
        {
            while (offsets.Count < id) offsets.Add(0);
            offsets[id - 1] = fs.Position;
            WriteText($"{id} 0 obj\n");
        }

        WriteText("%PDF-1.4\n");
        // Binary marker so transfer tools treat the file as binary.
        WriteRaw(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        var n = jpegPages.Count;
        // Object layout: 1=Catalog, 2=Pages, then per page i(0-based):
        //   3+i*3 = Page, 4+i*3 = Contents, 5+i*3 = Image XObject.
        BeginObj(1);
        WriteText("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        BeginObj(2);
        var kids = string.Join(" ", Enumerable.Range(0, n).Select(i => $"{3 + i * 3} 0 R"));
        WriteText($"<< /Type /Pages /Kids [{kids}] /Count {n} >>\nendobj\n");

        var w = pageWidthPt.ToString("0.##", inv);
        var h = pageHeightPt.ToString("0.##", inv);

        for (var i = 0; i < n; i++)
        {
            var (jpeg, px, py) = jpegPages[i];
            int pageId = 3 + i * 3, contentId = 4 + i * 3, imageId = 5 + i * 3;

            BeginObj(pageId);
            WriteText($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] " +
                      $"/Resources << /XObject << /Im{i} {imageId} 0 R >> >> " +
                      $"/Contents {contentId} 0 R >>\nendobj\n");

            // Scale the image to fill the page exactly.
            var content = $"q {w} 0 0 {h} 0 0 cm /Im{i} Do Q";
            var contentBytes = Encoding.ASCII.GetBytes(content);
            BeginObj(contentId);
            WriteText($"<< /Length {contentBytes.Length} >>\nstream\n");
            WriteRaw(contentBytes);
            WriteText("\nendstream\nendobj\n");

            BeginObj(imageId);
            WriteText($"<< /Type /XObject /Subtype /Image /Width {px} /Height {py} " +
                      "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode " +
                      $"/Length {jpeg.Length} >>\nstream\n");
            WriteRaw(jpeg);
            WriteText("\nendstream\nendobj\n");
        }

        var xrefPos = fs.Position;
        WriteText($"xref\n0 {offsets.Count + 1}\n");
        WriteText("0000000000 65535 f \n");
        foreach (var off in offsets)
            WriteText($"{off.ToString("0000000000", inv)} 00000 n \n");
        WriteText($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\n" +
                  $"startxref\n{xrefPos}\n%%EOF\n");
    }
}
