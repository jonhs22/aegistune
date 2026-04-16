namespace AegisTune.Core;

public sealed record ScheduledTaskReviewRecord(
    string TaskName,
    string TaskPath,
    string State,
    string? ExecutePath,
    bool ExecutePathExists,
    string Issue)
{
    public string DisplayPath => $"{TaskPath}{TaskName}";

    public string StateLabel => string.IsNullOrWhiteSpace(State) ? "State unknown" : State;

    public string ExecutePathLabel => string.IsNullOrWhiteSpace(ExecutePath)
        ? "Task action could not be resolved."
        : ExecutePath!;
}
