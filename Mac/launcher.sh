#!/bin/bash
set -euo pipefail
export PATH=/usr/bin:/bin:/usr/sbin:/sbin
umask 077
qc_resources="$(cd -- "$(dirname -- "$0")/../Resources" && pwd)"
qc_alert() {
 /usr/bin/osascript -l JavaScript -e 'function run(a){var app=Application.currentApplication();app.includeStandardAdditions=true;app.displayDialog(a[0],{withTitle:"轻存 · Mac 安装",buttons:["知道了"],defaultButton:"知道了"});}' -- "$1" >/dev/null 2>&1 || true
}
if [[ "$(/usr/bin/uname -s)" != Darwin ]]; then exit 1; fi
qc_major="$(/usr/bin/sw_vers -productVersion | /usr/bin/cut -d . -f 1)"
if (( qc_major < 13 )); then qc_alert "此预览包需要 macOS 13 或更新版本。"; exit 1; fi
qc_choice="$(/usr/bin/osascript -l JavaScript -e 'function run(){var app=Application.currentApplication();app.includeStandardAdditions=true;return app.displayDialog("轻存 2.3.3 · Mac 启动修复 1\n\n请先结束下载，退出轻存插件。安装会保留原有设置和素材。\n\n智能分析默认关闭，无需配置 Codex。请选择安装方式：",{withTitle:"安装轻存",buttons:["取消","仅 Eagle","完整安装"],defaultButton:"完整安装",cancelButton:"取消"}).buttonReturned;}')" || exit 0
qc_mode=eagle
if [[ "$qc_choice" == "完整安装" ]]; then qc_mode=browser; fi
qc_arch=x64
if [[ "$(/usr/sbin/sysctl -n hw.optional.arm64 2>/dev/null || true)" == 1 ]]; then qc_arch=arm64; fi
qc_tmp_base="${TMPDIR:-/tmp}"
qc_tmp_base="${qc_tmp_base%/}"
qc_stage="$(/usr/bin/mktemp -d "$qc_tmp_base/qingcun-install.XXXXXX")"
qc_cleanup(){ case "$qc_stage" in "$qc_tmp_base/"qingcun-install.*) /bin/rm -rf -- "$qc_stage";; esac; }
trap qc_cleanup EXIT
qc_logs="$HOME/Library/Logs/Qingcun"
if ! /bin/mkdir -p "$qc_logs"; then qc_alert '无法创建安装日志，请检查当前用户目录的写入权限。'; exit 1; fi
qc_log="$qc_logs/install-$(/bin/date +%Y%m%d-%H%M%S)-$$.log"
printf '%s\n' '轻存 Mac 2.3.3 启动修复 1' "Resources: $qc_resources" "Architecture: $qc_arch" "macOS: $(/usr/bin/sw_vers -productVersion)" > "$qc_log"
cd -- "$qc_resources"
if ! /bin/bash "$qc_resources/verify-payload.sh" "$qc_resources/轻存-Eagle-v2.3.3-Mac.eagleplugin" "$qc_resources/payload.sha256" >> "$qc_log" 2>&1; then
 qc_error="$(/usr/bin/tail -c 1200 "$qc_log")"
 qc_alert "安装文件检查未通过。\n\n$qc_error\n\n请保留此日志以便排查：$qc_log"
 exit 1
fi
if ! /usr/bin/unzip -q "轻存-Eagle-v2.3.3-Mac.eagleplugin" -d "$qc_stage/payload" >> "$qc_log" 2>&1; then qc_alert "安装包解压失败，请保留日志：$qc_log"; exit 1; fi
qc_engine="$qc_stage/payload/engine/$qc_arch"
/bin/chmod +x "$qc_engine/QingcunHost" "$qc_engine/tools/yt-dlp" "$qc_engine/tools/ffmpeg" "$qc_engine/tools/ffprobe" "$qc_engine/tools/deno"
printf '%s\n' '[runtime] 检查微软官方运行时签名' >> "$qc_log"
/bin/chmod +x "$qc_engine/runtime/dotnet"
if ! /usr/bin/codesign --verify --strict "$qc_engine/runtime/dotnet" >> "$qc_log" 2>&1; then
 qc_alert "运行时签名检查失败，请保留安装日志：$qc_log"
 exit 1
fi
/usr/bin/codesign --display --verbose=2 "$qc_engine/runtime/dotnet" >> "$qc_log" 2>&1 || true
printf '%s\n' '[runtime] 检测官方运行时启动' >> "$qc_log"
qc_runtime_exit=0
{ "$qc_engine/runtime/dotnet" --info; } >> "$qc_log" 2>&1 || qc_runtime_exit=$?
printf '[runtime] 运行时退出码：%s\n' "$qc_runtime_exit" >> "$qc_log"
if [[ "$qc_runtime_exit" -ne 0 ]]; then
 qc_alert "Mac 运行时未能启动（退出码：$qc_runtime_exit）。请保留安装日志：$qc_log"
 exit "$qc_runtime_exit"
fi
export COREHOST_TRACE=1
export COREHOST_TRACEFILE="$qc_logs/runtime-$(/bin/date +%Y%m%d-%H%M%S)-$$.log"
printf '%s\n' '[launch] 启动安装组件' >> "$qc_log"
qc_exit=0
{ "$qc_engine/QingcunHost" --mac-install "$qc_stage/payload" "$qc_resources" "$qc_mode"; } >> "$qc_log" 2>&1 || qc_exit=$?
printf '[launch] 安装组件退出码：%s\n' "$qc_exit" >> "$qc_log"
printf '[launch] 运行时日志：%s\n' "$COREHOST_TRACEFILE" >> "$qc_log"
if [[ "$qc_exit" -eq 0 ]]; then
 qc_alert "组件安装并自检完成。\n\n请在 Eagle 中点击“安装”。如果选择了完整安装，再按已打开的说明加载浏览器扩展。\n\n无需配置 Codex，即可开始下载。"
else
 qc_error="$(/usr/bin/tail -c 1600 "$qc_log")"
 qc_alert "安装未完成（退出码：$qc_exit），请保留日志。\n\n$qc_error\n\n日志：$qc_log"
 exit 1
fi
