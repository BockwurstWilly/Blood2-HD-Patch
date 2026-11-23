# Starts Client.exe with the given command line, takes a screenshot of the main menu and quits again.
# No input is sent. Optional DSOAL log (level 4) next to the screenshot.
param([Parameter(Mandatory)] [string]$Game, [Parameter(Mandatory)] [string]$Line, [Parameter(Mandatory)] [string]$Png, [int]$Wait = 22, [switch]$Log)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace B2M -Name U -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[void][B2M.U]::SetProcessDPIAware()
if ($Log) { $env:DSOAL_LOGLEVEL = '4'; $env:DSOAL_LOGFILE = [IO.Path]::ChangeExtension($Png, '.log') }
$proc = Start-Process (Join-Path $Game 'Client.exe') -ArgumentList $Line -WorkingDirectory $Game -PassThru
try {
    Start-Sleep $Wait
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    [System.Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $h = [int](960 * $b.Height / $b.Width)
    (New-Object System.Drawing.Bitmap $bmp, 960, $h).Save($Png)
    "alive: $(-not $proc.HasExited)"
}
finally {
    $proc.Refresh()
    if (-not $proc.HasExited) { [void]$proc.CloseMainWindow(); Start-Sleep 5; $proc.Refresh() }
    if (-not $proc.HasExited) { Stop-Process $proc -Force }
    Remove-Item Env:DSOAL_LOGLEVEL, Env:DSOAL_LOGFILE -ErrorAction SilentlyContinue
}
