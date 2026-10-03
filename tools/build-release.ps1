<#
Fabrique les deux artefacts de release de Tonex Song Advisor :

  - TonexSongAdvisor-<version>-portable.zip : publication autonome, à décompresser et à lancer
    (installation portable, rien n'est écrit ailleurs que dans %APPDATA%\TonexAdvisor).
  - TonexSongAdvisor-<version>-setup.exe    : installeur maison (code du projet, licence MIT),
    installation par utilisateur sans droits administrateur, raccourcis + désinstallation.

Usage :  powershell -ExecutionPolicy Bypass -File tools\build-release.ps1 -Version 0.3
#>
param(
    [string]$Version = "0.3"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
$artifacts = Join-Path $root 'artifacts'
$appPublish = Join-Path $artifacts 'app'
$setupPublish = Join-Path $artifacts 'setup'

foreach ($dir in @($artifacts, $appPublish, $setupPublish)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

Write-Host '1/3 - publication autonome de l''application'
& $dotnet publish (Join-Path $root 'src\TonexAdvisor.App\TonexAdvisor.App.csproj') `
    -c Release -r win-x64 --self-contained -o $appPublish -v minimal
if ($LASTEXITCODE -ne 0) { throw 'publication de l''application en échec' }

Write-Host '2/3 - archive portable'
$portable = Join-Path $artifacts "TonexSongAdvisor-$Version-portable.zip"
if (Test-Path $portable) { Remove-Item $portable -Force }
Compress-Archive -Path (Join-Path $appPublish '*') -DestinationPath $portable -CompressionLevel Optimal

Write-Host '3/3 - installeur (charge utile incorporée)'
$payload = Join-Path $root 'tools\Setup\payload.zip'
Copy-Item $portable $payload -Force
& $dotnet publish (Join-Path $root 'tools\Setup\TonexAdvisor.Setup.csproj') `
    -c Release -r win-x64 --self-contained -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:Version="$($Version).0" -o $setupPublish -v minimal
if ($LASTEXITCODE -ne 0) { Remove-Item $payload -Force; throw 'publication de l''installeur en échec' }
Remove-Item $payload -Force

Copy-Item (Join-Path $setupPublish 'setup.exe') (Join-Path $artifacts "TonexSongAdvisor-$Version-setup.exe") -Force
Remove-Item $appPublish, $setupPublish -Recurse -Force

'--- artefacts ---'
Get-ChildItem $artifacts -File | ForEach-Object { '{0,10:N0} Ko  {1}' -f ($_.Length / 1KB), $_.Name }
$setupExe = Join-Path $artifacts "TonexSongAdvisor-$Version-setup.exe"
'--- version de l''installeur ---'
(Get-Item $setupExe).VersionInfo | ForEach-Object { 'produit: ' + $_.ProductVersion + '  fichier: ' + $_.FileVersion }
