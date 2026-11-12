$ErrorActionPreference = "Stop"

$srcDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outExe = Join-Path (Split-Path -Parent $srcDir) "BrightnessControl.exe"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (!(Test-Path $csc)) {
    throw "C# compiler not found at $csc"
}

$argsList = @(
    "/nologo"
    "/target:winexe"
    "/optimize+"
    "/platform:anycpu"
    "/out:$outExe"
    "/win32icon:$srcDir\Resources\App.ico"
    "/reference:System.dll"
    "/reference:System.Drawing.dll"
    "/reference:System.Windows.Forms.dll"
    "/reference:System.Management.dll"
    "/resource:$srcDir\Resources\Icon.png,BrightnessControl.Resources.Icon.png"
    "/resource:$srcDir\Resources\IconDark.png,BrightnessControl.Resources.IconDark.png"
    "/resource:$srcDir\Resources\IconLight.png,BrightnessControl.Resources.IconLight.png"
    "$srcDir\AssemblyInfo.cs"
    "$srcDir\Interop.cs"
    "$srcDir\SettingsStore.cs"
    "$srcDir\DisplayController.cs"
    "$srcDir\InputListener.cs"
    "$srcDir\IconRenderer.cs"
    "$srcDir\BalloonTooltip.cs"
    "$srcDir\SmoothSlider.cs"
    "$srcDir\BrightnessFlyout.cs"
    "$srcDir\TrayContextMenu.cs"
    "$srcDir\TrayApplication.cs"
    "$srcDir\Program.cs"
)

& $csc @argsList
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Built standalone executable: $outExe"
Get-Item $outExe | Select-Object Name, Length, LastWriteTime

