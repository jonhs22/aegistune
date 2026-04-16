namespace AegisTune.Core;

public enum WindowsHealthFocusActionKind
{
    None,
    OpenEventViewer,
    OpenWindowsUpdate,
    OpenRepair,
    OpenServices,
    OpenTaskScheduler,
    OpenTarget
}

public sealed record WindowsHealthFocusGuidance(
    string Title,
    string Summary,
    string NextStep,
    WindowsHealthFocusActionKind PrimaryActionKind,
    string PrimaryActionLabel,
    WindowsHealthFocusActionKind SecondaryActionKind,
    string SecondaryActionLabel,
    string? TargetPath = null)
{
    public bool HasPrimaryAction => PrimaryActionKind != WindowsHealthFocusActionKind.None && !string.IsNullOrWhiteSpace(PrimaryActionLabel);

    public bool HasSecondaryAction => SecondaryActionKind != WindowsHealthFocusActionKind.None && !string.IsNullOrWhiteSpace(SecondaryActionLabel);

    public static WindowsHealthFocusGuidance Empty { get; } = new(
        "Nothing is selected yet",
        "Pick one crash, update issue, service, or scheduled task to see the next recommended button.",
        "If issues exist, start by pressing one of the review buttons in the lists below.",
        WindowsHealthFocusActionKind.None,
        string.Empty,
        WindowsHealthFocusActionKind.None,
        string.Empty);
}

public static class WindowsHealthFocusAdvisor
{
    public static WindowsHealthFocusGuidance CreateCrash(WindowsHealthEventRecord record) =>
        new(
            record.Title,
            record.Detail,
            "Start in Event Viewer to confirm the exact crash context. If the same component keeps failing, continue in Repair & Recovery.",
            WindowsHealthFocusActionKind.OpenEventViewer,
            "Open Event Viewer",
            WindowsHealthFocusActionKind.OpenRepair,
            "Open Repair & Recovery");

    public static WindowsHealthFocusGuidance CreateWindowsUpdate(WindowsHealthEventRecord record) =>
        new(
            record.Title,
            record.Detail,
            "Start in Windows Update to retry or inspect the latest failure. If the issue persists, continue in Repair & Recovery.",
            WindowsHealthFocusActionKind.OpenWindowsUpdate,
            "Open Windows Update",
            WindowsHealthFocusActionKind.OpenRepair,
            "Open Repair & Recovery");

    public static WindowsHealthFocusGuidance CreateService(ServiceReviewRecord record) =>
        new(
            record.DisplayTitle,
            record.Issue,
            record.ExecutablePathExists
                ? "Start in Services to inspect the live startup state. If you need to validate the binary, open the recorded target next."
                : "Start in Services to inspect startup type and dependencies. Then continue in Repair & Recovery because the registered service target is missing.",
            WindowsHealthFocusActionKind.OpenServices,
            "Open Services",
            record.ExecutablePathExists ? WindowsHealthFocusActionKind.OpenTarget : WindowsHealthFocusActionKind.OpenRepair,
            record.ExecutablePathExists ? "Open service target" : "Open Repair & Recovery",
            record.ExecutablePath);

    public static WindowsHealthFocusGuidance CreateScheduledTask(ScheduledTaskReviewRecord record) =>
        new(
            record.TaskName,
            record.Issue,
            record.ExecutePathExists
                ? "Start in Task Scheduler to inspect the trigger and action. If you need to validate the executable, open the recorded target next."
                : "Start in Task Scheduler to inspect the task definition. Then continue in Repair & Recovery because the recorded task target is missing.",
            WindowsHealthFocusActionKind.OpenTaskScheduler,
            "Open Task Scheduler",
            record.ExecutePathExists ? WindowsHealthFocusActionKind.OpenTarget : WindowsHealthFocusActionKind.OpenRepair,
            record.ExecutePathExists ? "Open task target" : "Open Repair & Recovery",
            record.ExecutePath);
}
