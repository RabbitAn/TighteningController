# 生成工业风格拧紧控制器ICO图标
# 使用多种尺寸以确保在不同场景下显示清晰

Add-Type -AssemblyName System.Drawing

function Create-Icon {
    param([int]$sz)
    
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    
    # 深蓝色工业背景
    $bgColor = [System.Drawing.Color]::FromArgb(30, 58, 95)
    $g.Clear($bgColor)
    
    # 绘制圆角矩形背景
    $cornerRadius = [int]($sz * 0.15)
    $rw = $sz - 5
    $rh = $sz - 5
    $rect = New-Object System.Drawing.Rectangle(2, 2, $rw, $rh)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $cornerRadius, $cornerRadius, 180, 90)
    $path.AddArc($rect.Right - $cornerRadius, $rect.Y, $cornerRadius, $cornerRadius, 270, 90)
    $path.AddArc($rect.Right - $cornerRadius, $rect.Bottom - $cornerRadius, $cornerRadius, $cornerRadius, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $cornerRadius, $cornerRadius, $cornerRadius, 90, 90)
    $path.CloseFigure()
    
    # 内部面板
    $panelColor = [System.Drawing.Color]::FromArgb(45, 80, 120)
    $panelBrush = New-Object System.Drawing.SolidBrush($panelColor)
    $g.FillPath($panelBrush, $path)
    
    # 边框高光
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(100, 150, 200), [Math]::Max(1, $sz / 32))
    $g.DrawPath($borderPen, $path)
    
    # 螺栓中心
    $centerX = $sz / 2
    $centerY = $sz / 2
    $boltRadius = $sz * 0.22
    
    # 六边形螺栓头部
    $boltBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 210, 220))
    $boltPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, 170, 180), [Math]::Max(1, $sz / 32))
    
    $boltPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $hexPoints = New-Object System.Drawing.PointF[] 6
    for ($i = 0; $i -lt 6; $i++) {
        $angle = [Math]::PI / 3 * $i - [Math]::PI / 6
        $hx = $centerX + $boltRadius * [Math]::Cos($angle)
        $hy = $centerY + $boltRadius * [Math]::Sin($angle)
        $hexPoints[$i] = New-Object System.Drawing.PointF($hx, $hy)
    }
    $boltPath.AddPolygon($hexPoints)
    $g.FillPath($boltBrush, $boltPath)
    $g.DrawPath($boltPen, $boltPath)
    
    # 中心孔
    $holeRadius = $boltRadius * 0.35
    $holeBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(25, 45, 75))
    $g.FillEllipse($holeBrush, $centerX - $holeRadius, $centerY - $holeRadius, $holeRadius * 2, $holeRadius * 2)
    
    # 旋转箭头
    if ($sz -ge 16) {
        $arrowColor = [System.Drawing.Color]::FromArgb(0, 200, 150)
        $arrowPen = New-Object System.Drawing.Pen($arrowColor, [Math]::Max(1.5, $sz / 20))
        $arrowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::ArrowAnchor
        
        $arrowRadius = $sz * 0.38
        $startAngle = 200
        $sweepAngle = 140
        $arx = $centerX - $arrowRadius
        $ary = $centerY - $arrowRadius
        $arw = $arrowRadius * 2
        $arh = $arrowRadius * 2
        $arrowRect = New-Object System.Drawing.RectangleF($arx, $ary, $arw, $arh)
        $g.DrawArc($arrowPen, $arrowRect, $startAngle, $sweepAngle)
        $arrowPen.Dispose()
    }
    
    # 状态指示灯
    if ($sz -ge 24) {
        $ledRadius = [Math]::Max(2, $sz / 12)
        $ledX = $sz - $ledRadius * 2.5
        $ledY = $sz - $ledRadius * 2.5
        
        $glowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 0, 255, 100))
        $g.FillEllipse($glowBrush, $ledX - $ledRadius, $ledY - $ledRadius, $ledRadius * 4, $ledRadius * 4)
        
        $ledBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 230, 120))
        $g.FillEllipse($ledBrush, $ledX, $ledY, $ledRadius * 2, $ledRadius * 2)
        
        $glowBrush.Dispose()
        $ledBrush.Dispose()
    }
    
    # 清理
    $panelBrush.Dispose()
    $borderPen.Dispose()
    $boltBrush.Dispose()
    $boltPen.Dispose()
    $holeBrush.Dispose()
    $path.Dispose()
    $boltPath.Dispose()
    $g.Dispose()
    
    return $bmp
}

# 创建多尺寸图标
$sizes = @(16, 24, 32, 48, 64, 128, 256)
[System.Collections.ArrayList]$bitmapList = @()

foreach ($size in $sizes) {
    $bmp = Create-Icon -sz $size
    [void]$bitmapList.Add($bmp)
}

# ICO 文件头
$icoStream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($icoStream)

# ICONDIR
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$bitmapList.Count)

$headerSize = 6 + ($bitmapList.Count * 16)
$dataOffset = $headerSize

$imageDataList = New-Object System.Collections.ArrayList

foreach ($bmp in $bitmapList) {
    $width = $bmp.Width
    $height = $bmp.Height
    
    $pngStream = New-Object System.IO.MemoryStream
    $bmp.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData = $pngStream.ToArray()
    $pngStream.Dispose()
    
    [void]$imageDataList.Add($pngData)
    
    $wb = if ($width -ge 256) { 0 } else { $width }
    $hb = if ($height -ge 256) { 0 } else { $height }
    $writer.Write([byte]$wb)
    $writer.Write([byte]$hb)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$pngData.Length)
    $writer.Write([UInt32]$dataOffset)
    
    $dataOffset += $pngData.Length
}

foreach ($data in $imageDataList) {
    $writer.Write($data)
}

$icoData = $icoStream.ToArray()
[System.IO.File]::WriteAllBytes("app.ico", $icoData)

$writer.Dispose()
$icoStream.Dispose()
foreach ($bmp in $bitmapList) {
    $bmp.Dispose()
}

Write-Host "成功创建工业风格图标 app.ico"
Write-Host "  包含尺寸: $($sizes -join ', ')"
Write-Host "  文件大小: $([Math]::Round((Get-Item 'app.ico').Length / 1024, 2)) KB"
