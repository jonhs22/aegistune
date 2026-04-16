using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class WindowsHealthFocusAdvisorTests
{
    [Fact]
    public void CreateCrash_PointsToEventViewerAndRepair()
    {
        WindowsHealthEventRecord record = new(
            "Contoso App crashed",
            "Application Error",
            1000,
            "Error",
            "Faulting module: contoso.dll",
            DateTimeOffset.Parse("2026-04-16T10:15:00+00:00"));

        WindowsHealthFocusGuidance guidance = WindowsHealthFocusAdvisor.CreateCrash(record);

        Assert.Equal(WindowsHealthFocusActionKind.OpenEventViewer, guidance.PrimaryActionKind);
        Assert.Equal(WindowsHealthFocusActionKind.OpenRepair, guidance.SecondaryActionKind);
    }

    [Fact]
    public void CreateService_UsesTargetWhenExecutableExists()
    {
        ServiceReviewRecord record = new(
            "ContosoSvc",
            "Contoso Background Service",
            "Automatic",
            "Stopped",
            @"C:\Program Files\Contoso\service.exe",
            true,
            "The service is automatic but not running.");

        WindowsHealthFocusGuidance guidance = WindowsHealthFocusAdvisor.CreateService(record);

        Assert.Equal(WindowsHealthFocusActionKind.OpenServices, guidance.PrimaryActionKind);
        Assert.Equal(WindowsHealthFocusActionKind.OpenTarget, guidance.SecondaryActionKind);
        Assert.Equal(@"C:\Program Files\Contoso\service.exe", guidance.TargetPath);
    }

    [Fact]
    public void CreateScheduledTask_FallsBackToRepairWhenTargetIsMissing()
    {
        ScheduledTaskReviewRecord record = new(
            "ContosoCleanup",
            @"\Contoso\",
            "Ready",
            @"C:\Missing\cleanup.exe",
            false,
            "The recorded task target is missing.");

        WindowsHealthFocusGuidance guidance = WindowsHealthFocusAdvisor.CreateScheduledTask(record);

        Assert.Equal(WindowsHealthFocusActionKind.OpenTaskScheduler, guidance.PrimaryActionKind);
        Assert.Equal(WindowsHealthFocusActionKind.OpenRepair, guidance.SecondaryActionKind);
    }
}
