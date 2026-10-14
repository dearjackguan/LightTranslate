$ErrorActionPreference = 'Stop'
$worker = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts\ocr-worker.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($worker, [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw 'OCR worker has PowerShell syntax errors.' }
$function = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-SmallTextScale'
}, $true)
. ([scriptblock]::Create($function.Extent.Text))
$small = [pscustomobject]@{ Lines = @([pscustomobject]@{ Words = @(
    [pscustomobject]@{ Text = 'Small'; BoundingRect = [pscustomobject]@{ Height = 10 } }
) }) }
$normal = [pscustomobject]@{ Lines = @([pscustomobject]@{ Words = @(
    [pscustomobject]@{ Text = 'Normal'; BoundingRect = [pscustomobject]@{ Height = 24 } },
    [pscustomobject]@{ Text = '.'; BoundingRect = [pscustomobject]@{ Height = 2 } }
) }) }
$mixed = [pscustomobject]@{ Lines = @([pscustomobject]@{ Words = @(
    [pscustomobject]@{ Text = 'Small'; BoundingRect = [pscustomobject]@{ Height = 10 } },
    [pscustomobject]@{ Text = 'Heading'; BoundingRect = [pscustomobject]@{ Height = 22 } }
) }) }
$cases = @(
    @{ name = 'normal-text-preserved'; result = $normal; width = 500; height = 200; maximum = 2600; expected = 1.0 },
    @{ name = 'small-text-enlarged'; result = $small; width = 500; height = 200; maximum = 2600; expected = 2.0 },
    @{ name = 'empty-recognition-retry'; result = $null; width = 500; height = 200; maximum = 2600; expected = 2.0 },
    @{ name = 'even-count-median'; result = $mixed; width = 500; height = 200; maximum = 2600; expected = 2.0 },
    @{ name = 'dimension-limit'; result = $small; width = 2000; height = 500; maximum = 2600; expected = 1.3 },
    @{ name = 'pixel-budget'; result = $small; width = 2000; height = 2000; maximum = 10000; expected = [Math]::Sqrt(2) },
    @{ name = 'large-native-image-not-enlarged'; result = $small; width = 3000; height = 3000; maximum = 10000; expected = 1.0 }
)
foreach ($case in $cases) {
    $scale = Get-SmallTextScale $case.result $case.width $case.height $case.maximum
    if ([Math]::Abs($scale - $case.expected) -gt 0.000001) { throw ('Unexpected OCR scale: ' + $case.name) }
    $width = [Math]::Floor($case.width * $scale)
    $height = [Math]::Floor($case.height * $scale)
    if ($width -gt $case.maximum -or $height -gt $case.maximum) { throw 'Enlargement exceeded the OCR dimension limit.' }
    if ($scale -gt 1 -and $width * $height -gt 8000000) { throw 'Enlargement exceeded the pixel budget.' }
}
Write-Output ('PASS OCR scale ' + $cases.Count + ' boundary checks')
