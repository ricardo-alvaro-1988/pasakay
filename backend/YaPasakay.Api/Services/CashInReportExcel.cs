using ClosedXML.Excel;
using YaPasakay.Application.Admin;

namespace YaPasakay.Api.Services;

public static class CashInReportExcel
{
    public static byte[] Build(IReadOnlyList<WalletHistoryItem> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Cash in");

        sheet.Cell(1, 1).Value = "Date";
        sheet.Cell(1, 2).Value = "Rider name";
        sheet.Cell(1, 3).Value = "Mobile";
        sheet.Cell(1, 4).Value = "Plate";
        sheet.Cell(1, 5).Value = "Payment method";
        sheet.Cell(1, 6).Value = "Amount";
        sheet.Cell(1, 7).Value = "Status";
        sheet.Cell(1, 8).Value = "Balance after";
        sheet.Cell(1, 9).Value = "Note";
        sheet.Cell(1, 10).Value = "Rejection reason";
        sheet.Cell(1, 11).Value = "Resolved at";
        sheet.Range(1, 1, 1, 11).Style.Font.Bold = true;

        var ph = TimeSpan.FromHours(8);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            var createdPh = row.CreatedAtUtc.Add(ph);
            sheet.Cell(excelRow, 1).Value = createdPh;
            sheet.Cell(excelRow, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Cell(excelRow, 2).Value = row.RiderName;
            sheet.Cell(excelRow, 3).Value = row.RiderPhone;
            sheet.Cell(excelRow, 4).Value = row.PlateNumber;
            sheet.Cell(excelRow, 5).Value = row.PaymentMethod?.ToString() ?? "";
            sheet.Cell(excelRow, 6).Value = row.Amount;
            sheet.Cell(excelRow, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(excelRow, 7).Value = row.Status.ToString();
            if (row.BalanceAfter is decimal balance)
            {
                sheet.Cell(excelRow, 8).Value = balance;
                sheet.Cell(excelRow, 8).Style.NumberFormat.Format = "#,##0.00";
            }

            sheet.Cell(excelRow, 9).Value = row.Note ?? "";
            sheet.Cell(excelRow, 10).Value = row.RejectionReason ?? "";
            if (row.ResolvedAtUtc is DateTime resolved)
            {
                sheet.Cell(excelRow, 11).Value = resolved.Add(ph);
                sheet.Cell(excelRow, 11).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            }
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
