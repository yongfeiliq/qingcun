#!/bin/bash
set -euo pipefail
qc_payload="${1:-}"
qc_manifest="${2:-}"
if [[ ! -f "$qc_payload" ]]; then printf '%s\n' '安装包中的插件文件不存在。' >&2; exit 2; fi
if [[ ! -f "$qc_manifest" ]]; then printf '%s\n' '安装包缺少校验文件。' >&2; exit 2; fi
qc_expected=''
qc_remainder=''
IFS=' ' read -r qc_expected qc_remainder < "$qc_manifest" || true
qc_expected="${qc_expected%$'\r'}"
if [[ ! "$qc_expected" =~ ^[0-9a-fA-F]{64}$ ]]; then printf '%s\n' '安装包校验记录格式不正确。' >&2; exit 3; fi
qc_expected="$(printf '%s' "$qc_expected" | tr 'A-F' 'a-f')"
# Hash the known payload through stdin; do not interpret a filename from the
# checksum record. Keep the full SHA-256 comparison for every installation.
if ! qc_result="$(shasum -a 256 -b < "$qc_payload")"; then
 printf '%s\n' '无法运行 SHA-256 校验程序。' >&2; exit 4
fi
qc_actual="${qc_result%% *}"
if [[ ! "$qc_actual" =~ ^[0-9a-f]{64}$ ]]; then printf '%s\n' '校验程序返回了无效结果。' >&2; exit 4; fi
printf 'Expected SHA-256: %s\nActual SHA-256: %s\n' "$qc_expected" "$qc_actual"
if [[ "$qc_actual" != "$qc_expected" ]]; then printf '%s\n' '插件文件内容与校验记录不一致。' >&2; exit 5; fi
printf '%s\n' '安装包 SHA-256 校验通过。'
