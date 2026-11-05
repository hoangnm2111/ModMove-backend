using System.IO.Compression;
using System.Xml.Linq;
using ModMove.Application.Common.Interfaces;
using ModMove.Infrastructure.ExternalServices.Excel;

namespace ModMove.IntegrationTests;

/// <summary>
/// Dedicated unit tests for OpenXmlExcelExporter (DECISIONS B16, zero-dependency Excel generator).
/// Tests happy paths and XML/OpenXML edge cases.
/// </summary>
public class OpenXmlExcelExporterUnitTests
{
    private readonly OpenXmlExcelExporter _exporter = new();

    private sealed record TestItem(string Name, int Count, decimal Price, string? Note);

    [Fact]
    public void ExportToExcel_produces_valid_xlsx_archive_with_data_happy_case()
    {
        var columns = new ExcelColumn<TestItem>[]
        {
            new("Tên mặt hàng", x => x.Name),
            new("Số lượng", x => x.Count),
            new("Đơn giá", x => x.Price),
            new("Ghi chú", x => x.Note),
        };

        var rows = new List<TestItem>
        {
            new("Bàn làm việc", 2, 1_500_000m, "Hàng dễ xước"),
            new("Ghế xoay", 5, 800_000m, null),
        };

        var bytes = _exporter.ExportToExcel("SanPham", columns, rows);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        using var zipStream = new MemoryStream(bytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheetEntry);

        using var sheetStream = sheetEntry.Open();
        var doc = XDocument.Load(sheetStream);
        var xml = doc.ToString();

        Assert.Contains("Tên mặt hàng", xml);
        Assert.Contains("Bàn làm việc", xml);
        Assert.Contains("1500000", xml);
        Assert.Contains("Ghế xoay", xml);
    }

    [Fact]
    public void ExportToExcel_handles_empty_rows_list_with_only_headers_edge_case()
    {
        var columns = new ExcelColumn<TestItem>[]
        {
            new("Cột A", x => x.Name),
            new("Cột B", x => x.Count),
        };

        var emptyRows = new List<TestItem>();

        var bytes = _exporter.ExportToExcel("EmptySheet", columns, emptyRows);

        using var zipStream = new MemoryStream(bytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheetEntry);

        using var sheetStream = sheetEntry.Open();
        var doc = XDocument.Load(sheetStream);
        var xml = doc.ToString();

        Assert.Contains("Cột A", xml);
        Assert.Contains("Cột B", xml);
        // Header row index is 1, no row 2
        Assert.Contains("r=\"1\"", xml);
        Assert.DoesNotContain("r=\"2\"", xml);
    }

    [Fact]
    public void ExportToExcel_escapes_special_xml_characters_in_text_cells_edge_case()
    {
        var columns = new ExcelColumn<TestItem>[]
        {
            new("Tiêu đề <đặc biệt> & test", x => x.Name),
            new("Ghi chú", x => x.Note),
        };

        var rows = new List<TestItem>
        {
            new("<script>alert('xss & attack')</script>", 1, 100m, "Dấu \"ngoặc kép\" và 'ngoặc đơn' & ký tự < >"),
        };

        var bytes = _exporter.ExportToExcel("EscapingTest", columns, rows);

        using var zipStream = new MemoryStream(bytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheetEntry);

        // Loading via XDocument verifies the XML is well-formed
        using var sheetStream = sheetEntry.Open();
        var doc = XDocument.Load(sheetStream);
        Assert.NotNull(doc.Root);

        // Cell values in decoded XDocument match original text without XML corruption
        var textValues = doc.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value).ToList();
        Assert.Contains("<script>alert('xss & attack')</script>", textValues);
        Assert.Contains("Dấu \"ngoặc kép\" và 'ngoặc đơn' & ký tự < >", textValues);
    }
}

