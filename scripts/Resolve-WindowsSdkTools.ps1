param(
    [switch]$AddToPath,
    [string[]]$RequiredTools = @(),
    [switch]$AsJson
)

function Resolve-WindowsSdkTools {
    param(
        [switch]$AddToPath,
        [string[]]$RequiredTools = @()
    )

    $kitsRoot = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -ErrorAction Stop).KitsRoot10
    if ([string]::IsNullOrWhiteSpace($kitsRoot) -or -not (Test-Path $kitsRoot)) {
        throw "Windows Kits root could not be resolved from HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots."
    }

    $sdkVersionDirectory = Get-ChildItem (Join-Path $kitsRoot 'bin') -Directory -ErrorAction Stop |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        Select-Object -First 1

    if (-not $sdkVersionDirectory) {
        throw "No versioned Windows SDK bin directory was found under $kitsRoot."
    }

    $sdkBinPath = Join-Path $sdkVersionDirectory.FullName 'x64'
    if (-not (Test-Path $sdkBinPath)) {
        throw "Windows SDK x64 tools directory was not found at $sdkBinPath."
    }

    $msbuild = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio' -Recurse -Filter MSBuild.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\MSBuild\\Current\\Bin(\\amd64)?\\MSBuild\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    $vsDevCmd = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio' -Recurse -Filter VsDevCmd.bat -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    $resolved = [pscustomobject]@{
        KitsRoot   = $kitsRoot
        SdkVersion = $sdkVersionDirectory.Name
        SdkBinPath = $sdkBinPath
        SignTool   = (Join-Path $sdkBinPath 'signtool.exe')
        MakeAppx   = (Join-Path $sdkBinPath 'makeappx.exe')
        MakePri    = (Join-Path $sdkBinPath 'makepri.exe')
        MsBuild    = $msbuild
        VsDevCmd   = $vsDevCmd
    }

    $missing = New-Object System.Collections.Generic.List[string]
    foreach ($toolName in $RequiredTools) {
        switch ($toolName.ToLowerInvariant()) {
            'signtool' { if (-not (Test-Path $resolved.SignTool)) { $missing.Add('signtool') } }
            'makeappx' { if (-not (Test-Path $resolved.MakeAppx)) { $missing.Add('makeappx') } }
            'makepri' { if (-not (Test-Path $resolved.MakePri)) { $missing.Add('makepri') } }
            'msbuild' { if ([string]::IsNullOrWhiteSpace($resolved.MsBuild) -or -not (Test-Path $resolved.MsBuild)) { $missing.Add('msbuild') } }
            'vsdevcmd' { if ([string]::IsNullOrWhiteSpace($resolved.VsDevCmd) -or -not (Test-Path $resolved.VsDevCmd)) { $missing.Add('vsdevcmd') } }
            default { }
        }
    }

    if ($missing.Count -gt 0) {
        throw "Missing required Windows SDK tool(s): $($missing -join ', ')."
    }

    if ($AddToPath) {
        $pathSegments = @($resolved.SdkBinPath)
        if (-not [string]::IsNullOrWhiteSpace($resolved.MsBuild)) {
            $pathSegments += (Split-Path -Parent $resolved.MsBuild)
        }

        foreach ($segment in $pathSegments | Select-Object -Unique) {
            if (-not [string]::IsNullOrWhiteSpace($segment) -and -not ($env:PATH -split ';' | Where-Object { $_ -eq $segment })) {
                $env:PATH = "$segment;$env:PATH"
            }
        }
    }

    return $resolved
}

if ($MyInvocation.InvocationName -ne '.') {
    $resolved = Resolve-WindowsSdkTools -AddToPath:$AddToPath -RequiredTools $RequiredTools
    if ($AsJson) {
        $resolved | ConvertTo-Json -Depth 3
    }
    else {
        $resolved | Format-List
    }
}
