namespace VanEscolar.Api.Infrastructure;
public static class LocalDates {
  // For transport operators in Brazil. Override at deployment if the operation is elsewhere.
  public static DateOnly Today() {
    string zoneName = Environment.GetEnvironmentVariable("APP_TIME_ZONE") ?? "America/Sao_Paulo";
    var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneName);
    return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
  }
}
