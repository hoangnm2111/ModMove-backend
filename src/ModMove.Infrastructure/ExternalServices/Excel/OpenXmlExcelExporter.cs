using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.Excel;

/// <summary>
/// Zero-dependency Office Open XML (.xlsx) generator using <see cref="ZipArchive"/> and <see cref="XDocument"/> (DECISIONS B16).
/// </summary>
public sealed class OpenXmlExcelExporter : IExcelExporter
{
    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly char[] InvalidSheetChars = [':', '\\', '/', '?', '*', '[', ']'];

    public byte[] ExportToExcel<T>(string sheetName, IReadOnlyList<ExcelColumn<T>> columns, IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        var safeSheetName = SanitizeSheetName(sheetName);
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteXmlEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteXmlEntry(archive, "_rels/.rels", BuildRootRels());
            WriteXmlEntry(archive, "xl/workbook.xml", BuildWorkbook(safeSheetName));
            WriteXmlEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRels());
            WriteXmlEntry(archive, "xl/styles.xml", BuildStyles());
            WriteXmlEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheet(columns, rows));
        }

        return stream.ToArray();
    }

    private static string SanitizeSheetName(string? sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
            return "Report";

        var cleaned = new string(sheetName.Trim().Where(c => !InvalidSheetChars.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(cleaned))
            return "Report";

        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    private static XDocument BuildContentTypes() =>
        new(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(ContentTypesNs + "Types",
                new XElement(ContentTypesNs + "Default",
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ContentTypesNs + "Default",
                    new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(ContentTypesNs + "Override",
                    new XAttribute("PartName", "/xl/workbook.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ContentTypesNs + "Override",
                    new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ContentTypesNs + "Override",
                    new XAttribute("PartName", "/xl/styles.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))));

    private static XDocument BuildRootRels() =>
        new(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(PackageRelNs + "Relationships",
                new XElement(PackageRelNs + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml"))));

    private static XDocument BuildWorkbook(string sheetName) =>
        new(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(MainNs + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", RelNs.NamespaceName),
                new XElement(MainNs + "sheets",
                    new XElement(MainNs + "sheet",
                        new XAttribute("name", sheetName),
                        new XAttribute("sheetId", "1"),
                        new XAttribute(RelNs + "id", "rId1")))));

    private static XDocument BuildWorkbookRels() =>
        new(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(PackageRelNs + "Relationships",
                new XElement(PackageRelNs + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml")),
                new XElement(PackageRelNs + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                    new XAttribute("Target", "styles.xml"))));

    private static XDocument BuildStyles() =>
        new(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(MainNs + "styleSheet",
                new XElement(MainNs + "fonts",
                    new XAttribute("count", "2"),
                    new XElement(MainNs + "font",
                        new XElement(MainNs + "sz", new XAttribute("val", "11")),
                        new XElement(MainNs + "name", new XAttribute("val", "Calibri"))),
                    new XElement(MainNs + "font",
                        new XElement(MainNs + "b"),
                        new XElement(MainNs + "sz", new XAttribute("val", "11")),
                        new XElement(MainNs + "name", new XAttribute("val", "Calibri")))),
                new XElement(MainNs + "fills",
                    new XAttribute("count", "2"),
                    new XElement(MainNs + "fill", new XElement(MainNs + "patternFill", new XAttribute("patternType", "none"))),
                    new XElement(MainNs + "fill", new XElement(MainNs + "patternFill", new XAttribute("patternType", "gray125")))),
                new XElement(MainNs + "borders",
                    new XAttribute("count", "1"),
                    new XElement(MainNs + "border",
                        new XElement(MainNs + "left"),
                        new XElement(MainNs + "right"),
                        new XElement(MainNs + "top"),
                        new XElement(MainNs + "bottom"),
                        new XElement(MainNs + "diagonal"))),
                new XElement(MainNs + "cellStyleXfs",
                    new XAttribute("count", "1"),
                    new XElement(MainNs + "xf",
                        new XAttribute("numFmtId", "0"),
                        new XAttribute("fontId", "0"),
                        new XAttribute("fillId", "0"),
                        new XAttribute("borderId", "0"))),
                new XElement(MainNs + "cellXfs",
                    new XAttribute("count", "2"),
                    new XElement(MainNs + "xf",
                        new XAttribute("numFmtId", "0"),
                        new XAttribute("fontId", "0"),
                        new XAttribute("fillId", "0"),
                        new XAttribute("borderId", "0"),
                        new XAttribute("xfId", "0")),
                    new XElement(MainNs + "xf",
                        new XAttribute("numFmtId", "0"),
                        new XAttribute("fontId", "1"),
                        new XAttribute("fillId", "0"),
                        new XAttribute("borderId", "0"),
                        new XAttribute("xfId", "0"),
                        new XAttribute("applyFont", "1")))));

    private static XDocument BuildWorksheet<T>(IReadOnlyList<ExcelColumn<T>> columns, IReadOnlyList<T> rows)
    {
        var sheetData = new XElement(MainNs + "sheetData");

        // Header row (Row 1, bold style index 1)
        var headerRow = new XElement(MainNs + "row", new XAttribute("r", 1));
        for (var colIndex = 0; colIndex < columns.Count; colIndex++)
        {
            var cellRef = ColumnLetter(colIndex) + "1";
            headerRow.Add(CreateInlineStringCell(cellRef, columns[colIndex].Header, styleIndex: 1));
        }

        sheetData.Add(headerRow);

        // Data rows (Row 2..N+1)
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var excelRowNumber = rowIndex + 2;
            var rowElement = new XElement(MainNs + "row", new XAttribute("r", excelRowNumber));
            var item = rows[rowIndex];

            for (var colIndex = 0; colIndex < columns.Count; colIndex++)
            {
                var cellRef = ColumnLetter(colIndex) + excelRowNumber.ToString(CultureInfo.InvariantCulture);
                var rawValue = columns[colIndex].ValueSelector(item);
                rowElement.Add(CreateCell(cellRef, rawValue));
            }

            sheetData.Add(rowElement);
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(MainNs + "worksheet", sheetData));
    }

    private static XElement CreateCell(string cellRef, object? value) =>
        value switch
        {
            int n => CreateNumberCell(cellRef, n.ToString(CultureInfo.InvariantCulture)),
            long n => CreateNumberCell(cellRef, n.ToString(CultureInfo.InvariantCulture)),
            decimal n => CreateNumberCell(cellRef, n.ToString(CultureInfo.InvariantCulture)),
            double n => CreateNumberCell(cellRef, n.ToString(CultureInfo.InvariantCulture)),
            float n => CreateNumberCell(cellRef, n.ToString(CultureInfo.InvariantCulture)),
            null => CreateInlineStringCell(cellRef, ""),
            _ => CreateInlineStringCell(cellRef, Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""),
        };

    private static XElement CreateNumberCell(string cellRef, string numericLiteral) =>
        new(MainNs + "c",
            new XAttribute("r", cellRef),
            new XAttribute("t", "n"),
            new XElement(MainNs + "v", numericLiteral));

    private static XElement CreateInlineStringCell(string cellRef, string text, int? styleIndex = null)
    {
        var cell = new XElement(MainNs + "c", new XAttribute("r", cellRef));
        if (styleIndex is { } s)
            cell.Add(new XAttribute("s", s));
        cell.Add(new XAttribute("t", "inlineStr"));

        var tElement = new XElement(MainNs + "t",
            new XAttribute(XNamespace.Xml + "space", "preserve"),
            text);
        cell.Add(new XElement(MainNs + "is", tElement));
        return cell;
    }

    private static string ColumnLetter(int zeroBasedIndex)
    {
        var dividend = zeroBasedIndex + 1;
        var name = new StringBuilder();
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            name.Insert(0, (char)('A' + modulo));
            dividend = (dividend - modulo) / 26;
        }

        return name.ToString();
    }

    private static void WriteXmlEntry(ZipArchive archive, string entryPath, XDocument document)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}

