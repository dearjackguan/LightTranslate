param([Parameter(Mandatory=$true)][string]$ImagePath)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$softwareBitmap = $null
$enlargedBitmap = $null
$stream = $null

try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $null = [Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
    $null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType=WindowsRuntime]
    $null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]

    $asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and
        $_.GetGenericArguments().Count -eq 1 -and $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
    } | Select-Object -First 1

    function Wait-WinRtOperation($operation, $type) {
        $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation))
        if (-not $task.Wait(15000)) { throw 'OCR_FAILURE' }
        return $task.Result
    }

    function Get-SmallTextScale($result, $width, $height, $maximum) {
        $heights = @($result.Lines.Words | Where-Object { $_.Text -match '[\p{L}\p{N}]' } |
            ForEach-Object { $_.BoundingRect.Height } | Where-Object { $_ -gt 0 } | Sort-Object)
        if ($heights.Count -gt 0) {
            $middle = [int][Math]::Floor($heights.Count / 2)
            $lower = [int][Math]::Floor(($heights.Count - 1) / 2)
            if (($heights[$lower] + $heights[$middle]) / 2.0 -ge 18) { return 1.0 }
        }
        $dimensionScale = [Math]::Min($maximum / [double]$width, $maximum / [double]$height)
        $pixelScale = [Math]::Sqrt(8000000.0 / ([double]$width * $height))
        return [Math]::Max(1.0, [Math]::Min(2.0, [Math]::Min($dimensionScale, $pixelScale)))
    }

    function Read-EnlargedBitmap($decoder, $scale) {
        $transform = New-Object Windows.Graphics.Imaging.BitmapTransform
        $transform.ScaledWidth = [uint32][Math]::Floor($decoder.PixelWidth * $scale)
        $transform.ScaledHeight = [uint32][Math]::Floor($decoder.PixelHeight * $scale)
        $transform.InterpolationMode = 'Cubic'
        return Wait-WinRtOperation ($decoder.GetSoftwareBitmapAsync('Bgra8', 'Ignore', $transform,
            'IgnoreExifOrientation', 'DoNotColorManage')) ([Windows.Graphics.Imaging.SoftwareBitmap])
    }

    $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
    if ($null -eq $engine) { throw 'OCR_LANGUAGE_UNAVAILABLE' }
    $file = Wait-WinRtOperation ([Windows.Storage.StorageFile]::GetFileFromPathAsync($ImagePath)) ([Windows.Storage.StorageFile])
    $stream = Wait-WinRtOperation ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
    $decoder = Wait-WinRtOperation ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $softwareBitmap = Wait-WinRtOperation ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
    $maximum = [Windows.Media.Ocr.OcrEngine]::MaxImageDimension
    if ($softwareBitmap.PixelWidth -gt $maximum -or $softwareBitmap.PixelHeight -gt $maximum) {
        throw 'OCR_IMAGE_TOO_LARGE'
    }
    $result = Wait-WinRtOperation ($engine.RecognizeAsync($softwareBitmap)) ([Windows.Media.Ocr.OcrResult])
    $scale = Get-SmallTextScale $result $softwareBitmap.PixelWidth $softwareBitmap.PixelHeight $maximum
    if ($scale -gt 1.0) {
        $enlargedBitmap = Read-EnlargedBitmap $decoder $scale
        $enlargedResult = Wait-WinRtOperation ($engine.RecognizeAsync($enlargedBitmap)) ([Windows.Media.Ocr.OcrResult])
        if (-not [string]::IsNullOrWhiteSpace($enlargedResult.Text)) { $result = $enlargedResult }
        else { $scale = 1.0 }
    }
    $text = ($result.Lines | ForEach-Object { $_.Text }) -join "`n"
    $text = [regex]::Replace($text, '(?<=[\p{IsCJKUnifiedIdeographs}]) +(?=[\p{IsCJKUnifiedIdeographs}])', '')
    $lines = @($result.Lines | ForEach-Object {
        $words = @($_.Words | ForEach-Object {
            @{ text = $_.Text; x = $_.BoundingRect.X; y = $_.BoundingRect.Y;
               width = $_.BoundingRect.Width; height = $_.BoundingRect.Height }
        })
        @{ text = $_.Text; words = $words }
    })
    @{ ok = $true; text = $text; lines = $lines; angle = $result.TextAngle;
       language = $engine.RecognizerLanguage.LanguageTag;
       preprocessing = @{ scale = $scale; originalWidth = $softwareBitmap.PixelWidth;
           originalHeight = $softwareBitmap.PixelHeight } } | ConvertTo-Json -Compress -Depth 6
}
catch {
    $code = $_.Exception.Message
    if ($code -notin @('OCR_LANGUAGE_UNAVAILABLE', 'OCR_IMAGE_TOO_LARGE')) { $code = 'OCR_FAILURE' }
    @{ ok = $false; error = $code } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $enlargedBitmap) { $enlargedBitmap.Dispose() }
    if ($null -ne $softwareBitmap) { $softwareBitmap.Dispose() }
    if ($null -ne $stream) { $stream.Dispose() }
}
