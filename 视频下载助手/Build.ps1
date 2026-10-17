$ErrorActionPreference='Stop'
$dotnet=(Get-Command dotnet -ErrorAction SilentlyContinue).Source
if(-not $dotnet -or -not @(& $dotnet --list-sdks 2>$null).Count){
  $dotnet=Join-Path (Split-Path $PSScriptRoot -Parent) 'tmp\release-2.3.1\dotnet-sdk\dotnet.exe'
}
if(-not(Test-Path -LiteralPath $dotnet)){throw '需要 .NET 8 SDK 才能编译当前 Windows 引擎。'}
$project=Join-Path $PSScriptRoot 'native-current\QingcunHost.csproj'
& $dotnet build $project -c Release -v:q
if($LASTEXITCODE -ne 0){throw '当前引擎编译失败。'}
$built=Join-Path $PSScriptRoot 'native-current\bin\Release\net48\QingcunHost.exe'
if(-not(Test-Path -LiteralPath $built)){throw '未找到编译后的引擎。'}
Copy-Item -LiteralPath $built -Destination (Join-Path $PSScriptRoot 'native\QingcunHost.exe') -Force
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'extension\manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($icon in $manifest.icons.PSObject.Properties){
  if(-not(Test-Path -LiteralPath (Join-Path (Join-Path $PSScriptRoot 'extension') $icon.Value))){throw '图标文件缺失。'}
}
Write-Host "Windows 引擎 $($manifest.version) 编译完成，扩展资源检查通过。" -ForegroundColor Green
