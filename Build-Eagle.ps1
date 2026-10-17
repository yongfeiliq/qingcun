$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot '视频下载助手\Build.ps1')
$eagleSource=Join-Path $PSScriptRoot '轻存-Eagle插件'
$eagleEngine=Join-Path $eagleSource 'engine'
[IO.Directory]::CreateDirectory($eagleEngine)|Out-Null
foreach($name in @('QingcunHost.exe','extension-id.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('视频下载助手\native\'+$name)) -Destination (Join-Path $eagleEngine $name) -Force}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '视频下载助手\native\tools') -Destination $eagleEngine -Recurse -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$version=(Get-Content -LiteralPath (Join-Path $eagleSource 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
$eaglePackage=Join-Path $PSScriptRoot ("轻存-Eagle-v$version.eagleplugin")
$eagleTemporary=Join-Path $PSScriptRoot ('tmp\eagle-package-'+[Guid]::NewGuid().ToString('N')+'.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($eagleSource,$eagleTemporary,[IO.Compression.CompressionLevel]::Optimal,$false)
$eagleZip=[IO.Compression.ZipFile]::OpenRead($eagleTemporary)
try{
 if(-not($eagleZip.Entries.FullName -contains 'manifest.json')){throw '安装包缺少 manifest.json'}
 $private=$eagleZip.Entries|Where-Object{$_.FullName -match '(^|/)(data|tmp|backups|state\.json|settings\.json|.*\.bin)(/|$)'}
 if($private){throw '安装包出现私有数据路径。'}
}finally{$eagleZip.Dispose()}
Copy-Item -LiteralPath $eagleTemporary -Destination $eaglePackage -Force
(Get-FileHash -LiteralPath $eaglePackage -Algorithm SHA256).Hash|Set-Content -LiteralPath ($eaglePackage+'.sha256') -Encoding ASCII
Write-Host ('Eagle 插件安装包已生成：'+$eaglePackage)
