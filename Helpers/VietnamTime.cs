namespace DuongVanDung.WebApp.Helpers;

/// <summary>
/// All date/time in the app must use Vietnam time (UTC+7).
/// Server may be in US/EU/UTC — never use DateTime.Now directly.
/// </summary>
public static class VietnamTime
{
    private static readonly TimeZoneInfo VnZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    /// <summary>Current Vietnam date+time</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnZone);

    /// <summary>Current Vietnam date (midnight)</summary>
    public static DateTime Today => Now.Date;

    /// <summary>First day of current month in Vietnam</summary>
    public static DateTime MonthStart => new DateTime(Now.Year, Now.Month, 1);
}
