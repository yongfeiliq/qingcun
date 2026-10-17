# Mac 通用预览版（2.3.2）

这是 Windows 现有业务引擎的 .NET 8 跨平台移植。Mac/generate-native.cjs 从共享 native 源码生成 Mac/generated，再补入 Compatibility.cs 和 MacInstaller.cs。Windows .NET Framework 构建保持独立。

主要适配：JSON 序列化、当前用户数据目录、文件选择窗口、Finder 打开/定位、进程与互斥锁、FFmpeg 图片预览和带时间戳拼图、模型密钥和 Vimeo 登录资料加密、浏览器 native messaging 注册、Eagle 平台与芯片选择。Mac 密钥存储在用户数据目录，文件权限仅允许当前用户读取。

构建：
1. tmp/release-2.3.1/downloads/lock.json 记录 SDK、yt-dlp、Deno、FFmpeg/FFprobe 的来源与官方发布摘要；下载后先校验，不执行下载来源中的脚本。
2. SDK 解压到 tmp/release-2.3.1/dotnet-sdk；Build-Mac.ps1 分别发布 osx-arm64 和 osx-x64 自带运行时程序，再调用 Mac/package.py。
3. 包含 .app 安装器、两种芯片运行时、Eagle 插件、可选 Chrome/Edge 扩展及中文说明。归档保留 Unix 可执行权限，并包含两层 SHA-256 校验。
4. 不添加私有 data、用户配置、模型密钥、视频或测试入口。系统安全保护不被关闭；包未商业签名、未公证。

本轮结果：
- 两种 Mac 架构编译、Mach-O 架构/依赖核对、Bash 启动脚本语法及压缩包校验通过。
- tests/mac-core.cjs 在 Windows 上运行跨平台核心，验证本地下载流程、类型分析（模拟模型）、图片预览与关闭模型后的下载。
- Mac/InstallerTests.cs 在 Windows 上验证安装路径、覆盖升级、数据保留、失败恢复、仅 Eagle 分支与路径越界防护。
- 不能将这些结果当作 Mac 实机通过。Mac Eagle、Chrome/Edge 通信、系统窗口、Codex 可执行文件发现、字幕烧录与在线下载仍待 Mac 实机验证。

产物：分发安装包/轻存一体安装-v2.3.2-Mac通用-预览版.zip
报告：tmp/release-2.3.1/mac-core-test-report.json、mac-package-report.json、final-package-check.json。

2.3.2 复用已校验的 2.3.1 SDK 与工具缓存；本轮输出与报告位于 tmp/release-2.3.2。包含 Eagle 简介独立网址清理与空行压缩修复，旧版核心测试记录仍保留于 2.3.1 目录。

## 2.3.2 Mac 安装修复 1
旧包在 Windows 生成 payload.sha256 时写入 CRLF，shasum -c 将 CR 当作文件名的一部分，安装会误报损坏。修复为 LF 校验文件，并按指定文件流计算 SHA-256；保留严格内容校验。验证从安装开始写入持久日志，不再随临时目录被删除。新版输出位于 tmp/release-2.3.2-mac-r1 与分发安装包，核心仍为 2.3.2，安装器构建号 2.3.2.1。

## 2.3.2 Mac 安装修复 2
Apple 芯片用户已证实修复 1 的 SHA 校验通过，但原生安装程序退出而没有日志。静态审计确认两个架构的 QingcunHost 均缺失 LC_CODE_SIGNATURE；.NET 8 在 Windows 上生成 apphost 后移除旧签名，默认不会重签。
构建时用已按官方 SHA-256 校验的 rcodesign 0.29.0 为自身 apphost 添加 ad-hoc 签名与 allow-jit entitlement，其他已有签名保留。逐页检查所有 Mach-O 的 CodeDirectory 哈希、特殊槽及 JIT 权限。此签名不是 Developer ID 签名或 Apple 公证。
增加独立 MacBootstrap 入口、阶段日志、原生退出码和系统 codesign --verify 检查。macInstallRevision=2 确保 Eagle 会更新旧的 Mac 组件。新产物为“轻存一体安装-v2.3.2-Mac通用-修复2-预览版.zip”，安装器构建号 2.3.2.2。仍未 Mac 实机验证。

## 2.3.3 分类更新分发
输出为分发安装包/轻存一体安装-v2.3.3-Mac通用-预览版.zip；构建目录 tmp/release-2.3.3。重用已校验的 SDK、下载依赖和 rcodesign 缓存，包含 folders.cjs 自动改名与手机/手表分类、入库附带资料和空目录清理。保留 macInstallRevision=2，并同时检查 categorySchema=1。仍需 Mac 实机验证。

## 2.3.3 Mac 启动修复 1
用户在 macOS 26.2 / M1 反馈：SHA 校验和 codesign 静态验证通过，QingcunHost 在 Bootstrap 前被 SIGKILL（137）。无法从该日志唯一确定系统终止原因。统一切换为 framework-dependent / UseAppHost=false 发布，使用包内微软原始 .NET 8.0.28 runtime/dotnet + QingcunHost.dll。QingcunHost 为保留 stdio、参数与退出码的 POSIX 包装器；官方下载按元数据 SHA-512 校验，运行时原文件不重签、不修改。macInstallRevision=3，构建 2.3.3.1，双架构同一方案，未 Mac 实机验证。输出目录 tmp/release-2.3.3-mac-r1。
