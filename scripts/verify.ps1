param([ValidateSet('local', 'services', 'gui', 'gui-messages', 'audit', 'layout', 'small-text', 'ocr-quality', 'ocr-lifecycle', 'ocr-development', 'ocr-acceptance', 'refinement-interactions')][string]$Mode = 'services')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($Mode -eq 'audit') { & (Join-Path $PSScriptRoot 'prepare-audit.ps1') }
$output = 'A:\Agent\Codex\Translation\verification'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sourceFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs'
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -Filter '*.cs'
) | ForEach-Object { $_.FullName }
$arguments = @('/nologo', '/warn:4', '/warnaserror+', '/optimize+', '/target:exe', '/platform:x64', '/codepage:65001', '/utf8output', '/main:LightTranslate.VerificationEntry')
$arguments += '/out:' + (Join-Path $output 'LightTranslate.Verification.exe')
$arguments += '/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')
$arguments += '/win32icon:' + (Join-Path $projectRoot 'assets\app.ico')
$arguments += '/resource:' + (Join-Path $projectRoot 'assets\app.ico') + ',LightTranslate.App.ico'
$arguments += @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll')
& $compiler @arguments @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Verification compilation failed.' }
if ($Mode -eq 'ocr-lifecycle') {
    $probe = 'A:\Agent\Codex\Translation\ocr-evaluation\process-probe'
    New-Item -ItemType Directory -Path $probe -Force | Out-Null
    & $compiler /nologo /warn:4 /warnaserror+ /target:exe /platform:x64 /codepage:65001 /main:LightTranslate.OcrProbeEntry "/out:$probe\OcrProbe.exe" /r:System.Web.Extensions.dll (Join-Path $projectRoot 'tests\OcrProbeEntry.cs')
    if ($LASTEXITCODE -ne 0) { throw 'OCR process probe compilation failed.' }
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\LightTranslate.exe.config') -Destination (Join-Path $output 'LightTranslate.Verification.exe.config') -Force
& (Join-Path $output 'LightTranslate.Verification.exe') $Mode
if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }
