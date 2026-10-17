#!/bin/bash
# Build isolated managed components on the Mac doing the investigation. Does not install.
set -euo pipefail
qc_root="$(cd -- "$(dirname -- "$0")/.." && pwd)"
cd -- "$qc_root"
if [[ "$(uname -s)" != Darwin ]]; then printf '%s\n' '此脚本只在 Mac 上编译；不会安装组件。' >&2; exit 2; fi
qc_dotnet="${QINGCUN_DOTNET:-$(command -v dotnet || true)}"
if [[ -z "$qc_dotnet" || ! -x "$qc_dotnet" ]]; then printf '%s\n' '未找到 .NET SDK。请让 Codex 检查并准备微软官方 .NET 8 SDK，然后重试。' >&2; exit 2; fi
command -v node >/dev/null || { printf '%s\n' '需要 Node.js 来同步共享源码。' >&2; exit 2; }
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
node Mac/generate-native.cjs
for qc_arch in arm64 x64; do
 "$qc_dotnet" publish Mac/QingcunHost.csproj -c Release -r "osx-$qc_arch" --self-contained false -p:UseAppHost=false -p:PlatformTarget=AnyCPU -p:RuntimeFrameworkVersion=8.0.28 -o "tmp/mac-local/mac-$qc_arch" --nologo -v quiet
done
printf '%s\n' '编译完成：tmp/mac-local/mac-arm64 和 mac-x64。尚未安装、尚未生成分发包。'
