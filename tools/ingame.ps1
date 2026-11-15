# In-game smoke test of an installed copy: starts Client.exe with the launcher's command line,
# walks into the first level and moves/turns/fires there, with screenshots and a DSOAL log.
# Input is only sent while the game window is in the foreground; otherwise the test stops.
param([Parameter(Mandatory)] [string]$Game, [Parameter(Mandatory)] [string]$Out, [int]$CutsceneWait = 80, [string]$LogLevel = '3', [switch]$MouseTest, [switch]$ViaLauncher, [string]$ExtraArgs = '', [int]$Width = 0, [int]$Height = 0)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace B2 -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
'@
[void][B2.U]::SetProcessDPIAware()
New-Item -ItemType Directory -Force $Out | Out-Null

function Shot($name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    [System.Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    (New-Object System.Drawing.Bitmap $bmp, 960, 540).Save((Join-Path $Out "$name.png"))
}
function GameInFront {
    $pid0 = 0
    [void][B2.U]::GetWindowThreadProcessId([B2.U]::GetForegroundWindow(), [ref]$pid0)
    return $pid0 -eq $script:proc.Id
}
function Guard {
    if (-not (GameInFront)) {
        $script:proc.Refresh()
        if ($script:proc.MainWindowHandle -ne [IntPtr]::Zero) { [void][B2.U]::SetForegroundWindow($script:proc.MainWindowHandle); Start-Sleep -Milliseconds 400 }
        if (-not (GameInFront)) { throw 'game window not in front - stopping before any input goes elsewhere' }
    }
}
# DirectInput reads scan codes: send them as such (KEYEVENTF_SCANCODE = 8, KEYUP = 2)
function Key([byte]$scan, [int]$holdMs = 80) {
    Guard
    [B2.U]::keybd_event(0, $scan, 8, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $holdMs
    [B2.U]::keybd_event(0, $scan, 10, [UIntPtr]::Zero)
}
function MouseMove([int]$dx, [int]$steps) {
    for ($i = 0; $i -lt $steps; $i++) { Guard; [B2.U]::mouse_event(1, $dx, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 15 }
}
# like a 1000 Hz gaming mouse: one event per millisecond (busy-wait, Sleep is too coarse)
function MouseFast([int]$dx, [int]$count) {
    Guard
    $sw = [Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt $count; $i++) {
        [B2.U]::mouse_event(1, $dx, 0, 0, [UIntPtr]::Zero)
        while ($sw.Elapsed.TotalMilliseconds -lt ($i + 1)) { }
    }
}
function Click { Guard; [B2.U]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120; [B2.U]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero) }

$cmd = Join-Path $env:TEMP 'b2_ingame_cmd.txt'
Start-Process (Join-Path $Game 'BLOOD2.EXE') -ArgumentList "/cmdline=`"$cmd`"" -Wait
$env:DSOAL_LOGLEVEL = $LogLevel
$env:ALSOFT_LOGLEVEL = '3'
$env:ALSOFT_LOGFILE = Join-Path $Out 'alsoft.log'
$env:DSOAL_LOGFILE = Join-Path $Out 'dsoal.log'
$line = (Get-Content $cmd -Raw).Trim()
if ($Width -gt 0) { $line = $line -replace '\+\+screenwidth \d+ \+\+screenheight \d+', "++screenwidth $Width ++screenheight $Height" }
if ($ExtraArgs) { $line += ' ' + $ExtraArgs }
$line | Set-Content (Join-Path $Out 'cmdline.txt')
if ($ViaLauncher) {
    # the Play button's way: settings saved, FOV patch applied by the launcher
    $lf = Join-Path $Out 'launch.txt'
    # not -Wait: that would also wait for the game the launcher started
    (Start-Process (Join-Path $Game 'BLOOD2.EXE') -ArgumentList "/launch=`"$lf`"" -PassThru).WaitForExit()
    $script:proc = Get-Process -Id ([int](Get-Content $lf)[0])
} else {
    $script:proc = Start-Process (Join-Path $Game 'Client.exe') -ArgumentList $line -WorkingDirectory $Game -PassThru
}
try {
    Start-Sleep 20
    $mods = & "$env:windir\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -Command "(Get-Process -Id $($proc.Id)).Modules | ForEach-Object FileName"
    $mods | Where-Object { $_ -match '(?i)ddraw|d3dimm|dsound|dsoal|dinput' } | Set-Content (Join-Path $Out 'modules.txt')
    Shot '1_menu'
    # Single Player -> new game -> difficulty: Enter through the menus
    for ($i = 0; $i -lt 5; $i++) { Key 0x1C; Start-Sleep 3 }
    Start-Sleep $CutsceneWait
    Shot '2_level'
    if ($MouseTest) {
        # the same 1000 counts right and back: at 1000 Hz, then at ~66 Hz; A=C=E and B=D when no counts get lost
        Start-Sleep 1; Shot 'm_A_start'
        MouseFast 2 500; Start-Sleep 1; Shot 'm_B_fast_right'
        MouseFast -2 500; Start-Sleep 1; Shot 'm_C_fast_back'
        MouseMove 20 50; Start-Sleep 1; Shot 'm_D_slow_right'
        MouseMove -20 50; Start-Sleep 1; Shot 'm_E_slow_back'
        "alive: $(-not $proc.HasExited)"
        return
    }
    Key 0x11 1500          # W: forward
    Start-Sleep 1
    Shot '3_forward'
    MouseMove 40 30         # turn right
    Start-Sleep 1
    Shot '4_turned'
    Click; Start-Sleep 1; Click
    Start-Sleep 2
    Shot '5_fired'
    Key 0x39               # Space: jump
    Start-Sleep 3
    "alive: $(-not $proc.HasExited)"
}
finally {
    $proc.Refresh()
    if (-not $proc.HasExited) { [void]$proc.CloseMainWindow(); Start-Sleep 6; $proc.Refresh() }
    if (-not $proc.HasExited) { Stop-Process $proc -Force }
    Remove-Item Env:DSOAL_LOGLEVEL, Env:DSOAL_LOGFILE, Env:ALSOFT_LOGLEVEL, Env:ALSOFT_LOGFILE -ErrorAction SilentlyContinue
}
