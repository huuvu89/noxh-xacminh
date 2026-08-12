# noxh-xacminh — Công cụ kiểm chứng độc lập kết quả bốc thăm NƠXH

Web app chạy **hoàn toàn trong trình duyệt** (Blazor WebAssembly) để bất kỳ ai — người dân, ban
giám sát, báo chí — tự kiểm chứng kết quả buổi bốc thăm nhà ở xã hội, không cần cài đặt, không cần
tin lời ban tổ chức.

> Đặc tả đầy đủ: issue **`quydautu/common_docs#3`**
> (`glab issue view 3` chạy trong repo `common_docs`).

## Công cụ này chứng minh điều gì

Nạp vào báo cáo minh bạch (`GET /projects/{id}/transparency`) đã công bố, công cụ chạy **9 hạng mục
kiểm** và **tái lập lại cả 4 vòng bốc thăm** từ hạt giống ngẫu nhiên đã cam kết:

1. Máy chủ đã niêm phong entropy trước khi biết mốc neo — `SHA-256(rServer) == rServerCommit`.
2. Hạt giống không bị thay — `MASTER_SEED == SHA-256(rServer ‖ rSupervisor ‖ blockHash)`.
3. Nội dung chồng phiếu công bố đúng bản đã niêm phong — `deckHash == SHA-256(canonical(tickets))`.
4. Nhật ký bốc không bị chèn/sửa — tính lại `entryHash` từ đúng preimage.
5. Kết quả từng lượt bốc khớp lá vé nằm ở đúng vị trí đó trong chồng phiếu.
6. Bảng kết quả chung cuộc không bị sửa — tính lại `resultsHash`.
7. Dấu thời gian RFC 3161 đóng đúng lên cam kết đang công bố.
8. Mốc neo là block thật trên chuỗi khối, và cam kết có **trước** khi block đó tồn tại.
9. Danh sách hồ sơ đầu vào khớp `ListHash` đã ghim (dành cho ban giám sát, cần khoá `K_idx`).

Và quan trọng nhất: **dựng lại chính chồng phiếu** từ `MASTER_SEED` rồi so `deckHash` — chứng minh
chồng phiếu mọc ra từ hạt giống đã cam kết trước, không phải do ai sắp đặt.

## Công cụ này KHÔNG chứng minh điều gì

- Không chứng minh danh sách hồ sơ là "danh sách đúng" — chỉ chứng minh nó khớp cam kết đã ghim.
  Ai đủ điều kiện dự bốc thăm là việc của hội đồng xét duyệt.
- Không chứng minh danh mục căn hộ đúng với thực tế dự án — đó là dữ liệu đầu vào do ban tổ chức
  công bố.
- Không kiểm chuỗi chứng thư của token dấu thời gian (chỉ kiểm nội dung được đóng dấu). Ai cần kiểm
  chữ ký thì tải token thô và dùng `openssl`.

## Nguyên tắc thiết kế

- **Không gọi máy chủ bốc thăm.** Người dùng tự thả file JSON đã công bố vào. Lập luận "kiểm chứng
  độc lập" chỉ mạnh khi công cụ không cần nói chuyện với hệ thống bị nghi ngờ.
- **Toàn bộ logic kiểm nằm sau một hàm thuần, không I/O** — mọi thứ cần mạng do lớp giao diện lấy
  về rồi đưa vào dưới dạng dữ liệu. Nhờ vậy kiểm được bằng fixture, tất định, không cần trình duyệt.
- **Lõi mật mã copy nguyên văn từ backend**, kèm test vector ghim. Viết lại theo trí nhớ là cách
  chắc chắn nhất để ra một công cụ luôn báo "ĐẠT".
- **Khoá người dùng dán vào chỉ nằm trong RAM** — không lưu đĩa, không gửi đi đâu.
- **Ba trạng thái, không phải hai**: ĐẠT · KHÔNG ĐẠT · **KHÔNG KIỂM ĐƯỢC (thiếu dữ liệu)**. Thiếu
  dữ liệu mà báo xanh là nói dối.

## Cấu trúc repo

| Thư mục | Nội dung |
|---|---|
| `fixtures/` | Fixture chuẩn vàng: JSON minh bạch **thật** của một dự án đã chạy trọn 4 vòng, kèm mã nguồn sinh lại nó từ backend. Đọc [`fixtures/README.md`](fixtures/README.md) trước khi dùng — nó nói rõ fixture chứng minh được gì và **không** chứng minh được gì. |
| `tests/` | Test. `Noxh.XacMinh.Fixtures.Tests` là hàng rào của fixture: fixture thiếu khối dữ liệu hay mất dấu vết nguồn gốc là đỏ ngay. |

```bash
dotnet test          # toàn bộ test trong solution
```

## Vì sao C# WebAssembly chứ không phải JavaScript

Tái lập kết quả đòi hỏi trùng khít ngữ nghĩa .NET ở ba chỗ mà port sang JS gần như chắc chắn sai:

- `Guid.ToByteArray()` dùng layout **mixed-endian**, không phải thứ tự byte RFC 4122 — nằm trong
  preimage của `entryHash`.
- PRNG của phép xáo dùng counter **8 byte little-endian**.
- `ListHash` băm trên chuỗi do `System.Text.Json` **mặc định** sinh (enum ra số, non-ASCII escape
  `\uXXXX`).

Viết bằng C# thì ba cái bẫy này tự biến mất — dùng đúng thư viện đã sinh ra giá trị gốc.
