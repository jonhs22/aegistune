namespace AegisTune.Core;

public sealed record ServiceReviewRecord(
    string Name,
    string DisplayName,
    string StartMode,
    string State,
    string? ExecutablePath,
    bool ExecutablePathExists,
    string Issue)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;

    public string StartModeLabel => string.IsNullOrWhiteSpace(StartMode) ? "Start mode unknown" : StartMode;

    public string StateLabel => string.IsNullOrWhiteSpace(State) ? "State unknown" : State;

    public string ExecutablePathLabel => string.IsNullOrWhiteSpace(ExecutablePath)
        ? "Executable path could not be resolved."
        : ExecutablePath!;
}
