# 从源码构建

本仓库提供项目源码，公开发布的安装包在 GitHub Releases 单独提供。仓库不包含下载引擎的第三方可执行文件、用户资料或构建缓存。

Windows 本地引擎使用 `视频下载助手/native-current/QingcunHost.csproj`，在 Windows 上用 .NET Framework 4.8 开发工具编译。浏览器扩展可从 `视频下载助手/extension` 以 Chrome / Edge 的开发者模式加载。Eagle 插件源文件位于 `轻存-Eagle插件`，构建脚本为根目录的 `Build-Eagle.ps1`。一体安装包还需要单独取得并核验 yt-dlp、FFmpeg、Deno 等第三方依赖。

`Mac` 目录保存较早版本的跨平台移植源码和安装器构建过程。当前发布的 Mac 2.3.15 预览包由另一台 Mac 制作，本仓库目前不具备该包的完整对应源码及构建依赖，因此不能从这个目录精确复现 Mac 2.3.15。请勿把较早的 `Build-Mac.ps1` 输出冒充 2.3.15。

OpenClaw 桥接插件在 `openclaw-qingcun`。运行 `node --test openclaw-qingcun/test/*.test.mjs` 验证工具逻辑；它依赖本机另外安装的轻存引擎。发布前也须在目标系统检查 Eagle、浏览器扩展及平台访问权限。

