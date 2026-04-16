using ClosedXML.Excel;

namespace DuongVanDung.WebApp.Helpers;

public static class ExcelHelper
{
    /// <summary>
    /// Tạo file .xlsx từ danh sách headers và rows (mỗi row là mảng string).
    /// </summary>
    public static byte[] Build(string sheetName, string[] headers, IEnumerable<string[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName.Length > 31 ? sheetName[..31] : sheetName);

        // Header row
        for (int col = 1; col <= headers.Length; col++)
        {
            var cell = ws.Cell(1, col);
            cell.Value = headers[col - 1];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data rows
        int rowNum = 2;
        bool even = false;
        foreach (var row in rows)
        {
            for (int col = 1; col <= row.Length; col++)
            {
                var cell = ws.Cell(rowNum, col);
                cell.Value = row[col - 1];
                if (even) cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
            }
            even = !even;
            rowNum++;
        }

        // Auto-fit columns
        ws.Columns().AdjustToContents();

        // Freeze header
        ws.SheetView.Freeze(1, 0);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
