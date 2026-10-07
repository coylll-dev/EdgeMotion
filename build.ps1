param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compilerPath)) { throw 'Нужен .NET Framework 4.8 или используйте dotnet build EdgeMotion.csproj.' }
$outputPath = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$sourcePaths = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /langversion:5 "/out:$outputPath\EdgeMotion.exe" "/win32manifest:$PSScriptRoot\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll $sourcePaths
if ($LASTEXITCODE -ne 0) { throw 'Сборка EdgeMotion завершилась ошибкой.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'EdgeMotion.exe.config') -Destination $outputPath
if ($Test) {
    & $compilerPath /nologo /target:exe /warnaserror+ /langversion:5 "/out:$outputPath\EdgeMotion.Tests.exe" /reference:System.Drawing.dll /reference:System.Xml.dll (Join-Path $PSScriptRoot 'src\GestureRecognizer.cs') (Join-Path $PSScriptRoot 'src\Settings.cs') (Join-Path $PSScriptRoot 'tests\GestureTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Сборка тестов завершилась ошибкой.' }
    & (Join-Path $outputPath 'EdgeMotion.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Тесты завершились ошибкой.' }
}
Write-Host "Готово: $outputPath\EdgeMotion.exe"
