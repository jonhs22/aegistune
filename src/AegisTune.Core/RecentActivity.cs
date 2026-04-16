namespace AegisTune.Core;

public sealed record RecentActivity(DateTimeOffset OccurredAt, string Title, string Detail)
{
    public string OccurredAtLabel => OccurredAt.ToLocalTime().ToString("g");
}
