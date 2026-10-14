$ErrorActionPreference = 'Stop'
$runtime = (Get-Command python -ErrorAction Stop).Source
$baseDirectory = Split-Path -Parent $runtime
$probe = 'A:\Agent\Codex\Translation\audit\process-probe\engine\python'
$libraryName = & $runtime -I -B -c 'import sys; print("python%d%d.dll" % sys.version_info[:2])'
if ($LASTEXITCODE -ne 0) { throw 'The existing Python runtime is unavailable.' }
$library = Join-Path $baseDirectory $libraryName
if (-not (Test-Path -LiteralPath $library)) { throw 'The existing Python DLL is unavailable.' }
# Reuse the installed standard library; no pip, downloads or third-party packages.
& $runtime -I -B -m venv --without-pip $probe
if ($LASTEXITCODE -ne 0) { throw 'Audit sandbox creation failed.' }
Copy-Item -LiteralPath $runtime -Destination (Join-Path $probe 'python.exe') -Force
Copy-Item -LiteralPath $library -Destination $probe -Force
