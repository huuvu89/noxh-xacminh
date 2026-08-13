#!/usr/bin/env sh
# Công cụ là WebAssembly: trình duyệt tải phần chạy bằng fetch, mà fetch trên file:// bị chặn — nên
# bản offline vẫn cần một máy chủ file, chạy ngay trên máy này. Không có gói tin nào ra Internet.
set -eu

CONG="${CONG:-8080}"
THU_MUC="$(cd "$(dirname "$0")" && pwd)"

echo "Mở http://localhost:$CONG/ trong trình duyệt. Ctrl+C để dừng."
exec python3 -m http.server "$CONG" --directory "$THU_MUC"
