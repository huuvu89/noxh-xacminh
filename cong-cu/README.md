# `cong-cu/` — công cụ dòng lệnh cho tổ giám sát

Thư mục này **không** đi kèm bản web công khai và không nằm trong gói offline. Nó dành cho tổ giám
sát và người vận hành, chạy trên máy của họ.

## `tai-goi-trail.py` — tải kho bằng chứng thành một gói `.zip`

### Vì sao cần

Công cụ kiểm chứng là trang tĩnh chạy trong trình duyệt. Trình duyệt chỉ đọc được kho bằng chứng
khi **chính kho** trả tiêu đề CORS (`Access-Control-Allow-Origin`, và cả `OPTIONS` nếu đọc bằng
khoá) — thứ nằm ở cấu hình bucket, ngoài tầm tay người kiểm. Kho chưa bật CORS thì màn hình trail
dừng ở **KHÔNG KIỂM ĐƯỢC**, dù kho đã mở công khai.

Script chạy ngoài trình duyệt nên không vướng CORS. Nó tải kho về thành một gói `.zip`, người kiểm
nạp gói đó vào mục **“Trail bằng chứng trên kho lưu trữ chỉ-ghi”** của công cụ. Gói cũng là cách
duy nhất để **bản offline** kiểm được trail, vì máy chạy bản offline vốn không có mạng.

Cách này **yếu hơn** đọc thẳng kho, và công cụ nói rõ điều đó ở mọi kết luận trail: đọc thẳng thì
chính trình duyệt người kiểm chứng kiến các lô đang nằm trên kho, còn nạp gói thì mắt xích ấy do
người chạy script gánh. Vì vậy:

- Ai kiểm thì **tự chạy script trên máy mình** khi có thể.
- Gói đi kèm mã băm: công cụ in `SHA-256` của gói vào bản xuất kết quả, đối chiếu bằng `sha256sum`.
- Hai người tải cùng một kho ra **cùng một gói byte-cho-byte** (thứ tự và mốc thời gian trong zip
  cố định), nên hai bên so mã băm gói của nhau là biết có ai đưa gói khác không.

Việc đúng đắn nhất vẫn là **xin ban tổ chức bật CORS cho bucket**; script này là lối thoát khi
không xin được, và là đường trail cho bản offline.

### Chạy

Trước lễ, chạy tự kiểm một lần (không gọi mạng — nó so chữ ký với vector chuẩn AWS công bố):

```bash
python3 tai-goi-trail.py --tu-kiem
```

Sau lễ, kho đã mở công khai — tải ẩn danh:

```bash
python3 tai-goi-trail.py \
    --diem-cuoi https://s3.vd-cloud.vn --bucket bang-chung --tien-to trail/ \
    --vung us-east-1 --ra goi-trail.zip
```

Trong lễ, kho chưa mở — cần khoá **chỉ-đọc** ban tổ chức cấp. Khoá **không nhận qua tham số dòng
lệnh** (dòng lệnh lộ ra ở `ps` và ở lịch sử shell); đặt biến môi trường hoặc để script hỏi:

```bash
python3 tai-goi-trail.py --hoi-khoa --diem-cuoi … --bucket … --ra goi-trail.zip
# hoặc: NOXH_KHO_MA_KHOA / NOXH_KHO_BI_MAT / NOXH_KHO_THE_PHIEN trong môi trường
```

Script chỉ ký `GET`, nên khoá có quyền ghi dán nhầm vào đây cũng không dựng nổi một request sửa kho.
Chỉ dùng thư viện chuẩn của Python 3 — máy không phải cài gì thêm.

Chạy xong script in ra `SHA-256` của gói. Lô nào tải không được thì nó **kêu lên và thoát khác 0**,
gói sẽ thiếu lô đó và công cụ báo lô đó chưa kiểm được — không lô nào bị bỏ trong im lặng.

### Trong gói có gì

```
manifest.json          # lời khai để hiển thị: địa chỉ kho, thời điểm tải. KHÔNG đổi được kết luận nào
listing/000.xml …      # nguyên văn từng trang trả lời ListObjectsV2 của kho
objects/<key>          # byte thô từng lô, tên đúng bằng key trên kho
```

Ba điều khuôn này cố tình giữ, và cũng là chỗ dễ mất nhất khi ai đó “tối ưu” gói:

1. **Gói chỉ chở byte thô.** Script không tính mã băm hộ từng lô; công cụ tự băm lại trên đúng byte
   trong gói. Script mà khai hộ mã băm thì chuỗi móc xích chỉ còn kiểm lời khai của script.
2. **Chở nguyên văn XML danh sách**, không chở danh sách key đã bóc: công cụ tự suy ra danh sách và
   “đã đọc hết kho hay chưa” bằng chính bộ bóc đang được test. Kho từ chối (`AccessDenied`) cũng
   giữ nguyên trong gói, nên không ai nhầm “kho rỗng” với “khoá không đủ quyền”.
3. **`manifest.json` chỉ để hiển thị.** Thiếu nó gói vẫn bóc được, chỉ là màn hình không biết địa
   chỉ kho mà nói.

Khuôn gói được ghim hai đầu: `Noxh.XacMinh.Core/Kho/DocGoiTrail.cs` bóc gói, và
`Noxh.XacMinh.Pipeline.Tests/KichBanTaiGoiTrailTests.cs` bắt script với lõi phải khai cùng một khuôn.
