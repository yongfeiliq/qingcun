$currentVersion=(Get-Content -LiteralPath (Join-Path $PSScriptRoot '视频下载助手\extension\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
if($currentVersion -ne '2.3.14'){throw "此安装脚本固定为 2.3.14，不可用于当前源码 $currentVersion；请先更新分发脚本。"}
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$maker=Join-Path $taskRoot '安装包制作'
$output=Join-Path $taskRoot '分发安装包'
$stage=Join-Path $taskRoot ('tmp\unified-build-'+[Guid]::NewGuid().ToString('N'))
$payload=Join-Path $stage 'payload'
New-Item -ItemType Directory -Path $payload,$output -Force | Out-Null
$pluginRoot=Join-Path $taskRoot '轻存-Eagle插件'
$browserRoot=Join-Path $taskRoot '视频下载助手\extension'
$sourceRoot=Join-Path $taskRoot '视频下载助手'
$pluginStage=Join-Path $stage 'eagle'
New-Item -ItemType Directory -Path $pluginStage -Force | Out-Null
# Never take data or configuration from the installed copy.
foreach($name in @('manifest.json','index.html','app.js','native.cjs','sync.cjs','folders.cjs','storage.cjs','presentation.cjs','eagle-adapter.cjs','existing.cjs','existing-ui.js','style.css','logo.png','help.html','使用说明.md')) {
 Copy-Item -LiteralPath (Join-Path $pluginRoot $name) -Destination $pluginStage
}
foreach($folder in @('views','licenses','engine')) {
 if(Test-Path -LiteralPath (Join-Path $pluginRoot $folder)){Copy-Item -LiteralPath (Join-Path $pluginRoot $folder) -Destination $pluginStage -Recurse}
}
$notices=Join-Path $pluginStage 'licenses'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
foreach($name in @('THIRD-PARTY.txt','DENO-LICENSE.txt','GPL-3.0.txt','YT-DLP-LICENSE.txt','ffmpeg-build.txt')) {
 Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination $notices -Force
}
$noticeFile=Join-Path $notices 'THIRD-PARTY.txt'
$notice=[IO.File]::ReadAllText($noticeFile).Replace('The extension and native bridge source code are included.','The browser extension and Eagle plugin source code are included.')
[IO.File]::WriteAllText($noticeFile,$notice,(New-Object Text.UTF8Encoding($false)))

$manual=Join-Path $pluginStage '使用说明.md'
$content=[IO.File]::ReadAllText($manual).Replace('此电脑已绑定「汽车cg参考」。','请在首次打开插件前，先打开准备使用的 Eagle 资源库。')
[IO.File]::WriteAllText($manual,$content,(New-Object Text.UTF8Encoding($false)))
if((Get-FileHash -LiteralPath (Join-Path $sourceRoot 'native\QingcunHost.exe')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $pluginStage 'engine\QingcunHost.exe')).Hash){throw '浏览器和 Eagle 本地组件不一致。'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$eaglePackage=Join-Path $payload '轻存-Eagle-v2.3.14.eagleplugin'
[IO.Compression.ZipFile]::CreateFromDirectory($pluginStage,$eaglePackage,[IO.Compression.CompressionLevel]::Optimal,$false)
Copy-Item -LiteralPath $browserRoot -Destination $payload -Recurse
$manifestFile=Join-Path $payload 'extension\manifest.json'
$m=Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8|ConvertFrom-Json
$m.version='2.3.14'
[IO.File]::WriteAllText($manifestFile,($m|ConvertTo-Json -Depth 20),(New-Object Text.UTF8Encoding($false)))
Copy-Item -LiteralPath (Join-Path $maker '安装说明.html') -Destination $payload
Copy-Item -LiteralPath (Join-Path $maker '实测范围.txt') -Destination $payload
$entries=@(Get-ChildItem -LiteralPath $payload -Recurse -File; Get-ChildItem -LiteralPath $pluginStage -Recurse -File)
$bad=$entries | Where-Object {$_.FullName.Substring($stage.Length) -match '(?i)([\\/](data|tmp|profiles|backups)[\\/]|[\\/](state|settings|auth|cookies)\.json$|\.bin$|\.log$|host-manifest\.json$|\.(mp4|mov|webm|mkv)$)'}
if($bad){throw '安装包出现不应分发的个人数据或运行记录。'}
$zip=Join-Path $stage 'payload.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$hashFile=Join-Path $stage 'payload.sha256'
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()|Set-Content -LiteralPath $hashFile -Encoding ASCII
$exe=Join-Path $output '轻存一体安装-v2.3.14-Windows.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ ('/out:'+$exe) ('/win32manifest:'+(Join-Path $maker 'app.manifest')) ('/resource:'+$zip+',QingcunPayload.zip') ('/resource:'+$hashFile+',QingcunPayload.sha256') /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.dll /reference:System.Web.Extensions.dll (Join-Path $maker 'Installer.cs')
if($LASTEXITCODE-ne 0){throw '安装器编译失败。'}
$hash=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$hash+'  '+[IO.Path]::GetFileName($exe)|Set-Content -LiteralPath ($exe+'.sha256') -Encoding UTF8
$short=@'
轻存 · 一体安装 2.3.14
====================
把“轻存一体安装-v2.3.14-Windows.exe”复制到另一台电脑，双击后点“开始安装”。

1. 先安装 Eagle 4 或更新版本，打开准备使用的资源库。
2. 安装程序会打开 Eagle 插件安装提示，在 Eagle 点“安装”。
3. 浏览器扩展可选：点击“打开 Chrome”或“打开 Edge” → 开发者模式 → 加载已解压的扩展程序。
   文件夹路径会自动复制，粘贴进文件夹选择窗口的地址栏即可。
4. 打开 Eagle 插件“轻存 · 参考片入库”，粘贴链接下载。

适用：64 位 Windows 10 / 11。无需管理员权限，无需另装 Python、Node.js、FFmpeg。
只使用 Eagle 时，可以取消浏览器扩展选项。
首次加载浏览器扩展、在 Eagle 确认安装，需要你亲自点击。

包内不含你的视频、资源库、登录信息或密钥。
智能分析默认关闭，无需配置 Codex。需要时在设置中开启；升级保留原有选择。
如需迁移已有作品，请另外迁移 Eagle 资源库。
本版自动将“轻存参考片”改为“轻存”、“汽车交通”改为“汽车”，保留原分类与素材。
新增手机、手表识别；旧待分类素材在打开轻存后自动检查。升级后请重新打开 Eagle 和轻存。

安装位置：%LOCALAPPDATA%\QingcunVideo
安装完成后可删除 EXE，请保留上述安装目录。
再次安装保留已有视频和设置；升级前请完成任务、关闭预览及 Eagle。
此包为未商业签名的本地安装程序。
请阅读同目录“实测范围.txt”；抖音公开样本在本机返回网站 403，不能保证该平台当前可下载。Mac 预览版未包含在此包内。
'@
[IO.File]::WriteAllText((Join-Path $output '先看这里.txt'),$short,(New-Object Text.UTF8Encoding($true)))
[IO.File]::WriteAllText((Join-Path $stage 'build.json'),(@{exe=$exe;payload=$payload;plugin=$pluginStage;sha256=$hash;files=$entries.Count;version='2.3.14'}|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
Write-Output (@{exe=$exe;stage=$stage;size=(Get-Item -LiteralPath $exe).Length;sha256=$hash}|ConvertTo-Json)

$releaseStage=Join-Path $stage 'release'
New-Item -ItemType Directory -Path $releaseStage -Force | Out-Null
foreach($file in @($exe,($exe+'.sha256'),(Join-Path $output '先看这里.txt'))){Copy-Item -LiteralPath $file -Destination $releaseStage}
Copy-Item -LiteralPath (Join-Path $maker '实测范围.txt') -Destination $releaseStage
$temporaryZip=Join-Path $stage '轻存一体安装-v2.3.14-Windows.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($releaseStage,$temporaryZip,[IO.Compression.CompressionLevel]::Optimal,$false)
$finalZip=Join-Path $output '轻存一体安装-v2.3.14-Windows.zip'
Copy-Item -LiteralPath $temporaryZip -Destination $finalZip -Force
$zipHash=(Get-FileHash -LiteralPath $finalZip -Algorithm SHA256).Hash
$zipHash+'  '+[IO.Path]::GetFileName($finalZip)|Set-Content -LiteralPath ($finalZip+'.sha256') -Encoding UTF8
Write-Output (@{zip=$finalZip;size=(Get-Item -LiteralPath $finalZip).Length;sha256=$zipHash}|ConvertTo-Json)
