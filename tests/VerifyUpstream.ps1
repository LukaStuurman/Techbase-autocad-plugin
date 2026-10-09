param([string]$Dotnet, [string]$Runtime8 = 'C:\Program Files\dotnet\dotnet.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$vendor = Join-Path $root 'vendor\AutoCAD-Enexis-kabels-checker'
$output = Join-Path $root 'validation\Kabelchecker\upstream'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$results = @()
foreach ($framework in @('net8.0-windows','net10.0-windows')) {
    $runtime = if ($framework.StartsWith('net8')) { $Runtime8 } else { $Dotnet }
    foreach ($tool in @('VerifyExcelCases','VerifyKaderTemplates','VerifyExcelIsolation')) {
        $project = Join-Path $vendor "tools\$tool\$tool.csproj"
        # The core-only reference runner targets net8.0; use its configured target.
        $toolFramework = if ($tool -eq 'VerifyExcelCases') { 'net8.0' } else { $framework }
        if ($tool -eq 'VerifyExcelCases' -and !$framework.StartsWith('net8')) { continue }
        $log = Join-Path $output "$tool-$toolFramework.log"
        & $Dotnet build $project -c Release -f $toolFramework --nologo *> $log
        if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $log -Tail 12; throw "Build $tool $toolFramework mislukt." }
        $assembly = Join-Path (Split-Path -Parent $project) "bin\Release\$toolFramework\$tool.dll"
        $arguments = @($assembly)
        if ($tool -eq 'VerifyKaderTemplates') { $arguments += Join-Path $vendor 'src\Enexis.KabelChecker.AutoCAD\Resources' }
        if ($tool -eq 'VerifyExcelIsolation') { $arguments += Join-Path $vendor "src\Enexis.KabelChecker.ExcelWorker\bin\Release\$framework\Enexis.KabelChecker.ExcelWorker.dll" }
        & $runtime @arguments *>> $log
        if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $log -Tail 12; throw "Controle $tool $toolFramework mislukt." }
        $results += [pscustomobject]@{ Tool = $tool; Runtime = $toolFramework; Status = 'PASS' }
        Write-Host "PASS: $tool $toolFramework"
    }
}
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'checks.json') -Encoding UTF8
