param([string]$OutputDirectory = 'A:\Agent\Codex\Translation\ocr-evaluation\fixtures')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath (Join-Path $output 'manifest.json')) {
    throw 'The frozen OCR fixture manifest already exists; use a new directory for a new cohort.'
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$subjects = @(
    @{ id='zh-update'; language='zh'; text='文件尚未保存，请勿关闭窗口。'; critical=@('未','请勿') },
    @{ id='zh-tax'; language='zh'; text='订阅费用为19.99美元，不含税费。'; critical=@('19.99','不含') },
    @{ id='zh-connection'; language='zh'; text='网络连接已断开，数据不会上传。'; critical=@('不会') },
    @{ id='zh-repair'; language='zh'; text='维修报价925元，不能超过预算。'; critical=@('925','不能') },
    @{ id='zh-battery'; language='zh'; text='电量剩余86%，请不要切断电源。'; critical=@('86','不要') },
    @{ id='zh-install'; language='zh'; text='安装还需3分钟，必须保持电脑开启。'; critical=@('3','必须') },
    @{ id='en-save'; language='en'; text='Do not close this window until the file is saved.'; critical=@('not') },
    @{ id='en-update'; language='en'; text='Keep the computer on during the update.'; critical=@('on') },
    @{ id='en-price'; language='en'; text='The monthly price is $19.99, excluding tax.'; critical=@('19.99','excluding') },
    @{ id='en-code'; language='en'; text='Error 502: retry after 30 seconds.'; critical=@('502','30') },
    @{ id='mixed-version'; language='mixed'; text='版本 v2.6 已安装，请勿删除配置文件。'; critical=@('v2.6','请勿') },
    @{ id='mixed-date'; language='mixed'; text='Update 2026-10-05：备份完成，不要断电。'; critical=@('2026-10-05','不要') }
)

function Write-Fixture($subject, $pixels, $dark, $path) {
    $image = New-Object Drawing.Bitmap(760, 72)
    $drawing = [Drawing.Graphics]::FromImage($image)
    $fontName = if ($subject.language -eq 'en') { 'Segoe UI' } else { 'Microsoft YaHei UI' }
    $font = New-Object Drawing.Font($fontName, $pixels, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
    $background = if ($dark) { [Drawing.Color]::FromArgb(28, 30, 34) } else { [Drawing.Color]::White }
    $brush = if ($dark) { [Drawing.Brushes]::White } else { [Drawing.Brushes]::Black }
    try {
        $drawing.Clear($background)
        $drawing.TextRenderingHint = [Drawing.Text.TextRenderingHint]::ClearTypeGridFit
        $drawing.DrawString($subject.text, $font, $brush, 16, 16)
        $image.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $font.Dispose(); $drawing.Dispose(); $image.Dispose() }
}

$fixtures = @()
foreach ($subject in $subjects) {
    foreach ($pixels in @(12, 14, 16)) {
        foreach ($dark in @($false, $true)) {
            $theme = if ($dark) { 'dark' } else { 'light' }
            $name = $subject.id + '-' + $pixels + '-' + $theme + '.png'
            $path = Join-Path $output $name
            Write-Fixture $subject $pixels $dark $path
            $fixtures += [ordered]@{ id=$name; language=$subject.language; pixels=$pixels; theme=$theme;
                expected=$subject.text; critical=$subject.critical; path=$name;
                sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
        }
    }
}
$manifest = [ordered]@{ schema=1; createdUtc=[DateTime]::UtcNow.ToString('o'); license='MIT';
    scope='Authored single-line ClearType fixtures frozen before candidate inference. Independent of previous eight diagnosis fixtures; not a general accuracy corpus.';
    fixtures=$fixtures }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
Write-Output ('PASS froze ' + $fixtures.Count + ' new OCR fixtures')
