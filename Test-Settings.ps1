$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetTempPath()) ('GridNest-settings-tests-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GridNest.exe') -Destination $root
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll') | ForEach-Object { '/reference:'+(Join-Path (Join-Path $framework 'WPF') $_) }
$refs+=('/reference:'+(Join-Path $framework 'System.Xaml.dll'))
$refs+=('/reference:'+(Join-Path $framework 'System.Xml.Linq.dll'))
$refs+=('/reference:'+(Join-Path $root 'GridNest.exe'))
$runner=Join-Path $root 'SettingsRestart.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe ('/out:'+$runner) $refs (Join-Path $PSScriptRoot 'tests\SettingsRestart.cs')
if($LASTEXITCODE -ne 0) {throw 'Settings test compilation failed.'}
& $runner
if($LASTEXITCODE -ne 0) {throw 'Settings recovery tests failed.'}
$restartRoot=Join-Path $root 'restart-data'
& $runner --seed $restartRoot
if($LASTEXITCODE -ne 0) {throw 'Settings seed failed.'}
1..3 | ForEach-Object {
 & $runner --reopen $restartRoot
 if($LASTEXITCODE -ne 0) {throw 'Settings changed across process restart.'}
}
Write-Output 'PASS: settings retained across three process restarts; temporary and damaged-file recovery.'
