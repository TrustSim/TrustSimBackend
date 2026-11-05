using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using ITextDocument = iText.Layout.Document;
using ITextPageSize = iText.Kernel.Geom.PageSize;

namespace TrustSimBackend.Tests.Common;

public static class PdfTestSampleBuilder
{
    public static string ResolveTestTrueTypeFontPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts", "times.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "times.ttf"),
            @"C:\Windows\Fonts\times.ttf",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            @"C:\Windows\Fonts\arial.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"
        };

        return candidates.First(File.Exists);
    }

    public static byte[] CreateUnicodeEmbeddedPdfBytes(string vietnameseText)
    {
        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        using (var pdf = new PdfDocument(writer))
        using (var doc = new ITextDocument(pdf, ITextPageSize.A4))
        {
            var fontPath = ResolveTestTrueTypeFontPath();
            var font = PdfFontFactory.CreateFont(
                fontPath,
                PdfEncodings.IDENTITY_H,
                PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
            font.SetSubset(true);

            doc.SetFont(font);
            doc.Add(new Paragraph(vietnameseText));
        }

        return ms.ToArray();
    }

    public static byte[] CreateUnembeddedStandardFontPdfBytes(string text)
    {
        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        using (var pdf = new PdfDocument(writer))
        using (var doc = new ITextDocument(pdf, ITextPageSize.A4))
        {
            var unembeddedFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            doc.SetFont(unembeddedFont);
            doc.Add(new Paragraph(text));
        }

        return ms.ToArray();
    }

    public static byte[] CreateLegacyNonUnicodeFontPdfBytes()
    {
        var baseBytes = CreateUnicodeEmbeddedPdfBytes("Van ban dung font bang ma cu VNI");

        using var inputStream = new MemoryStream(baseBytes);
        using var outputStream = new MemoryStream();
        using (var reader = new PdfReader(inputStream))
        using (var writer = new PdfWriter(outputStream))
        using (var pdfDoc = new PdfDocument(reader, writer))
        {
            var page = pdfDoc.GetPage(1);
            var resources = page.GetResources().GetPdfObject();
            var fontDict = resources.GetAsDictionary(PdfName.Font);
            if (fontDict != null)
            {
                foreach (var key in fontDict.KeySet())
                {
                    var fontObj = fontDict.GetAsDictionary(key);
                    fontObj?.Put(PdfName.BaseFont, new PdfName("VNI-Times"));
                }
            }
        }

        return outputStream.ToArray();
    }

    public static byte[] CreateMissingToUnicodeCMapPdfBytes()
    {
        var baseBytes = CreateUnicodeEmbeddedPdfBytes("Hợp đồng kiểm thử thiếu bảng ToUnicode CMap");

        using var inputStream = new MemoryStream(baseBytes);
        using var outputStream = new MemoryStream();
        using (var reader = new PdfReader(inputStream))
        using (var writer = new PdfWriter(outputStream))
        using (var pdfDoc = new PdfDocument(reader, writer))
        {
            var page = pdfDoc.GetPage(1);
            var resources = page.GetResources().GetPdfObject();
            var fontDict = resources.GetAsDictionary(PdfName.Font);
            if (fontDict != null)
            {
                foreach (var key in fontDict.KeySet())
                {
                    var fontObj = fontDict.GetAsDictionary(key);
                    fontObj?.Remove(PdfName.ToUnicode);
                }
            }
        }

        return outputStream.ToArray();
    }
}
