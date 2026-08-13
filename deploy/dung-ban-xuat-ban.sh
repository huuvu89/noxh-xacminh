#!/usr/bin/env bash
# Dựng hai thứ từ cùng một lần publish: bản đem lên địa chỉ tĩnh công khai, và gói offline tải về.
#
# Kịch bản này chạy được cả trên máy cá nhân, không chỉ trên CI — người hoài nghi tự dựng lại rồi
# đối chiếu nội dung với bản đang phục vụ, thay vì phải tin lời trang web. Nó không đẩy gì đi đâu:
# việc đẩy là của workflow gọi nó.
#
#   BASE_HREF=/noxh-xacminh/ COMMIT=$(git rev-parse HEAD) ./deploy/dung-ban-xuat-ban.sh
#
# Lưu ý về "dựng lại ra cùng mã băm": nội dung dựng lại được, nhưng mã băm của file .zip còn phụ
# thuộc công cụ nén và dấu thời gian, nên đừng hứa byte-identical. Mã băm in ở chân trang là để
# đối chiếu với **gói đã tải về từ trang**, và với chính con số mà nhật ký lần dựng công khai in ra.
set -euo pipefail

GOC="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RA="${RA:-$GOC/xuat-ban}"

# Địa chỉ tĩnh thường nằm trong một thư mục con (…/noxh-xacminh/); base href sai thì _framework/
# tải hụt và trang đứng ở màn hình "đang tải".
BASE_HREF="${BASE_HREF:-/}"
# Thiếu dấu gạch chéo cuối thì trình duyệt coi đoạn cuối là tên file: `<base href="/noxh-xacminh">`
# làm `_framework/…` rơi về `/_framework/…`. Nhà cung cấp trả base_path không có dấu này là chuyện
# thường, nên chuẩn hoá ngay ở đây thay vì chờ phát hiện trên trang đã lên sóng.
case "$BASE_HREF" in */) ;; *) BASE_HREF="$BASE_HREF/" ;; esac

COMMIT="${COMMIT:-$(git -C "$GOC" rev-parse HEAD 2>/dev/null || echo "")}"
LINK_LAN_DUNG="${LINK_LAN_DUNG:-}"
THOI_DIEM="${THOI_DIEM:-$(date -u +%Y-%m-%dT%H:%M:%SZ)}"

# ── Dựng ─────────────────────────────────────────────────────────────────────────────────
# `RA` bị xoá sạch ngay dưới đây; một biến môi trường đặt hớ không được kéo theo cả cây nguồn.
case "$RA" in "" | "/" | "$GOC") echo "RA không dùng được: '$RA'" >&2; exit 1 ;; esac

rm -rf "$RA"
mkdir -p "$RA"

# `BAN_TINH` chỉ sẵn một thư mục đã publish thì dùng luôn — để test chạy được toàn bộ phần dàn
# trang (base href, 404, gói offline, dấu vết bản dựng) mà không phải publish lại cả app.
if [ -z "${BAN_TINH:-}" ]; then
    dotnet publish "$GOC/src/Noxh.XacMinh.Web" --configuration Release --output "$RA/publish"
    BAN_TINH="$RA/publish/wwwroot"
fi

[ -f "$BAN_TINH/index.html" ] || { echo "Không thấy $BAN_TINH/index.html — publish hỏng?" >&2; exit 1; }

# GitHub Pages chạy Jekyll trước khi phục vụ, và Jekyll nuốt thư mục bắt đầu bằng dấu gạch dưới:
# mất _framework/ là mất toàn bộ runtime.
[ -f "$BAN_TINH/.nojekyll" ] || { echo "Publish làm rơi .nojekyll — _framework/ sẽ bị nuốt." >&2; exit 1; }

# `build-info.json`: dấu vết để nối trang đang chạy với mã nguồn công khai. Trang đọc đúng file này
# để in chân trang — đổi tên trường ở đây là chân trang trắng bên kia.
ghi_dau_vet() { # $1=thư mục  $2=tên gói offline  $3=mã băm gói
    cat >"$1/build-info.json" <<JSON
{
  "maCommit": "$COMMIT",
  "tenGoi": "$2",
  "maBamGoi": "$3",
  "linkLanDung": "$LINK_LAN_DUNG",
  "thoiDiemDung": "$THOI_DIEM"
}
JSON
}

# ── Gói offline ──────────────────────────────────────────────────────────────────────────
# Giữ nguyên base href "/" của bản nguồn: gói này chạy sau một máy chủ file tại chỗ, ở thư mục gốc.
# Gói không mang mã băm của chính nó — tự tham chiếu thì không tính được; chân trang của bản offline
# nói thẳng điều đó và chỉ sang trang công khai.
cp -r "$BAN_TINH" "$RA/offline"
cp -r "$GOC/deploy/offline/." "$RA/offline/"
ghi_dau_vet "$RA/offline" "" ""

TEN_GOI="noxh-xacminh-offline${COMMIT:+-${COMMIT:0:7}}.zip"
# Đóng băng dấu thời gian trước khi nén: hai lần dựng cùng một nội dung ra cùng một gói, để khác
# biệt nào cũng là khác biệt thật.
find "$RA/offline" -exec touch -d "$THOI_DIEM" {} +
dotnet msbuild "$GOC/deploy/DongGoiOffline.proj" -nologo -v:quiet \
    -t:DongGoi -p:ThuMuc="$RA/offline" -p:FileRa="$RA/$TEN_GOI"
MA_BAM_GOI="$(sha256sum "$RA/$TEN_GOI" | cut -d' ' -f1)"

# ── Bản đem lên địa chỉ tĩnh ─────────────────────────────────────────────────────────────
cp -r "$BAN_TINH" "$RA/site"
sed -i "s|<base href=\"[^\"]*\"|<base href=\"$BASE_HREF\"|" "$RA/site/index.html"
grep -q "<base href=\"$BASE_HREF\"" "$RA/site/index.html" || {
    echo "Không đặt được base href='$BASE_HREF' — index.html đã đổi khuôn?" >&2
    exit 1
}

# Máy chủ tĩnh không biết định tuyến bên trong trang: đường dẫn con và F5 phải rơi về chính trang
# kiểm chứng, không phải trang lỗi của nhà cung cấp.
cp "$RA/site/index.html" "$RA/site/404.html"
cp "$RA/$TEN_GOI" "$RA/site/$TEN_GOI"
ghi_dau_vet "$RA/site" "$TEN_GOI" "$MA_BAM_GOI"

cat <<TOMTAT
Đã dựng xong:
  bản đem lên  : $RA/site  (base href $BASE_HREF)
  gói offline  : $RA/$TEN_GOI
  commit       : ${COMMIT:-(không rõ)}
  SHA-256 gói  : $MA_BAM_GOI
TOMTAT
