using ClosedXML.Excel;
using YaPasakay.Application.Admin;

namespace YaPasakay.Api.Services;

public static class CustomerReportExcel
{
    public static byte[] Build(IReadOnlyList<CustomerReportItem> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Customers");

        sheet.Cell(1, 1).Value = "Customer name";
        sheet.Cell(1, 2).Value = "Mobile";
        sheet.Cell(1, 3).Value = "Joined";
        sheet.Cell(1, 4).Value = "No# rides";
        sheet.Cell(1, 5).Value = "No# cancel";
        sheet.Cell(1, 6).Value = "Booking amount";
        sheet.Cell(1, 7).Value = "Total spent";
        sheet.Range(1, 1, 1, 7).Style.Font.Bold = true;

        var ph = TimeSpan.FromHours(8);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            sheet.Cell(excelRow, 1).Value = row.CustomerName;
            sheet.Cell(excelRow, 2).Value = row.Mobile;
            var joinedPh = row.JoinedAtUtc.Add(ph);
            sheet.Cell(excelRow, 3).Value = joinedPh;
            sheet.Cell(excelRow, 3).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Cell(excelRow, 4).Value = row.TotalRides;
            sheet.Cell(excelRow, 5).Value = row.TotalCancel;
            sheet.Cell(excelRow, 6).Value = row.BookingAmount;
            sheet.Cell(excelRow, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(excelRow, 7).Value = row.TotalSpent;
            sheet.Cell(excelRow, 7).Style.NumberFormat.Format = "#,##0.00";
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
