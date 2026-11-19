# Builds the Blood II: The Chosen HD Patch: BLOOD2.EXE (the new launcher) and Blood2HD_Setup.exe
# (C#, .NET Framework 4). Output: out\Blood2HD_Setup.exe (single file, everything embedded) and
# out\release\ (package for upload)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src = Join-Path $root 'src'
$payload = Join-Path $root 'payload'
$out = Join-Path $root 'out'
$obj = Join-Path $out 'obj'
New-Item -ItemType Directory -Force $obj | Out-Null

# Roslyn csc (C# 7.3) from any Visual Studio / Build Tools installation (2019 or newer)
$csc = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path $csc)) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vs = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($vs) { $csc = Join-Path $vs 'MSBuild\Current\Bin\Roslyn\csc.exe' }
    }
}
if (-not (Test-Path $csc)) { throw 'csc.exe (Roslyn) not found: install the Visual Studio Build Tools with ".NET desktop build tools"' }
$fw = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319'

function Invoke-Checked([string]$what, [scriptblock]$cmd) {
    & $cmd
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit $LASTEXITCODE)" }
}

# --- icon (generated, so the package carries no game artwork): a drop of blood on black ---------
$ico = Join-Path $obj 'blood2hd.ico'
Add-Type -AssemblyName System.Drawing
$frames = foreach ($size in 256, 48, 32, 16) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::FromArgb(255, 14, 10, 10))
    $c = $size / 2.0
    # the drop: a circle with a pointed top
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = $size * 0.27
    $cy = $c + $size * 0.12
    $path.AddArc([float]($c - $r), [float]($cy - $r), [float]($r * 2), [float]($r * 2), -30, 240)
    $path.AddLine([float]($c - $r * 0.866), [float]($cy - $r * 0.5), [float]$c, [float]($size * 0.10))
    $path.CloseFigure()
    $red = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF 0, 0, $size, $size),
        ([System.Drawing.Color]::FromArgb(255, 214, 40, 32)), ([System.Drawing.Color]::FromArgb(255, 96, 6, 6)), 60
    $g.FillPath($red, $path)
    # a highlight
    $shine = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 255, 210, 200))
    $g.FillEllipse($shine, [float]($c - $r * 0.55), [float]($cy - $r * 0.35), [float]($r * 0.3), [float]($r * 0.45))
    $edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 92, 46, 40)), ([Math]::Max(1, $size / 32))
    $g.DrawRectangle($edge, 0, 0, $size - 1, $size - 1)
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , @($size, $ms.ToArray())
}
$fs = [IO.File]::Create($ico)
$w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $s = if ($f[0] -ge 256) { 0 } else { $f[0] }
    $w.Write([byte]$s); $w.Write([byte]$s); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$f[1].Length); $w.Write([uint32]$offset)
    $offset += $f[1].Length
}
foreach ($f in $frames) { $w.Write($f[1]) }
$w.Close()

# --- C# ---------------------------------------------------------------------------------------
$refs = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'Microsoft.CSharp.dll') |
    ForEach-Object { "/reference:$fw\$_" }
$common = Get-ChildItem "$src\common\*.cs" | ForEach-Object { $_.FullName }
$cscBase = @('/nologo', '/target:winexe', '/optimize+', '/warnaserror+', '/platform:anycpu', '/langversion:7.3',
             "/win32manifest:$src\app.manifest", "/win32icon:$ico", '/utf8output',
             "/resource:$src\res\compiware.png,res/compiware.png", "/resource:$src\res\bockwurstwilly.png,res/bockwurstwilly.png") + $refs

Write-Host '== BLOOD2.EXE (launcher)'
$launcher = Join-Path $obj 'BLOOD2.EXE'
Invoke-Checked 'launcher' { & $csc @cscBase "/out:$launcher" @common (Get-ChildItem "$src\launcher\*.cs" | ForEach-Object { $_.FullName }) }

Write-Host '== Blood2HD_Setup.exe'
# payload\game\... is installed into the game folder as is; the launcher comes from the build
$resources = @()
foreach ($f in Get-ChildItem "$payload\game" -File -Recurse) {
    $rel = $f.FullName.Substring($payload.Length + 1).Replace('\', '/')
    $resources += "/resource:$($f.FullName),payload/$rel"
}
$resources += "/resource:$launcher,payload/game/BLOOD2.EXE"
$resources += "/resource:$root\README.txt,payload/doc/README.txt"
$resources += "/resource:$root\THIRD-PARTY.txt,payload/doc/THIRD-PARTY.txt"
foreach ($f in Get-ChildItem "$payload\licenses" -File) { $resources += "/resource:$($f.FullName),payload/doc/licenses/$($f.Name)" }
$setup = Join-Path $out 'Blood2HD_Setup.exe'
Invoke-Checked 'setup' { & $csc @cscBase "/out:$setup" @common (Get-ChildItem "$src\setup\*.cs" | ForEach-Object { $_.FullName }) @resources }

$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
Write-Host ("== done: {0} ({1:N1} MB) SHA256 {2}" -f $setup, ((Get-Item $setup).Length / 1MB), $hash)

# --- release package ----------------------------------------------------------------------------
$version = ([regex]::Match((Get-Content "$src\common\Text.cs" -Raw), 'Text = "([^"]+)"')).Groups[1].Value
$name = "Blood2HD_Patch_$version"
$rel = Join-Path $out "release\$name"
if (Test-Path $rel) { Remove-Item $rel -Recurse -Force }
New-Item -ItemType Directory -Force "$rel\Lizenzen - Licenses" | Out-Null
Copy-Item $setup "$rel\Blood2HD_Setup.exe"
Copy-Item "$root\README.txt" "$rel\Liesmich - Readme.txt"
Copy-Item "$root\THIRD-PARTY.txt" "$rel\Lizenzen - Licenses\THIRD-PARTY.txt"
Copy-Item "$payload\licenses\*" "$rel\Lizenzen - Licenses\"
Copy-Item "$root\LICENSE" "$rel\Lizenzen - Licenses\HD-Patch-License.txt"
"$hash *Blood2HD_Setup.exe" | Set-Content "$rel\Blood2HD_Setup.exe.sha256" -Encoding ASCII
$zip = Join-Path $out "release\$name.zip"
if (Test-Path $zip) { Remove-Item $zip }
# entries with forward slashes (Compress-Archive in PowerShell 5.1 writes backslashes, which other tools reject)
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$za = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($f in Get-ChildItem $rel -Recurse -File) {
        $entry = $name + '/' + $f.FullName.Substring($rel.Length + 1).Replace([char]92, [char]47)
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $f.FullName, $entry, 'Optimal')
    }
}
finally { $za.Dispose() }
Write-Host ("== release: {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
