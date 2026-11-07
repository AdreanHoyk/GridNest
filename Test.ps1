$ErrorActionPreference='Stop'
if(Get-Process GridNest -ErrorAction SilentlyContinue) { throw 'Close GridNest before testing.' }
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('GridNest-tests-'+[guid]::NewGuid().ToString('N'))
foreach($name in @('Работа','AI','Сеть')) { New-Item -ItemType Directory -Path (Join-Path $testRoot $name) -Force | Out-Null }
$start=Get-Date
$process=Start-Process -FilePath (Join-Path $PSScriptRoot 'GridNest.exe') -ArgumentList @('--self-test','--data-root',('"'+$testRoot+'"')) -PassThru -WindowStyle Normal
if(!$process.WaitForExit(30000)) { throw "Test timeout. Test data: $testRoot" }
if($process.ExitCode -ne 0) { throw "Test process failed ($($process.ExitCode)). Test data: $testRoot" }
$report=Get-Item (Join-Path $testRoot '.app\self-test.txt')
if($report.LastWriteTime -lt $start) { throw 'Stale test report' }
$text=Get-Content -LiteralPath $report.FullName -Raw
if($text -match 'FAIL' -or $text -notmatch 'color/persist/validation=PASS' -or $text -notmatch 'FEATURES:') { $text; throw 'Tests failed' }
$text
Write-Output "PASS. Isolated test data retained: $testRoot"
