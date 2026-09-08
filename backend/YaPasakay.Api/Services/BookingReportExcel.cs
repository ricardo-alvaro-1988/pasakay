using ClosedXML.Excel;
using YaPasakay.Application.Admin;

namespace YaPasakay.Api.Services;

public static class BookingReportExcel
{
    public static byte[] Build(IReadOnlyList<BookingReportItem> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Bookings");

        sheet.Cell(1, 1).Value = "Date time";
        sheet.Cell(1, 2).Value = "Booking no";
        sheet.Cell(1, 3).Value = "Rider";
        sheet.Cell(1, 4).Value = "Customer";
        sheet.Cell(1, 5).Value = "Rider comm";
        sheet.Cell(1, 6).Value = "System comm";
        sheet.Cell(1, 7).Value = "Operator comm";
        sheet.Cell(1, 8).Value = "Promo";
        sheet.Cell(1, 9).Value = "Fare";
        sheet.Range(1, 1, 1, 9).Style.Font.Bold = true;

        var ph = TimeSpan.FromHours(8);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            var whenPh = row.DateUtc.Add(ph);
            sheet.Cell(excelRow, 1).Value = whenPh;
            sheet.Cell(excelRow, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Cell(excelRow, 2).Value = row.Reference;
            sheet.Cell(excelRow, 3).Value = row.RiderName;
            sheet.Cell(excelRow, 4).Value = row.CustomerName;
            sheet.Cell(excelRow, 5).Value = row.RiderCommission;
            sheet.Cell(excelRow, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(excelRow, 6).Value = row.SystemCommission;
            sheet.Cell(excelRow, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(excelRow, 7).Value = row.OperatorCommission;
            sheet.Cell(excelRow, 7).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(excelRow, 8).Value = row.Promo;
            sheet.Cell(excelRow, 8).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(excelRow, 9).Value = row.Fare;
            sheet.Cell(excelRow, 9).Style.NumberFormat.Format = "#,##0.00";
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
