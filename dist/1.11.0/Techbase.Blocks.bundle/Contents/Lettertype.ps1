# Installs from Microsoft's official Store package; no font binary is redistributed.
$ErrorActionPreference = 'Stop'
$fontFileName = 'ArialNova-Light.ttf'
$fontDirectory = Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Fonts'
$fontTarget = Join-Path $fontDirectory $fontFileName

function Find-TechbaseFont {
    foreach ($path in @($fontTarget, (Join-Path $env:WINDIR ('Fonts\' + $fontFileName)))) {
        if (Test-Path -LiteralPath $path) { return $path }
    }
    $package = Get-AppxPackage -Name Microsoft.ArialNova -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($package) {
        $file = Get-ChildItem -LiteralPath $package.InstallLocation -Filter $fontFileName -Recurse -File | Select-Object -First 1
        if ($file) { return $file.FullName }
    }
    return $null
}

$source = Find-TechbaseFont
if (!$source) {
    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if (!$winget) { throw 'Arial Nova Light ontbreekt. Installeer App Installer van Microsoft en voer Installeren.cmd opnieuw uit.' }
    Write-Host 'Arial Nova Light wordt automatisch via de Microsoft Store geinstalleerd...'
    & $winget.Source install --id 9NS5CT1MZ7M8 --source msstore --exact --silent --accept-source-agreements --accept-package-agreements --disable-interactivity
    $source = Find-TechbaseFont
    if (!$source) { throw 'Microsoft Store kon Arial Nova Light niet installeren. Controleer internet of de Store-toegang en voer Installeren.cmd opnieuw uit.' }
}
New-Item -ItemType Directory -Path $fontDirectory -Force | Out-Null
if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($fontTarget)) {
    Copy-Item -LiteralPath $source -Destination $fontTarget -Force
}
$registry = 'HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts'
New-Item -Path $registry -Force | Out-Null
New-ItemProperty -LiteralPath $registry -Name 'Arial Nova Light (TrueType)' -Value $fontTarget -PropertyType String -Force | Out-Null
if (!('TechbaseFontNative' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TechbaseFontNative {
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern int AddFontResourceEx(string path, uint flags, IntPtr reserved);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wparam, IntPtr lparam, uint flags, uint timeout, out UIntPtr result);
}
'@
}
if ([TechbaseFontNative]::AddFontResourceEx($fontTarget, 0, [IntPtr]::Zero) -eq 0) { throw 'Windows kon Arial Nova Light niet activeren.' }
$broadcastResult = [UIntPtr]::Zero
[TechbaseFontNative]::SendMessageTimeout([IntPtr]0xffff, 0x1d, [UIntPtr]::Zero, [IntPtr]::Zero, 2, 2000, [ref]$broadcastResult) | Out-Null
Write-Host 'Arial Nova Light is beschikbaar voor Techbase.'
