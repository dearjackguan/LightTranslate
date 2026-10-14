$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $projectRoot 'assets'
$previews = 'A:\Agent\Codex\Translation\icons'
New-Item -ItemType Directory -Path $assets, $previews -Force | Out-Null

function New-GlyphPath([string]$Text, [string]$Family, [Drawing.RectangleF]$Box) {
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $font = [Drawing.FontFamily]::new($Family)
    try {
        $path.AddString($Text, $font, [int][Drawing.FontStyle]::Bold, 128,
            [Drawing.PointF]::Empty, [Drawing.StringFormat]::GenericTypographic)
        $bounds = $path.GetBounds()
        $scale = [Math]::Min($Box.Width / $bounds.Width, $Box.Height / $bounds.Height)
        $matrix = [Drawing.Drawing2D.Matrix]::new()
        try {
            $matrix.Translate(-$bounds.X, -$bounds.Y, [Drawing.Drawing2D.MatrixOrder]::Append)
            $matrix.Scale($scale, $scale, [Drawing.Drawing2D.MatrixOrder]::Append)
            $matrix.Translate($Box.X + ($Box.Width - $bounds.Width * $scale) / 2,
                $Box.Y + ($Box.Height - $bounds.Height * $scale) / 2, [Drawing.Drawing2D.MatrixOrder]::Append)
            $path.Transform($matrix)
        } finally { $matrix.Dispose() }
    } finally { $font.Dispose() }
    return ,$path
}

function ConvertTo-SvgPath([Drawing.Drawing2D.GraphicsPath]$Path) {
    $points = $Path.PathPoints
    $types = $Path.PathTypes
    $commands = [Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $points.Length; $i++) {
        $kind = $types[$i] -band 7
        $coordinates = $points[$i].X.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture) + ',' +
            $points[$i].Y.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture)
        if ($kind -eq 0) { $commands.Add('M' + $coordinates) }
        elseif ($kind -eq 1) { $commands.Add('L' + $coordinates) }
        elseif ($kind -eq 3) {
            $segment = @($coordinates)
            for ($j = 1; $j -le 2; $j++) {
                $i++
                $segment += $points[$i].X.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture) + ',' +
                    $points[$i].Y.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture)
            }
            $commands.Add('C' + ($segment -join ' '))
        }
        if ($types[$i] -band 128) { $commands.Add('Z') }
    }
    return $commands -join ' '
}

function New-IconBitmap([int]$Size, $Base, $Chinese, $Latin) {
    $large = [Drawing.Bitmap]::new($Size * 4, $Size * 4, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $drawing = [Drawing.Graphics]::FromImage($large)
    $blue = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#1E57CD'))
    $white = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#FFFFFF'))
    $arrow = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#8DECE2'), 9)
    try {
        $drawing.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $drawing.ScaleTransform($Size * 4 / 256, $Size * 4 / 256)
        $drawing.FillPath($blue, $Base)
        $drawing.FillPath($white, $Chinese)
        $drawing.FillPath($white, $Latin)
        $arrow.StartCap = $arrow.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $arrow.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
        $drawing.DrawLines($arrow, [Drawing.PointF[]]@([Drawing.PointF]::new(47,65), [Drawing.PointF]::new(113,65)))
        $drawing.DrawLines($arrow, [Drawing.PointF[]]@([Drawing.PointF]::new(99,51), [Drawing.PointF]::new(113,65), [Drawing.PointF]::new(99,79)))
        $drawing.DrawLines($arrow, [Drawing.PointF[]]@([Drawing.PointF]::new(209,190), [Drawing.PointF]::new(143,190)))
        $drawing.DrawLines($arrow, [Drawing.PointF[]]@([Drawing.PointF]::new(157,176), [Drawing.PointF]::new(143,190), [Drawing.PointF]::new(157,204)))
        $bitmap = [Drawing.Bitmap]::new($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $small = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $small.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $small.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $small.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $small.DrawImage($large, [Drawing.Rectangle]::new(0, 0, $Size, $Size))
        } finally { $small.Dispose() }
        return ,$bitmap
    } finally { $drawing.Dispose(); $large.Dispose(); $blue.Dispose(); $white.Dispose(); $arrow.Dispose() }
}

function Get-DibFrame([Drawing.Bitmap]$Bitmap) {
    $size = $Bitmap.Width
    $locked = $Bitmap.LockBits([Drawing.Rectangle]::new(0, 0, $size, $size),
        [Drawing.Imaging.ImageLockMode]::ReadOnly, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = [byte[]]::new($locked.Stride * $size)
    try { [Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $pixels, 0, $pixels.Length) }
    finally { $Bitmap.UnlockBits($locked) }
    $maskStride = [int][Math]::Ceiling($size / 32) * 4
    $mask = [byte[]]::new($maskStride * $size)
    for ($row = 0; $row -lt $size; $row++) {
        for ($col = 0; $col -lt $size; $col++) {
            if ($pixels[$row * $locked.Stride + $col * 4 + 3] -eq 0) {
                $index = ($size - 1 - $row) * $maskStride + [int][Math]::Floor($col / 8)
                $mask[$index] = $mask[$index] -bor (128 -shr ($col % 8))
            }
        }
    }
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint32]40); $writer.Write([int]$size); $writer.Write([int]($size * 2))
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]0)
        $writer.Write([uint32]($pixels.Length + $mask.Length))
        1..4 | ForEach-Object { $writer.Write([uint32]0) }
        for ($row = $size - 1; $row -ge 0; $row--) { $writer.Write($pixels, $row * $locked.Stride, $size * 4) }
        $writer.Write($mask)
        return ,$stream.ToArray()
    } finally { $writer.Dispose(); $stream.Dispose() }
}

$base = [Drawing.Drawing2D.GraphicsPath]::new()
$base.AddArc(8, 8, 112, 112, 180, 90)
$base.AddArc(136, 8, 112, 112, 270, 90)
$base.AddArc(136, 136, 112, 112, 0, 90)
$base.AddArc(8, 136, 112, 112, 90, 90)
$base.CloseFigure()
$chinese = New-GlyphPath '文' 'Microsoft YaHei UI' ([Drawing.RectangleF]::new(41, 102, 99, 106))
$latin = New-GlyphPath 'A' 'Segoe UI' ([Drawing.RectangleF]::new(143, 48, 74, 96))
$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$frames = [Collections.Generic.List[byte[]]]::new()
try {
    foreach ($size in $sizes) {
        $bitmap = New-IconBitmap $size $base $chinese $latin
        try {
            $bitmap.Save((Join-Path $previews "icon-$size.png"), [Drawing.Imaging.ImageFormat]::Png)
            if ($size -eq 256) {
                $stream = [IO.MemoryStream]::new()
                try { $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png); $frames.Add($stream.ToArray()) }
                finally { $stream.Dispose() }
                $bitmap.Save((Join-Path $assets 'app-icon.png'), [Drawing.Imaging.ImageFormat]::Png)
            } else { $frames.Add((Get-DibFrame $bitmap)) }
        } finally { $bitmap.Dispose() }
    }
    $stream = [IO.File]::Create((Join-Path $assets 'app.ico'))
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Length)
        $offset = 6 + 16 * $sizes.Length
        for ($i = 0; $i -lt $sizes.Length; $i++) {
            $dimension = $sizes[$i] % 256
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write($frame) }
    } finally { $writer.Dispose(); $stream.Dispose() }
    $svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256"><title>LightTranslate</title>' +
        '<path fill="#1E57CD" d="' + (ConvertTo-SvgPath $base) + '"/>' +
        '<g fill="#fff"><path d="' + (ConvertTo-SvgPath $chinese) + '"/><path d="' + (ConvertTo-SvgPath $latin) + '"/></g>' +
        '<g fill="none" stroke="#8DECE2" stroke-width="9" stroke-linecap="round" stroke-linejoin="round">' +
        '<path d="M47 65H113M99 51L113 65 99 79M209 190H143M157 176L143 190 157 204"/></g></svg>'
    [IO.File]::WriteAllText((Join-Path $assets 'app-icon.svg'), $svg, [Text.UTF8Encoding]::new($false))
} finally { $base.Dispose(); $chinese.Dispose(); $latin.Dispose() }
Write-Output ('Created SVG, PNG and ICO frames: ' + ($sizes -join ', '))
