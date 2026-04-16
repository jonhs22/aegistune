using System.ComponentModel;
using System.Management;
using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsSystemRestoreService : ISystemRestoreService
{
    public Task<SystemRestoreCheckpointResult> CreateCheckpointAsync(
        string description,
        SystemRestoreIntent intent,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => CreateCheckpoint(description, intent), cancellationToken);

    private static SystemRestoreCheckpointResult CreateCheckpoint(
        string description,
        SystemRestoreIntent intent)
    {
        DateTimeOffset processedAt = DateTimeOffset.Now;
        string normalizedDescription = BuildDescription(description, intent);

        try
        {
            using ManagementClass restoreClass = new(@"root\default", "SystemRestore", null);
            using ManagementBaseObject inParameters = restoreClass.GetMethodParameters("CreateRestorePoint");
            inParameters["Description"] = normalizedDescription;
            inParameters["RestorePointType"] = intent switch
            {
                SystemRestoreIntent.DeviceDriverInstall => 10u,
                SystemRestoreIntent.ApplicationInstall => 0u,
                _ => 12u
            };
            inParameters["EventType"] = 100u;

            using ManagementBaseObject? outParameters = restoreClass.InvokeMethod("CreateRestorePoint", inParameters, null);
            int returnCode = Convert.ToInt32(outParameters?["ReturnValue"] ?? -1);
            if (returnCode == 0)
            {
                return new SystemRestoreCheckpointResult(
                    true,
                    normalizedDescription,
                    processedAt,
                    $"Created a Windows restore point for {DescribeIntent(intent)}.",
                    "AegisTune can continue with the risky change and keep System Restore available as a rollback path.",
                    returnCode);
            }

            string detail = BuildReturnCodeDetail(returnCode);
            return new SystemRestoreCheckpointResult(
                false,
                normalizedDescription,
                processedAt,
                $"Windows did not create a restore point for {DescribeIntent(intent)}. {detail}",
                "Enable System Protection for the system drive and run AegisTune from an elevated session before retrying this change.",
                returnCode);
        }
        catch (ManagementException ex)
        {
            return new SystemRestoreCheckpointResult(
                false,
                normalizedDescription,
                processedAt,
                $"System Restore could not be queried from Windows Management Instrumentation: {ex.Message}",
                "Check whether System Protection is enabled on the system drive and whether WMI access is available in this Windows session.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new SystemRestoreCheckpointResult(
                false,
                normalizedDescription,
                processedAt,
                $"Windows rejected the restore-point request: {ex.Message}",
                "Run AegisTune as administrator before trying risky driver or registry operations.");
        }
    }

    private static string BuildDescription(string description, SystemRestoreIntent intent)
    {
        string prefix = intent switch
        {
            SystemRestoreIntent.DeviceDriverInstall => "AegisTune driver",
            SystemRestoreIntent.ApplicationInstall => "AegisTune app",
            _ => "AegisTune fix"
        };

        string suffix = string.IsNullOrWhiteSpace(description)
            ? "checkpoint"
            : description.Trim();
        string combined = $"{prefix}: {suffix}";
        return combined.Length <= 64
            ? combined
            : combined[..64];
    }

    private static string DescribeIntent(SystemRestoreIntent intent) => intent switch
    {
        SystemRestoreIntent.DeviceDriverInstall => "the driver install",
        SystemRestoreIntent.ApplicationInstall => "the app change",
        _ => "the settings or registry change"
    };

    private static string BuildReturnCodeDetail(int returnCode)
    {
        if (returnCode <= 0)
        {
            return "Windows returned an unknown restore-point status.";
        }

        string win32Message = new Win32Exception(returnCode).Message;
        return string.Equals(win32Message, $"Unknown error (0x{returnCode:X})", StringComparison.OrdinalIgnoreCase)
            ? $"Return code {returnCode}."
            : $"Return code {returnCode}: {win32Message}";
    }
}
