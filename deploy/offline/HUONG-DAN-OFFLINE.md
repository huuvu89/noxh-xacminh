# Bản offline — công cụ kiểm chứng kết quả bốc thăm NƠXH

Gói này là **toàn bộ công cụ**. Nó chạy trên máy bạn, không gọi ra Internet, và dùng được kể cả khi
trang web công khai đã bị gỡ.

## Chạy

Mở thẳng `index.html` bằng cách nháy đúp thì **không chạy được** — công cụ là WebAssembly, trình
duyệt tải phần chạy bằng `fetch`, mà `fetch` trên `file://` bị chính trình duyệt chặn. Nên gói kèm
theo một máy chủ file chạy ngay trên máy này (không có gói tin nào ra Internet):

- **Linux / macOS**: `sh chay-offline.sh` (gọi qua `sh` vì file `.zip` không giữ được quyền chạy)
- **Windows**: nháy đúp `chay-offline.cmd`

Rồi mở `http://localhost:8080/` trong trình duyệt. Đổi cổng bằng biến `CONG`.

Cả hai file chỉ gọi `python3 -m http.server` trên chính thư mục này. Không có Python thì bất kỳ máy
chủ file tĩnh nào cũng được, ví dụ:

```bash
npx --yes serve .          # Node
php -S localhost:8080      # PHP
```

## Đối chiếu gói này với mã nguồn công khai

`build-info.json` trong gói ghi **mã commit** đã dựng và **link tới lần chạy dựng công khai**. Gói
không thể chứa mã băm của chính nó (tự tham chiếu thì không tính được) — mã băm SHA-256 của gói in
ở chân trang bản công khai và trong nhật ký lần chạy dựng.

Kiểm bằng tay:

```bash
sha256sum noxh-xacminh-offline-<commit>.zip
```

Con số đó phải khớp cả hai chỗ trên. Muốn đi xa hơn thì tự dựng lại từ mã nguồn tại đúng commit đó:

```bash
git clone <repo> && cd noxh-xacminh && git checkout <commit>
./deploy/dung-ban-xuat-ban.sh
```

Nội dung dựng lại được; riêng **mã băm của file .zip** còn phụ thuộc công cụ nén, nên hãy so nội
dung thư mục `xuat-ban/offline/` chứ đừng chờ hai file zip trùng byte.

## Giới hạn

Bản offline không tra cứu được mốc neo chuỗi khối và không đọc thẳng được trail bằng chứng trên kho
lưu trữ — hai việc đó cần mạng. Hai hạng mục ấy sẽ dừng ở **KHÔNG KIỂM ĐƯỢC**, kèm link tra cứu
tay, chứ không bao giờ tự chuyển thành ĐẠT.

Riêng trail còn một lối: xin **gói trail** (`.zip`) mà tổ giám sát đã tải sẵn từ kho, rồi nạp vào ô
“Nạp gói trail đã tải sẵn” trong mục trail bằng chứng — bước này không gọi mạng. Đường này yếu hơn
đọc thẳng kho và màn hình nói rõ chỗ yếu: máy này không tự chứng kiến các lô đang nằm trên kho, nó
chỉ băm lại đúng byte trong gói. Hãy đối chiếu `sha256sum` của gói với con số in trong bản xuất kết
quả, và tốt nhất là xin gói từ hai nguồn độc lập rồi so mã băm.
