param([switch]$NoLaunch,[switch]$NoStartup)
$ErrorActionPreference='Stop'
$data=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'GridNest'
$install=Join-Path $data '.app'
if(Get-Process GridNest -ErrorAction SilentlyContinue) { throw 'Close GridNest from its tray menu before updating.' }
New-Item -ItemType Directory -Force -Path $data,$install | Out-Null
foreach($file in @('GridNest.exe','app.ico','README.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $install -Force }
$marker=Join-Path $install 'initialized'
if(!(Test-Path -LiteralPath $marker)) {
 foreach($name in @('Работа','AI','Сеть','VR')) { New-Item -ItemType Directory -Path (Join-Path $data $name) -Force | Out-Null }
 Set-Content -LiteralPath $marker -Value '1'
}
$shell=New-Object -ComObject WScript.Shell
$dirs=@([Environment]::GetFolderPath('Programs'))
if(!$NoStartup) { $dirs+=[Environment]::GetFolderPath('Startup') }
foreach($dir in $dirs) {
 $link=$shell.CreateShortcut((Join-Path $dir 'GridNest.lnk'))
 $link.TargetPath=Join-Path $install 'GridNest.exe'; $link.WorkingDirectory=$install
 $link.IconLocation=(Join-Path $install 'app.ico')+',0'; $link.Save()
}
if(!$NoLaunch) { Start-Process -FilePath (Join-Path $install 'GridNest.exe') -WindowStyle Normal }
Write-Output "Installed GridNest: $install"
