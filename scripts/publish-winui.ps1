param(
    [switch]$Installer,
    [string]$Version
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src/FloatTodo.WinUI/FloatTodo.WinUI.csproj'

$extraArgs = @()
if (![string]::IsNullOrWhiteSpace($Version)) {
    $extraArgs += "-p:Version=$Version"
}

foreach ($profile in @('PublishFolder','PublishPortable')) {
    & dotnet publish $project -p:Platform=x64 "-p:PublishProfile=$profile" @extraArgs
    if ($LASTEXITCODE) { throw "Publish failed: $profile" }
}

if ($Installer) {
    $compiler = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (!$compiler) { throw 'Install Inno Setup 6 before building the installer.' }
    
    $isccArgs = @()
    if (![string]::IsNullOrWhiteSpace($Version)) {
        $isccArgs += "/DAppVersion=$Version"
    }
    $isccArgs += (Join-Path $root 'installer/FloatTodo.iss')

    & $compiler @isccArgs
    if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
}

