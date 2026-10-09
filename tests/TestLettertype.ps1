# Exercise the real installer with an isolated font directory and simulated Store.
# Never uninstall a user's font or alter their real font registration.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repo ('validation\font-installer-' + [Guid]::NewGuid().ToString('N'))
$originalLocal = $env:LOCALAPPDATA
$originalWindows = $env:WINDIR
$realFont = Join-Path $originalLocal 'Microsoft\Windows\Fonts\ArialNova-Light.ttf'
if (!(Test-Path -LiteralPath $realFont)) { throw 'Run Lettertype.ps1 once before this test.' }
$mockStore = Join-Path $fixture 'Store'
$calls = Join-Path $fixture 'store-calls.txt'
New-Item -ItemType Directory -Path $mockStore -Force | Out-Null
$mockInstaller = Join-Path $fixture 'winget.ps1'
$fakeCode = @'
param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
[IO.File]::WriteAllText($calls, ($Arguments -join ' '))
Copy-Item -LiteralPath $realFont -Destination (Join-Path $mockStore 'ArialNova-Light.ttf')
'@
[IO.File]::WriteAllText($mockInstaller, $fakeCode)
function Get-AppxPackage { param($Name, $ErrorAction)
    if (Test-Path -LiteralPath (Join-Path $mockStore 'ArialNova-Light.ttf')) { [pscustomobject]@{ InstallLocation = $mockStore } }
}
function Get-Command { param($Name, $ErrorAction)
    if ($global:TechbaseFontTestDisableWinget) { return $null }
    [pscustomobject]@{ Source = $mockInstaller }
}
function New-ItemProperty { param($LiteralPath, $Name, $Value, $PropertyType, [switch]$Force)
    if ($Name -ne 'Arial Nova Light (TrueType)' -or !$Value.StartsWith($fixture)) { throw 'Unsafe test registration' }
    $global:TechbaseFontTestRegistration = $Value
}
try {
    $env:LOCALAPPDATA = Join-Path $fixture 'User'
    $env:WINDIR = Join-Path $fixture 'Windows'
    & (Join-Path $repo 'Lettertype.ps1')
    $target = Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Fonts\ArialNova-Light.ttf'
    if (!(Test-Path -LiteralPath $target) -or $global:TechbaseFontTestRegistration -ne $target) { throw 'Missing font was not installed and registered' }
    if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $realFont).Hash) { throw 'Font copy differs' }
    $arguments = [IO.File]::ReadAllText($calls)
    if ($arguments -notmatch '--id 9NS5CT1MZ7M8 --source msstore --exact --silent' -or $arguments -notmatch '--disable-interactivity') { throw 'Incorrect official Store installation' }
    $stamp = (Get-Item -LiteralPath $calls).LastWriteTimeUtc
    & (Join-Path $repo 'Lettertype.ps1')
    if ((Get-Item -LiteralPath $calls).LastWriteTimeUtc -ne $stamp) { throw 'Installed font was downloaded again' }
    $global:TechbaseFontTestDisableWinget = $true
    $env:LOCALAPPDATA = Join-Path $fixture 'OtherUser'
    $mockStore = Join-Path $fixture 'MissingStore'
    $failed = $false
    try { & (Join-Path $repo 'Lettertype.ps1') } catch { $failed = $_.Exception.Message -match 'App Installer' }
    if (!$failed) { throw 'Missing installer dependency was not reported' }
    [pscustomobject]@{ Status = 'PASS'; Checks = 6; StoreDownload = 'Simulated'; WindowsFontActivation = 'Actual' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repo 'validation\font-installer.json') -Encoding UTF8
    Write-Host 'FONT INSTALLER PASS: 6 checks'
}
finally {
    $env:LOCALAPPDATA = $originalLocal; $env:WINDIR = $originalWindows
    Remove-Variable TechbaseFontTestRegistration,TechbaseFontTestDisableWinget -Scope Global -ErrorAction SilentlyContinue
}
