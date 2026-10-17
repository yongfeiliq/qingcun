# 轻存

轻存将多平台视频下载、整理并导入 Eagle。本仓库包含 Windows 浏览器扩展、Eagle 插件、本地下载引擎源码、Mac 适配源码与安装器源码，以及一个可在 OpenClaw 中调用本机轻存的桥接插件。

## 组件

- `视频下载助手/extension`：Chrome / Edge 开发者模式扩展。
- `视频下载助手/native-current`：Windows 本地引擎（.NET Framework）。
- `轻存-Eagle插件`：Eagle 界面与入库逻辑。
- `Mac`：Mac 适配与安装器代码。
- `安装包制作`：Windows 安装器代码。
- `openclaw-qingcun`：OpenClaw 插件，通过本机原生消息接口提交下载任务。

发布包请从 GitHub Releases 获取。Windows 2.3.14 为当前 Windows 包；Mac 2.3.15 是通用预览包。Mac 2.3.15 安装包来自另一台 Mac，本机保存的 `Mac` 源码目录仍为较早的构建版，因此**不能用此仓库当前 Mac 源码完整重现 2.3.15 安装包**。发布包内含实际运行组件；请以包内实测范围为准。

源码仓库不包含个人视频、Eagle 资源库、登录资料、密钥、缓存和第三方可执行文件。安装包中第三方组件依照各自许可证提供。

## OpenClaw

参见 [`openclaw-qingcun/README.md`](openclaw-qingcun/README.md)。桥接插件需要与 Eagle 和轻存安装在同一台电脑；它不包含视频下载引擎，亦不会绕过平台访问权限。

## 权利与训练使用

本项目原创内容未经权利人明确许可不得用于二次训练、微调、蒸馏或训练／评测数据集。具体范围与第三方例外见 [`LICENSE.txt`](LICENSE.txt)。公开发布无法从技术上阻止第三方复制或训练；此声明表达权利人的使用条件。

