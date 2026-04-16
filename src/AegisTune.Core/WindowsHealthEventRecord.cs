namespace AegisTune.Core;

public sealed record WindowsHealthEventRecord(
    string Title,
    string Source,
    int EventId,
    string Severity,
    string Detail,
    DateTimeOffset ObservedAt)
{
    public string EventLabel => $"{Source} • Event {EventId}";

    public string OccurredAtLabel => ObservedAt.ToLocalTime().ToString("g");
}
