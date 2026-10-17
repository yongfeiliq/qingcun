#!/bin/sh
set -eu
qc_host_root="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if [ ! -x "$qc_host_root/runtime/dotnet" ] || [ ! -f "$qc_host_root/QingcunHost.dll" ]; then
  printf '%s\n' '轻存运行组件不完整，请重新运行 Mac 安装器。' >&2
  exit 127
fi
export DOTNET_ROOT="$qc_host_root/runtime"
export DOTNET_MULTILEVEL_LOOKUP=0
exec "$qc_host_root/runtime/dotnet" "$qc_host_root/QingcunHost.dll" "$@"
