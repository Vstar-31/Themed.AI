[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'The WinUI XAML compiler and desktop tests require Windows. Run this script on Windows 10 19041+ or Windows 11.'
}

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet exited with code $LASTEXITCODE" }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    Get-Command dotnet -ErrorAction Stop | Out-Null
    $results = Join-Path $repoRoot "artifacts/test-results/$Configuration"
    New-Item -ItemType Directory -Force -Path $results | Out-Null
    # global.json selects the SDK; setup-dotnet/Visual Studio must also provide .NET 8.
    Invoke-Dotnet --info
    Invoke-Dotnet restore ThemeManager.sln --runtime win-x64
    Invoke-Dotnet build ThemeManager.sln --configuration $Configuration '-p:Platform=x64' --no-restore "-bl:$results/build.binlog"
    Invoke-Dotnet test ThemeManager.Tests/ThemeManager.Tests.csproj --configuration $Configuration '-p:Platform=x64' --no-build --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory $results

    if ($Publish) {
        $output = Join-Path $repoRoot 'artifacts/windows-x64'
        # Include both .NET and Windows App SDK. WebView2 Runtime remains a prerequisite.
        Invoke-Dotnet publish ThemeManager.WinUI/ThemeManager.WinUI.csproj --configuration $Configuration --runtime win-x64 --self-contained true '-p:Platform=x64' '-p:WindowsAppSDKSelfContained=true' --output $output
        if (-not (Test-Path (Join-Path $output 'ThemeManager.WinUI.exe'))) {
            throw 'Publish completed without ThemeManager.WinUI.exe.'
        }
        foreach ($resource in @('MainWindow.xbf', 'ThemeManager.WinUI.pri', 'Views/StudioPage.xbf', 'Views/SkinsPage.xbf', 'Views/SkinEditorPage.xbf', 'Resources/ThemeResources.xbf')) {
            if (-not (Test-Path -LiteralPath (Join-Path $output $resource))) {
                throw "Publish is missing required UI resource: $resource"
            }
        }
        git rev-parse HEAD | Set-Content (Join-Path $output 'source-commit.txt')
        Write-Host "Launch $output/ThemeManager.WinUI.exe and follow docs/WINDOWS_TESTING.md."
    }
}
finally { Pop-Location }
