param([string]$OutputDirectory = 'A:\Agent\Codex\Translation\refinement\ocr-fixtures')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (Test-Path -LiteralPath (Join-Path $OutputDirectory 'manifest.json')) { throw 'Frozen fixtures already exist.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$subjects = @(
    @{ id='dev-negation'; split='development'; text='提交尚未成功，请不要删除草稿。'; font='Microsoft YaHei UI'; critical=@('未','不要') },
    @{ id='dev-voltage'; split='development'; text='Voltage 3.3 V; do not exceed 85 C.'; font='Segoe UI'; critical=@('3.3','not','85') },
    @{ id='dev-price'; split='development'; text='维修费用480元，不包括税费。'; font='Microsoft YaHei UI'; critical=@('480','不包括') },
    @{ id='dev-two-lines'; split='development'; text="The upload has not finished.`nKeep this window open."; font='Segoe UI'; critical=@('not','open') },
    @{ id='dev-version'; split='development'; text='版本 v3.8 已保存，请勿覆盖。'; font='Microsoft YaHei UI'; critical=@('v3.8','请勿') },
    @{ id='dev-columns'; split='development'; text="Do not delete.`nKeep backup.|Price 27.50 USD.`nTax excluded."; font='Segoe UI'; critical=@('not','27.50','excluded') },
    @{ id='accept-negation'; split='acceptance'; text='验证尚未通过，请勿断开电源。'; font='Microsoft YaHei UI'; critical=@('未','请勿') },
    @{ id='accept-units'; split='acceptance'; text='Pressure 120 kPa; never exceed 24 V.'; font='Arial'; critical=@('120','never','24') },
    @{ id='accept-refund'; split='acceptance'; text='可退押金650元，不含运输费用。'; font='Microsoft YaHei UI'; critical=@('650','不含') },
    @{ id='accept-two-lines'; split='acceptance'; text="The backup is not complete.`nDo not restart yet."; font='Segoe UI'; critical=@('not','not','yet') },
    @{ id='accept-version'; split='acceptance'; text='组件 v4.7 已更新，不要删除缓存。'; font='Microsoft YaHei UI'; critical=@('v4.7','不要') },
    @{ id='accept-columns'; split='acceptance'; text="Save the file.`nDo not exit.|Cost 63.20 EUR.`nTax included."; font='Arial'; critical=@('not','63.20','included') }
)

function Write-RefinementImage($subject, $condition, $path) {
    $image = New-Object Drawing.Bitmap(900, 130)
    $drawing = [Drawing.Graphics]::FromImage($image)
    $font = New-Object Drawing.Font($subject.font, 12, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
    $background = if ($condition -eq 'dark') { [Drawing.Color]::FromArgb(30,32,36) } else { [Drawing.Color]::White }
    $foreground = if ($condition -eq 'dark') { [Drawing.Color]::White } elseif ($condition -eq 'low-contrast') { [Drawing.Color]::FromArgb(150,150,150) } else { [Drawing.Color]::Black }
    $brush = New-Object Drawing.SolidBrush($foreground)
    try {
        $drawing.Clear($background)
        if ($condition -eq 'framed') {
            $drawing.Clear([Drawing.Color]::FromArgb(28,30,34))
            $drawing.FillRectangle([Drawing.Brushes]::White, 10, 10, 880, 110)
        }
        $drawing.TextRenderingHint = [Drawing.Text.TextRenderingHint]::ClearTypeGridFit
        $columns = $subject.text.Split('|')
        for ($i=0; $i -lt $columns.Count; $i++) { $drawing.DrawString($columns[$i],$font,$brush,24+450*$i,24) }
        if ($condition -eq 'jpeg') {
            $codec = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
            $parameters = New-Object Drawing.Imaging.EncoderParameters(1)
            $quality = New-Object Drawing.Imaging.EncoderParameter([Drawing.Imaging.Encoder]::Quality,[long]70)
            try { $parameters.Param[0]=$quality; $image.Save($path,$codec,$parameters) }
            finally { $quality.Dispose(); $parameters.Dispose() }
        }
        else { $image.Save($path,[Drawing.Imaging.ImageFormat]::Png) }
    }
    finally { $brush.Dispose(); $font.Dispose(); $drawing.Dispose(); $image.Dispose() }
}

$fixtures=@()
foreach ($subject in $subjects) {
    foreach ($condition in @('dark','low-contrast','jpeg','framed')) {
        $extension=if ($condition -eq 'jpeg') { '.jpg' } else { '.png' }
        $name=$subject.id+'-'+$condition+$extension
        $path=Join-Path $OutputDirectory $name
        Write-RefinementImage $subject $condition $path
        $fixtures += [ordered]@{ id=$name; split=$subject.split; condition=$condition; expected=$subject.text.Replace('|',"`n");
            critical=$subject.critical; path=$name; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
}
[ordered]@{ license='MIT'; createdUtc=[DateTime]::UtcNow.ToString('o'); fixtures=$fixtures;
    scope='Authored 12-pixel compressed, low-contrast, framed, dark, wrapped and column fixtures. Acceptance family stays unread until final implementation evaluation; no general accuracy claim.' } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'manifest.json') -Encoding UTF8
Write-Output ('Froze '+$fixtures.Count+' OCR fixtures in development/acceptance families.')
