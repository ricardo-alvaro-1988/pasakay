using ClosedXML.Excel;
using YaPasakay.Application.Admin;

namespace YaPasakay.Api.Services;

public static class RiderReportExcel
{
    public static byte[] Build(IReadOnlyList<RiderReportItem> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Riders");

        sheet.Cell(1, 1).Value = "Rider name";
        sheet.Cell(1, 2).Value = "Plate";
        sheet.Cell(1, 3).Value = "Registration";
        sheet.Cell(1, 4).Value = "Mobile";
        sheet.Cell(1, 5).Value = "Joined";
        sheet.Cell(1, 6).Value = "No# rides";
        sheet.Cell(1, 7).Value = "No# cancel";
        sheet.Cell(1, 8).Value = "Rider income";
        sheet.Cell(1, 9).Value = "Booking amount";
        sheet.Range(1, 1, 1, 9).Style.Font.Bold = true;

        var ph = TimeSpan.FromHours(8);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            sheet.Cell(excelRow, 1).Value = row.RiderName;
            sheet.Cell(excelRow, 2).Value = row.PlateNumber;
            sheet.Cell(excelRow, 3).Value = row.VehicleFranchiseNumber;
            sheet.Cell(excelRow, 4).Value = row.Mobile;
            var joinedPh = row.JoinedAtUtc.Add(ph);
            sheet.Cell(excelRow, 5).Value = joinedPh;
            sheet.Cell(excelRow, 5).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Cell(excelRow, 6).Value = row.TotalRides;
            sheet.Cell(excelRow, 7).Value = row.TotalCancel;
            sheet.Cell(excelRow, 8).Value = row.RiderIncome;
            sheet.Cell(excelRow, 8).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(excelRow, 9).Value = row.BookingAmount;
            sheet.Cell(excelRow, 9).Style.NumberFormat.Format = "#,##0.00";
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
