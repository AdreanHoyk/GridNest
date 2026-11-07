$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$zip=Join-Path $PSScriptRoot 'GridNest.zip'
Compress-Archive -LiteralPath (Join-Path $PSScriptRoot 'GridNest.exe'),(Join-Path $PSScriptRoot 'app.ico'),(Join-Path $PSScriptRoot 'Install.ps1'),(Join-Path $PSScriptRoot 'README.txt'),(Join-Path $PSScriptRoot 'LICENSE.txt') -DestinationPath $zip -Force
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object {'/reference:'+(Join-Path $framework $_)}
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /optimize+ "/win32manifest:$PSScriptRoot\Installer.manifest" "/win32icon:$PSScriptRoot\app.ico" "/resource:$PSScriptRoot\app.ico,GridNest.Setup.ico" "/resource:$zip,GridNest.Payload.zip" "/out:$PSScriptRoot\GridNest-Setup.exe" $refs (Join-Path $PSScriptRoot 'Installer.cs')
if($LASTEXITCODE -ne 0) {throw 'Installer compilation failed.'}
