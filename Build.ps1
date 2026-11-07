$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc=Join-Path $framework 'csc.exe'
$refs=@('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll') | ForEach-Object { '/reference:'+ (Join-Path (Join-Path $framework 'WPF') $_) }
$refs+=('/reference:'+(Join-Path $framework 'System.Xaml.dll'))
$refs+=('/reference:'+(Join-Path $framework 'System.Xml.Linq.dll'))
$refs+=('/reference:'+(Join-Path $framework 'System.Windows.Forms.dll'))
$refs+=('/reference:'+(Join-Path $framework 'System.Drawing.dll'))
& $csc /nologo /target:winexe /platform:anycpu /optimize+ "/win32icon:$PSScriptRoot\app.ico" "/resource:$PSScriptRoot\app.ico,GridNest.AppIcon.ico" "/out:$PSScriptRoot\GridNest.exe" $refs "$PSScriptRoot\GridNest.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }

