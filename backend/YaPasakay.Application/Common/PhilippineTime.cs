namespace YaPasakay.Application.Common;

public static class PhilippineTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(8);

    public static DateTime ToUtc(DateTime phLocal) =>
        DateTime.SpecifyKind(DateTime.SpecifyKind(phLocal, DateTimeKind.Unspecified).Add(-Offset), DateTimeKind.Utc);

    public static DateTime ToUtc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        ToUtc(new DateTime(year, month, day, hour, minute, second));

    public static DateTime ToPh(DateTime utc) =>
        DateTime.SpecifyKind(utc.ToUniversalTime().Add(Offset), DateTimeKind.Unspecified);
}
