param(
    [string]$OutputDirectory = 'A:\Agent\Codex\Translation\app'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 compiler is unavailable.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
$arguments = @('/nologo', '/warn:4', '/warnaserror+', '/optimize+', '/target:winexe', '/platform:x64', '/codepage:65001', '/utf8output')
$arguments += '/out:' + (Join-Path $output 'LightTranslate.exe')
$arguments += '/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')
$arguments += '/win32icon:' + (Join-Path $projectRoot 'assets\app.ico')
$arguments += '/resource:' + (Join-Path $projectRoot 'assets\app.ico') + ',LightTranslate.App.ico'
$arguments += @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll')
& $compiler @arguments @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\LightTranslate.exe.config') -Destination $output -Force
$engine = Join-Path $output 'engine'
New-Item -ItemType Directory -Path $engine -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts\translation-worker.py') -Destination $engine -Force
Get-Item -LiteralPath (Join-Path $output 'LightTranslate.exe') | Select-Object FullName, Length
