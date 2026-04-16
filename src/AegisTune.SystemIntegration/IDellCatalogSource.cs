using System.Diagnostics;
using System.Text;
using System.Runtime.Versioning;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public interface IDellCatalogSource
{
    Task<string?> GetCatalogXmlAsync(CancellationToken cancellationToken = default);
}

[SupportedOSPlatform("windows")]
public sealed class DellCatalogCabSource : IDellCatalogSource
{
    private readonly HttpClient _httpClient;

    public DellCatalogCabSource(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> GetCatalogXmlAsync(CancellationToken cancellationToken = default)
    {
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "AegisTune",
            "DellCatalog",
            Guid.NewGuid().ToString("N"));
        string outerCabPath = Path.Combine(tempRoot, "CatalogPC.cab");
        string expandedDirectory = Path.Combine(tempRoot, "expanded");

        Directory.CreateDirectory(tempRoot);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, CatalogUrl);
            request.Headers.UserAgent.ParseAdd(DefaultUserAgent);

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (FileStream fileStream = File.Create(outerCabPath))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }

            Directory.CreateDirectory(expandedDirectory);
            await ExpandCabAsync(outerCabPath, expandedDirectory, cancellationToken);

            string? xmlPath = Directory.EnumerateFiles(expandedDirectory, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(IsXmlPayloadFile);

            return xmlPath is null
                ? null
                : await File.ReadAllTextAsync(xmlPath, cancellationToken);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Temp cleanup should never block the lookup result path.
            }
        }
    }

    private static async Task ExpandCabAsync(
        string cabPath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "expand.exe",
            Arguments = $"-F:* \"{cabPath}\" \"{destinationDirectory}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("expand.exe could not be started for Dell catalog extraction.");

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Dell catalog extraction failed with exit code {process.ExitCode}. {standardError} {standardOutput}".Trim());
        }
    }

    private static bool IsXmlPayloadFile(string path)
    {
        try
        {
            using StreamReader reader = new(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            char[] buffer = new char[64];
            int read = reader.Read(buffer, 0, buffer.Length);
            string prefix = new string(buffer, 0, read).TrimStart();
            return prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("<Manifest", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private const string CatalogUrl = "https://downloads.dell.com/catalog/CatalogPC.cab";
    private const string DefaultUserAgent = "AegisTune/1.0 (+https://ichiphost.gr)";
}
