namespace ModMove.Application.Common.Interfaces;

public sealed record ExcelColumn<T>(string Header, Func<T, object?> ValueSelector);

/// <summary>Builds an Office Open XML (.xlsx) workbook in memory (DECISIONS B16, UC-ADM-23).</summary>
public interface IExcelExporter
{
    byte[] ExportToExcel<T>(string sheetName, IReadOnlyList<ExcelColumn<T>> columns, IReadOnlyList<T> rows);
}

