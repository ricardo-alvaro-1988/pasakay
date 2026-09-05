namespace YaPasakay.Api.Models;

public class OperatorEwalletCashInForm
{
    public string? GCashNumber { get; set; }
    public IFormFile? GCashQr { get; set; }
    public bool ClearGCashQr { get; set; }
    public string? MayaNumber { get; set; }
    public IFormFile? MayaQr { get; set; }
    public bool ClearMayaQr { get; set; }
}

public class OperatorBankCashInForm
{
    public string BankName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public IFormFile? Qr { get; set; }
    public bool ClearQr { get; set; }
}
