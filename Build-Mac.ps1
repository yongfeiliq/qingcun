$currentVersion=(Get-Content -LiteralPath (Join-Path $PSScriptRoot '视频下载助手\extension\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
if($currentVersion -ne '2.3.3'){throw "此 Mac 构建脚本固定为 2.3.3，不可用于当前源码 $currentVersion；请先完成 Mac 适配。"}
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$root=$PSScriptRoot
$sdk=Join-Path $root 'tmp/release-2.3.1/dotnet-sdk/dotnet.exe'
if(-not(Test-Path -LiteralPath $sdk)){throw '先准备并校验 Mac 构建依赖（详见 Mac/README.md）。'}
node (Join-Path $root 'Mac/generate-native.cjs')
if($LASTEXITCODE-ne 0){throw 'Mac 代码生成失败'}
foreach($target in @('arm64','x64')){
 & $sdk publish (Join-Path $root 'Mac/QingcunHost.csproj') -c Release -r ('osx-'+$target) --self-contained false -p:UseAppHost=false -p:PlatformTarget=AnyCPU -p:RuntimeFrameworkVersion=8.0.28 -o (Join-Path $root ('tmp/release-2.3.3-mac-r1/mac-'+$target)) --nologo -v quiet
 if($LASTEXITCODE-ne 0){throw ('Mac '+$target+' 编译失败')}
}
$python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $python (Join-Path $root 'Mac/package.py')
if($LASTEXITCODE-ne 0){throw 'Mac 打包校验失败'}
