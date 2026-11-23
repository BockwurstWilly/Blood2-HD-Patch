# End-to-end test of Blood2HD_Setup.exe against a pristine copy of the GOG game folder.
#   e2e.ps1 -GameCopy <folder with an unpatched GOG install>
# Install -> verify -> update (settings must survive, stale files go) -> uninstall -> byte-compare with the
# start state. A foreign DDraw.dll (another wrapper) sits in the folder before and must come back unchanged.
# Then the update path from the last published version (out\release\Blood2HD_Patch_<old>\Blood2HD_Setup.exe).
# GOG-like compatibility entries are set for the copy first and must come back after the uninstall.
param([Parameter(Mandatory)] [string]$GameCopy, [string]$OldVersion = '1.2.2')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$setup = Join-Path $root 'out\Blood2HD_Setup.exe'
$log = Join-Path $env:TEMP 'Blood2HD_Setup.log'
$layers = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
$gogClient = 'WINXPSP2 DISABLETHEMES DISABLEDWM RUNASADMIN HIGHDPIAWARE'
$gogLauncher = '~WINXPSP2 DISABLETHEMES DISABLEDWM RUNASADMIN HIGHDPIAWARE'
$script:fails = 0
function Check($ok, $what) { if ($ok) { "PASS  $what" } else { $script:fails++; "FAIL  $what" } }
function Snap($dir) {
    # autoexec.cfg holds the player's settings: the patch edits it (keys) and leaves it on uninstall
    Get-ChildItem $dir -Recurse -File | Where-Object { $_.Name -ne 'autoexec.cfg' } | Sort-Object FullName |
        ForEach-Object { '{0}|{1}' -f $_.FullName.Substring($dir.Length), (Get-FileHash $_.FullName -Algorithm SHA1).Hash }
}
function Layer($exe) {
    $p = Get-ItemProperty $layers -ErrorAction SilentlyContinue
    if (-not $p) { return $null }
    $n = $p.PSObject.Properties | Where-Object { $_.Name -ieq (Join-Path $GameCopy $exe) } | Select-Object -First 1
    if ($n) { $n.Value } else { $null }
}
function Setup([string[]]$a, [string]$exe = $setup) {
    $p = Start-Process $exe -ArgumentList $a -Wait -PassThru
    # an uninstall started from the game folder continues in a temporary copy
    Start-Sleep -Milliseconds 500
    while (Get-Process Blood2HD_Uninstall -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 300 }
    $p.ExitCode
}

New-ItemProperty $layers -Name (Join-Path $GameCopy 'CLIENT.EXE') -Value $gogClient -PropertyType String -Force | Out-Null
New-ItemProperty $layers -Name (Join-Path $GameCopy 'blood2.exe') -Value $gogLauncher -PropertyType String -Force | Out-Null
$foreign = Join-Path $GameCopy 'DDraw.dll'
try {
    # another wrapper's DDraw.dll: the patch must keep it and put it back
    [IO.File]::WriteAllText($foreign, 'foreign wrapper, not part of the GOG release')
    $foreignHash = (Get-FileHash $foreign).Hash
    $before = Snap $GameCopy
    $cfgBefore = Get-Content "$GameCopy\autoexec.cfg"

    "== install"
    $rc = Setup @('/install', '/silent', "/dir=`"$GameCopy`"", '/nodesktop', '/nostartmenu')
    Check ($rc -eq 0) "setup exit code 0 ($rc)"
    Get-Content $log -Encoding UTF8 | Select-String '^!!' | ForEach-Object { "      log: $_" }
    foreach ($f in Get-ChildItem "$root\payload\game" -File) {
        if ($f.Name -in 'dgVoodoo.conf', 'alsoft.ini') { continue }   # rewritten by the settings step
        Check ((Get-FileHash $f.FullName).Hash -eq (Get-FileHash "$GameCopy\$($f.Name)").Hash) "installed $($f.Name)"
    }
    Check ((Get-FileHash "$root\out\obj\BLOOD2.EXE").Hash -eq (Get-FileHash "$GameCopy\BLOOD2.EXE").Hash) 'launcher replaced'
    Check ((Get-FileHash "$GameCopy\_HDPatch_Backup\BLOOD2.EXE" -Algorithm SHA1).Hash -eq 'E6071893809B153DF51F226C0A0822CC0A814124') 'original launcher backed up'
    Check ((Layer 'Client.exe') -eq '~ HIGHDPIAWARE') "Client.exe compatibility: $(Layer 'Client.exe')"
    Check ($null -eq (Layer 'BLOOD2.EXE')) 'BLOOD2.EXE: no admin start'
    Check (-not (Test-Path "$GameCopy\Monolith.smk") -and (Test-Path "$GameCopy\_HDPatch_Backup\movies\Monolith.smk")) 'logo videos moved aside'
    Check (Test-Path "$GameCopy\Intro.smk") 'story intro stays'
    $conf = Get-Content "$GameCopy\dgVoodoo.conf"
    Check (($conf -match '^FPSLimit\s*=\s*60$') -and ($conf -match '^Antialiasing\s*=\s*8x$') -and ($conf -match '^Filtering\s*=\s*16$')) 'dgVoodoo.conf: 60 fps, 8x MSAA, 16x AF'
    Check ($conf -match '^FullscreenAttributes\s*=\s*fake$') 'dgVoodoo.conf: borderless fake fullscreen'
    Check ((Select-String "$GameCopy\BloodHD.ini" -Pattern '^Fov=90$' -Quiet)) 'BloodHD.ini: field of view 90'
    Check ((Select-String "$GameCopy\autoexec.cfg" -Pattern 'rangebind "##keyboard" "##17" .* "Forward"' -Quiet)) 'modern keys: W = Forward'
    $actions = @($cfgBefore | Where-Object { $_ -match '^AddAction' })
    Check ((@(Get-Content "$GameCopy\autoexec.cfg" | Where-Object { $_ -match '^AddAction' })).Count -eq $actions.Count) 'autoexec.cfg: actions untouched'
    Check (Test-Path "$GameCopy\_HDPatch\Blood2HD_Setup.exe") 'uninstaller copy'
    Check (Test-Path "$GameCopy\_HDPatch\README.txt") 'readme'
    Check (Test-Path "$GameCopy\Blood II - HD Launcher.lnk") 'shortcut in game folder'
    $k = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Blood2HDPatch' -ErrorAction SilentlyContinue
    Check ($k -and $k.InstallLocation -eq $GameCopy) 'Apps & Features entry'

    "== launcher"
    $cmd = Join-Path $env:TEMP 'blood2hd_e2e_cmd.txt'
    Start-Process "$GameCopy\BLOOD2.EXE" -ArgumentList "/cmdline=`"$cmd`"" -Wait
    $line = Get-Content $cmd -Raw
    Check ($line -match '-rez SOUND\.REZ -rez BLOOD2\.REZ -rez custom -rez BLOOD2P\.REZ -rez BLOOD2P2\.REZ -rez BLOOD2NM\.REZ') 'command line: archive order'
    Check ($line -match '\+\+3dsoundprovider 7 \+\+3dsoundprovidername "Microsoft DirectSound3D with Creative Labs EAX\(TM\)"') 'command line: EAX by name'
    $snap = Join-Path $env:TEMP 'blood2hd_e2e_snap'
    if (Test-Path $snap) { Remove-Item $snap -Recurse -Force }
    Start-Process "$GameCopy\BLOOD2.EXE" -ArgumentList "/snapshot=$snap" -Wait
    Check ((Get-ChildItem $snap -Filter 'launcher_*.png').Count -eq 6) 'launcher renders all pages from the installed copy'
    $rc = (Start-Process "$GameCopy\BLOOD2.EXE" -ArgumentList '/keys=original' -Wait -PassThru).ExitCode
    Check ($rc -eq 0 -and (Select-String "$GameCopy\autoexec.cfg" -Pattern 'rangebind "##keyboard" "##200" .* "Forward"' -Quiet)) 'original layout: Up = Forward'

    Check ((Get-FileHash "$GameCopy\_HDPatch_Backup\DDraw.dll").Hash -eq $foreignHash) 'foreign DDraw.dll backed up before overwriting'
    Check ((Get-Content "$GameCopy\_HDPatch\files.txt") -contains 'DDraw.dll') 'files.txt lists the installed files'

    "== update keeps settings"
    # a file an older version brought and this one no longer does
    [IO.File]::WriteAllText("$GameCopy\stale_old_version.dll", 'x')
    Add-Content "$GameCopy\_HDPatch\files.txt" 'stale_old_version.dll'
    (Get-Content "$GameCopy\dgVoodoo.conf") -replace '^(Antialiasing\s*=\s*)8x$', '${1}4x' | Set-Content "$GameCopy\dgVoodoo.conf"
    (Get-Content "$GameCopy\BloodHD.ini") -replace '^Msaa=8$', 'Msaa=4' -replace '^Provider=eax$', 'Provider=ds3d' | Set-Content "$GameCopy\BloodHD.ini"
    # the player switched the input wrapper off: the launcher parks the dll in _HDPatch
    Move-Item "$GameCopy\dinput.dll" "$GameCopy\_HDPatch\dinputto8.dll"
    $rc = Setup @('/install', '/silent', "/dir=`"$GameCopy`"", '/nodesktop', '/nostartmenu')
    Check ($rc -eq 0) "update exit code 0 ($rc)"
    Check (-not (Test-Path "$GameCopy\stale_old_version.dll")) 'update removes files of older versions'
    Check (-not (Test-Path "$GameCopy\dinput.dll") -and (Test-Path "$GameCopy\_HDPatch\dinputto8.dll")) 'input wrapper stays off after update'
    Check ((Select-String "$GameCopy\dgVoodoo.conf" -Pattern '^Antialiasing\s*=\s*4x$' -Quiet)) 'dgVoodoo.conf kept'
    Check ((Select-String "$GameCopy\BloodHD.ini" -Pattern '^Provider=ds3d$' -Quiet)) 'BloodHD.ini kept'
    Check ((Get-FileHash "$GameCopy\_HDPatch_Backup\BLOOD2.EXE" -Algorithm SHA1).Hash -eq 'E6071893809B153DF51F226C0A0822CC0A814124') 'backup still holds the original launcher'

    "== uninstall (Apps & Features path: the copy in _HDPatch)"
    $p = Start-Process "$GameCopy\_HDPatch\Blood2HD_Setup.exe" -ArgumentList '/uninstall', '/silent' -Wait -PassThru
    Start-Sleep -Milliseconds 500
    while (Get-Process Blood2HD_Uninstall -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 300 }
    Get-Content $log -Encoding UTF8 | Select-String '^!!' | ForEach-Object { "      log: $_" }
    $after = Snap $GameCopy
    $diff = Compare-Object $before $after | ForEach-Object { '{0} {1}' -f $_.SideIndicator, $_.InputObject }
    Check (@($diff).Count -eq 0) 'game folder byte-identical to the start (except autoexec.cfg)'
    $diff | ForEach-Object { "      $_" }
    Check ((Layer 'Client.exe') -eq $gogClient) 'GOG compatibility entry of Client.exe restored'
    Check ((Layer 'BLOOD2.EXE') -eq $gogLauncher) 'GOG compatibility entry of BLOOD2.EXE restored'
    Check (-not (Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Blood2HDPatch')) 'Apps & Features entry removed'
    Start-Sleep 4
    Check (-not (Test-Path (Join-Path $env:TEMP 'Blood2HD_Uninstall.exe'))) 'temporary uninstaller removed itself'
    Check ((Get-FileHash $foreign).Hash -eq $foreignHash) 'foreign DDraw.dll restored unchanged'

    "== update from the published $OldVersion"
    $old = Join-Path $root "out\release\Blood2HD_Patch_$OldVersion\Blood2HD_Setup.exe"
    if (-not (Test-Path $old)) {
        Check $false "published setup $OldVersion not found ($old): gh release download v$OldVersion, unzip into out\release"
    }
    else {
        Remove-Item $foreign   # versions up to 1.2.2 overwrote a foreign DDraw.dll without a backup
        $before2 = Snap $GameCopy
        $rc = Setup @('/install', '/silent', "/dir=`"$GameCopy`"", '/nodesktop', '/nostartmenu') $old
        Check ($rc -eq 0) "install $OldVersion exit code 0 ($rc)"
        (Get-Content "$GameCopy\BloodHD.ini") -replace '^Msaa=8$', 'Msaa=2' | Set-Content "$GameCopy\BloodHD.ini"
        (Get-Content "$GameCopy\dgVoodoo.conf") -replace '^(Antialiasing\s*=\s*)8x$', '${1}2x' | Set-Content "$GameCopy\dgVoodoo.conf"
        $rc = Setup @('/install', '/silent', "/dir=`"$GameCopy`"", '/nodesktop', '/nostartmenu')
        Check ($rc -eq 0) "update to the new version exit code 0 ($rc)"
        Check ((Get-Item "$GameCopy\BLOOD2.EXE").VersionInfo.ProductVersion -ne $OldVersion) 'new launcher in place'
        Check ((Select-String "$GameCopy\BloodHD.ini" -Pattern '^Msaa=2$' -Quiet) -and (Select-String "$GameCopy\dgVoodoo.conf" -Pattern '^Antialiasing\s*=\s*2x$' -Quiet)) 'settings of the old version kept'
        Check (Test-Path "$GameCopy\_HDPatch\files.txt") 'files.txt written on the update'
        Check ((Get-FileHash "$GameCopy\_HDPatch_Backup\BLOOD2.EXE" -Algorithm SHA1).Hash -eq 'E6071893809B153DF51F226C0A0822CC0A814124') 'GOG launcher still in the backup'
        $p = Start-Process "$GameCopy\_HDPatch\Blood2HD_Setup.exe" -ArgumentList '/uninstall', '/silent' -Wait -PassThru
        Start-Sleep -Milliseconds 500
        while (Get-Process Blood2HD_Uninstall -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 300 }
        $diff2 = Compare-Object $before2 (Snap $GameCopy) | ForEach-Object { '{0} {1}' -f $_.SideIndicator, $_.InputObject }
        Check (@($diff2).Count -eq 0) 'after old -> new -> uninstall: game folder byte-identical'
        $diff2 | ForEach-Object { "      $_" }
        Start-Sleep 4
    }
}
finally {
    if (Test-Path $foreign) { if ((Get-FileHash $foreign).Hash -eq $foreignHash) { Remove-Item $foreign } }
    foreach ($exe in 'CLIENT.EXE', 'blood2.exe', 'Client.exe', 'BLOOD2.EXE') {
        Remove-ItemProperty $layers -Name (Join-Path $GameCopy $exe) -ErrorAction SilentlyContinue
    }
    "(test compatibility entries removed)"
}
"== $(if ($script:fails) { "$($script:fails) FAILED" } else { 'ALL PASSED' })"
exit $script:fails
