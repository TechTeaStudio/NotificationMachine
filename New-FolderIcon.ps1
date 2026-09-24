<#
.SYNOPSIS
    Rebuilds the repository's Windows folder icon from the package icon.

.DESCRIPTION
    Generates .icon\folder.ico (16, 32, 48, 64, 128 and 256 px frames) from the NuGet package
    icon, writes the desktop.ini that binds it, and sets the attributes the shell requires:
    hidden+system on desktop.ini, read-only on the folder itself.

    Both outputs are gitignored - git cannot carry the hidden+system attribute, so a committed
    desktop.ini would just be a visible file that does nothing. Run this once after a fresh
    clone if you want the icon.

.EXAMPLE
    .\New-FolderIcon.ps1
#>
[CmdletBinding()]
param(
    [string]$Source,
    [string]$Root
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Resolved here, not in the param defaults: Windows PowerShell leaves $PSScriptRoot empty during
# parameter binding when the script is launched by relative path (powershell -File .\this.ps1).
if (-not $Root) {
    $Root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
}
if (-not $Source) { $Source = Join-Path $Root 'src\TechTeaStudio.NotificationMachine\icon.png' }

if (-not (Test-Path $Source)) { throw "Package icon not found: $Source" }

$sizes = @(256, 128, 64, 48, 32, 16)
$src = [System.Drawing.Image]::FromFile((Resolve-Path $Source).Path)
$blobs = New-Object 'System.Collections.Generic.List[byte[]]'

try {
    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        # TileFlipXY stops the edge pixels being blended against transparent black, which is
        # what makes naively downscaled icons look like they have a dark halo at 16px.
        $attr = New-Object System.Drawing.Imaging.ImageAttributes
        $attr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        $g.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, $size, $size)),
            0, 0, $src.Width, $src.Height, [System.Drawing.GraphicsUnit]::Pixel, $attr)
        $g.Dispose(); $attr.Dispose()

        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $blobs.Add($ms.ToArray())
        $ms.Dispose(); $bmp.Dispose()
    }
}
finally { $src.Dispose() }

# ICO container: 6-byte ICONDIR, one 16-byte ICONDIRENTRY per frame, then the PNG payloads.
# PNG-compressed frames are what Vista+ expects at 256px and the shell reads them at every size.
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }   # 256 is encoded as 0
    $w.Write([byte]$dim); $w.Write([byte]$dim)
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$blobs[$i].Length); $w.Write([uint32]$offset)
    $offset += $blobs[$i].Length
}
foreach ($blob in $blobs) { $w.Write($blob) }
$w.Flush()

$iconDir = Join-Path $Root '.icon'
New-Item -ItemType Directory -Force $iconDir | Out-Null
$icoPath = Join-Path $iconDir 'folder.ico'
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
$w.Dispose(); $out.Dispose()

# UTF-16 LE with BOM - the encoding the shell expects and the one the sibling repos use.
$iniPath = Join-Path $Root 'desktop.ini'
if (Test-Path $iniPath) { attrib -h -s $iniPath }
[System.IO.File]::WriteAllText(
    $iniPath,
    "[.ShellClassInfo]`r`nIconResource=.icon\folder.ico,0`r`nConfirmFileOp=0`r`n",
    (New-Object System.Text.UnicodeEncoding($false, $true)))

attrib +h +s $iniPath
attrib +r $Root

$notify = '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int e, uint f, IntPtr a, IntPtr b);'
(Add-Type -MemberDefinition $notify -Name Shell -Namespace FolderIcon -PassThru)::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

Write-Host "Wrote $icoPath ($((Get-Item $icoPath).Length) bytes, frames: $($sizes -join ', '))"
Write-Host "Wrote $iniPath"
Write-Host "Explorer may need a restart to drop its cached icon for this folder."
