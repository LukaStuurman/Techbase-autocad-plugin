param([string]$Destination = (Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\Techbase.Blocks.bundle'))
$ErrorActionPreference = 'Stop'
$bundle = Join-Path $PSScriptRoot 'Techbase.Blocks.bundle'
if (!(Test-Path -LiteralPath (Join-Path $bundle 'PackageContents.xml'))) { throw 'Techbase.Blocks.bundle ontbreekt naast dit installatiebestand.' }
$target = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $target) {
    $manifest = Join-Path $target 'PackageContents.xml'
    if (!(Test-Path -LiteralPath $manifest)) { throw "Doelmap bevat geen herkenbare plugin: $target" }
    [xml]$existing = Get-Content -LiteralPath $manifest -Raw
    if ($existing.ApplicationPackage.UpgradeCode -ne '{38F92DB5-FD94-41EB-B73E-31DAC1BD784A}') { throw 'Doelmap behoort aan een andere plugin.' }
    $backup = $target + '.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
    Copy-Item -LiteralPath $target -Destination $backup -Recurse
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -Path (Join-Path $bundle '*') -Destination $target -Recurse -Force
Get-ChildItem -LiteralPath $target -Recurse -File | Unblock-File
# Update only this plugin's existing demand-load registrations. Core Console uses
# these cached entries and does not always re-scan a changed bundle manifest.
[xml]$installed = Get-Content -LiteralPath (Join-Path $target 'PackageContents.xml') -Raw
$registryRoot = 'HKCU:\Software\Autodesk\AutoCAD'
$registrations = @()
if (Test-Path -LiteralPath $registryRoot) {
    foreach ($series in Get-ChildItem -LiteralPath $registryRoot) {
        foreach ($product in Get-ChildItem -LiteralPath $series.PSPath -ErrorAction SilentlyContinue) {
            if ($product.PSChildName -notlike 'ACAD-*') { continue }
            foreach ($component in $installed.ApplicationPackage.Components) {
                $entry = $component.ComponentEntry
                $applicationKey = Join-Path $product.PSPath ('Applications\' + $entry.AppName)
                if (!(Test-Path -LiteralPath $applicationKey)) { continue }
                $properties = Get-ItemProperty -LiteralPath $applicationKey
                if (!$properties.LOADER) { continue }
                $oldLoader = [IO.Path]::GetFullPath([string]$properties.LOADER)
                if (!$oldLoader.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { continue }
                $commandKey = Join-Path $applicationKey 'Commands'
                $previousCommands = @{}
                if (Test-Path -LiteralPath $commandKey) {
                    $commandProperties = Get-ItemProperty -LiteralPath $commandKey
                    foreach ($property in $commandProperties.PSObject.Properties) {
                        if ($property.Name -notlike 'PS*') { $previousCommands[$property.Name] = $property.Value }
                    }
                }
                $registrations += [pscustomobject]@{ Key = $applicationKey; Loader = $properties.LOADER; Commands = $previousCommands }
                $loader = [IO.Path]::GetFullPath((Join-Path $target ([string]$entry.ModuleName)))
                Set-ItemProperty -LiteralPath $applicationKey -Name LOADER -Value $loader
                New-Item -Path $commandKey -Force | Out-Null
                foreach ($command in $entry.Commands.Command) {
                    New-ItemProperty -LiteralPath $commandKey -Name ([string]$command.Global) -Value ([string]$command.Local) -PropertyType String -Force | Out-Null
                }
                # The old name belonged to this verified plugin registration.
                if (@($entry.Commands.Command.Global) -contains 'TECHBASE' -and $previousCommands.ContainsKey('TBLOCKS')) {
                    $commandRegistry = Get-Item -LiteralPath $commandKey
                    $writableCommands = $commandRegistry.OpenSubKey('', $true)
                    try { $writableCommands.DeleteValue('TBLOCKS', $false) }
                    finally { $writableCommands.Dispose(); $commandRegistry.Dispose() }
                }
            }
        }
    }
}
if ($registrations.Count -gt 0) {
    $registrations | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $target ('registratie-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json')) -Encoding UTF8
}
Write-Host "Geïnstalleerd in: $target"
Write-Host 'Start AutoCAD of Civil 3D opnieuw en voer TECHBASE uit.'
