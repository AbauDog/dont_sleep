Add-Type -AssemblyName System.Drawing

$size = 256
$destPath = "c:\Users\tatung\Downloads\VSC\dont_sleep\resources\app_icon.ico"
Write-Host "Generating icon at: $destPath"

try {
    # Create Bitmap
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    # Colors
    $greenColor = [System.Drawing.Color]::FromArgb(57, 255, 20) # Neon Green
    $greenPen = New-Object System.Drawing.Pen($greenColor, 12)
    $greenBrush = New-Object System.Drawing.SolidBrush($greenColor)
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 255, 255, 255))
    $blackBrush = [System.Drawing.Brushes]::Black

    # Background: Rounded Square
    $g.Clear([System.Drawing.Color]::Transparent)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $radius = 50
    $path.AddArc($rect.X, $rect.Y, $radius, $radius, 180, 90)
    $path.AddArc($rect.Right - $radius, $rect.Y, $radius, $radius, 270, 90)
    $path.AddArc($rect.Right - $radius, $rect.Bottom - $radius, $radius, $radius, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $radius, $radius, $radius, 90, 90)
    $path.CloseFigure()
    $g.FillPath($blackBrush, $path)

    # Eye Shape
    # Top lid
    $p1 = New-Object System.Drawing.Point(30, 128)
    $pTop = New-Object System.Drawing.Point(128, 40)
    $p2 = New-Object System.Drawing.Point(226, 128)
    # Bottom lid
    $pBottom = New-Object System.Drawing.Point(128, 216)

    $g.DrawCurve($greenPen, @($p1, $pTop, $p2), 0.5)
    $g.DrawCurve($greenPen, @($p1, $pBottom, $p2), 0.5)

    # Iris
    $g.FillEllipse($greenBrush, 83, 83, 90, 90)
    
    # Pupil Reflection
    $g.FillEllipse($whiteBrush, 145, 110, 20, 20)

    $g.Dispose()

    # Save as Icon
    # GetHicon creates a Windows handle to an icon.
    $iconHandle = $bmp.GetHicon()
    $icon = [System.Drawing.Icon]::FromHandle($iconHandle)
    
    $fileStream = New-Object System.IO.FileStream($destPath, [System.IO.FileMode]::Create)
    $icon.Save($fileStream)
    $fileStream.Close()
    
    # Cleanup
    # Note: DestroyIcon might be needed in C++, but in .NET Icon.Dispose should handle it, mostly.
    $icon.Dispose()
    $bmp.Dispose()
    
    Write-Host "Icon generation successful."
} catch {
    Write-Error "Failed to generate icon: $_"
    exit 1
}
