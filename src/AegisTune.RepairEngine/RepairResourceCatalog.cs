namespace AegisTune.RepairEngine;

public static class RepairResourceCatalog
{
    public static RepairResourceLink VisualCppRedistributable => All[0];

    public static RepairResourceLink WebView2Runtime => All[1];

    public static RepairResourceLink DirectXRuntime => All[2];

    public static IReadOnlyList<RepairResourceLink> All { get; } =
        new[]
        {
            new RepairResourceLink(
                "Latest supported Visual C++ Redistributable",
                "Use this for MSVCP, MSVCR, VCRUNTIME, CONCRT, and SideBySide cases tied to Microsoft Visual C++ runtimes.",
                "Open Microsoft VC++ runtime page",
                new Uri("https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170")),
            new RepairResourceLink(
                "Microsoft Edge WebView2 Runtime",
                "Use this when the app or error mentions WebView2Loader.dll or msedgewebview2.exe.",
                "Open WebView2 runtime page",
                new Uri("https://developer.microsoft.com/microsoft-edge/webview2/#download-section")),
            new RepairResourceLink(
                "DirectX End-User Runtimes (June 2010)",
                "Use this only for legacy DirectX components such as d3dx9_43.dll, d3dcompiler_43.dll, xinput1_3.dll, or xaudio2_7.dll.",
                "Open DirectX runtime page",
                new Uri("https://www.microsoft.com/en-us/download/details.aspx?id=8109"))
        };
}
