param(
    [string]$Dotnet = 'dotnet',
    [string]$PaletteRoot = ''
)
$ErrorActionPreference = 'Stop'
$geulAssets = Join-Path $PSScriptRoot 'Assets\Kabelgeul'
$geulVersion = Get-Content -LiteralPath (Join-Path $geulAssets 'bronversie.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $geulVersion.Source)).Hash -ne $geulVersion.SourceSHA256 -or
    (Get-FileHash -LiteralPath (Join-Path $geulAssets 'Techbase.Kabelgeul.lsp')).Hash -ne $geulVersion.GeneratedSHA256) {
    throw 'Kabelgeul-bron gewijzigd. Genereer de module opnieuw met tests/prepare_kabelgeul.py en voer de geulcontroles uit.'
}
if ([string]::IsNullOrWhiteSpace($PaletteRoot)) {
    $PaletteRoot = Join-Path $PSScriptRoot 'Assets\Palette'
    if (!(Test-Path -LiteralPath (Join-Path $PaletteRoot 'Techbase toolpallete.xtp'))) {
        $PaletteRoot = Join-Path $env:USERPROFILE 'Techbase\Techbase - Documenten\Tekenkamer\Techbase WES\7. Bestekken\Tekenwijze Techbase'
    }
}
[xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PackageContents.xml') -Raw
$version = [string]$manifest.ApplicationPackage.AppVersion
$dist = Join-Path $PSScriptRoot "dist\$version"
$bundle = Join-Path $dist 'Techbase.Blocks.bundle'
$contents = Join-Path $bundle 'Contents'
$assets = Join-Path $contents 'Assets'
& (Join-Path $PSScriptRoot 'PrepareEnexis.ps1')
New-Item -ItemType Directory -Path (Join-Path $assets 'Library') -Force | Out-Null
foreach ($series in @('R25','R26')) {
    & $Dotnet build (Join-Path $PSScriptRoot 'src\Techbase.Blocks.csproj') -c Release "-p:ApiSeries=$series" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build $series mislukt." }
    $framework = if ($series -eq 'R25') { 'net8.0-windows' } else { 'net10.0-windows' }
    $source = Join-Path $PSScriptRoot "src\bin\$series\Release\$framework"
    $target = Join-Path $contents "$series-$version"
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source 'Techbase.Blocks.dll') -Destination $target -Force
    Copy-Item -LiteralPath (Join-Path $source 'Techbase.Blocks.pdb') -Destination $target -Force
    $workerProject = Join-Path $PSScriptRoot 'vendor\AutoCAD-Enexis-kabels-checker\src\Enexis.KabelChecker.ExcelWorker\Enexis.KabelChecker.ExcelWorker.csproj'
    & $Dotnet build $workerProject -c Release -f $framework --nologo
    if ($LASTEXITCODE -ne 0) { throw "Excel-worker $series mislukt." }
    $workerOutput = Join-Path (Split-Path -Parent $workerProject) "bin\Release\$framework"
    $excel = Join-Path $target 'excel'
    New-Item -ItemType Directory -Path $excel -Force | Out-Null
    Copy-Item -Path (Join-Path $workerOutput '*.dll') -Destination $excel -Force
}
$xtp = Join-Path $PaletteRoot 'Techbase toolpallete.xtp'
Copy-Item -LiteralPath $xtp -Destination $assets -Force
[xml]$xml = Get-Content -LiteralPath $xtp -Raw
$dependent = [string]$xml.ToolPaletteExport.DependentFilesPath
Copy-Item -LiteralPath (Join-Path $PaletteRoot $dependent) -Destination $assets -Recurse -Force
$sources = $xml.SelectNodes('//Palette/Tools/Tool/Data/Block/SourceFile') | ForEach-Object { $_.InnerText.Replace('%UserProfileFolder%', $env:USERPROFILE) } | Select-Object -Unique
$hashes = @()
foreach ($source in $sources) {
    $packagedSource = Join-Path (Join-Path $PaletteRoot 'Library') ([IO.Path]::GetFileName($source))
    if (Test-Path -LiteralPath $packagedSource) { $source = $packagedSource }
    if (!(Test-Path -LiteralPath $source)) { throw "Bron ontbreekt: $source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $assets 'Library') -Force
    $snapshot = Join-Path (Join-Path $assets 'Library') ([IO.Path]::GetFileName($source))
    $hashes += [pscustomobject]@{ File = [IO.Path]::GetFileName($source); SHA256 = (Get-FileHash -LiteralPath $snapshot -Algorithm SHA256).Hash }
}
$hashes += [pscustomobject]@{ File = 'Techbase toolpallete.xtp'; SHA256 = (Get-FileHash -LiteralPath $xtp -Algorithm SHA256).Hash }
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assets 'bronversies.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'enexis-source.json') -Destination $assets -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets\Kabelgeul') -Destination $assets -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PackageContents.xml') -Destination $bundle -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Lettertype.ps1') -Destination $contents -Force
foreach ($file in @('Installeren.ps1','Installeren.cmd','Lettertype.ps1','LEESMIJ.md','VALIDATIE.md')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $dist -Force }
Compress-Archive -LiteralPath $bundle,(Join-Path $dist 'Installeren.ps1'),(Join-Path $dist 'Installeren.cmd'),(Join-Path $dist 'Lettertype.ps1'),(Join-Path $dist 'LEESMIJ.md'),(Join-Path $dist 'VALIDATIE.md') -DestinationPath (Join-Path $PSScriptRoot "Techbase Blocks $version.zip") -Force
Write-Host "Pakket: $dist"
