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
- **Hai chế độ hiển thị.** Mặc định nói chuyện với người dân: mỗi hạng mục một dòng kết luận kèm
  một câu giải thích "cái này chứng minh điều gì", không hex. Công tắc **chuyên sâu** mở giá trị kỳ
  vọng / giá trị tính được / preimage / số liệu thô cho giám sát và kiểm toán, và giữ nguyên lựa
  chọn đó khi nạp file mới trong cùng phiên.

## Cấu trúc repo

| Thư mục | Nội dung |
|---|---|
| `fixtures/` | Fixture chuẩn vàng: JSON minh bạch **thật** của một dự án đã chạy trọn 4 vòng, kèm danh mục căn của chính dự án đó và mã nguồn sinh lại cả hai từ backend. Đọc [`fixtures/README.md`](fixtures/README.md) trước khi dùng — nó nói rõ fixture chứng minh được gì và **không** chứng minh được gì. |
| `src/Noxh.XacMinh.Core/` | Lõi kiểm chứng **thuần, không I/O**: lõi mật mã copy nguyên văn từ backend (`Crypto/`), model JSON minh bạch (`Transparency/`), và seam duy nhất `Verifier.Verify(VerificationInput) → VerificationReport` (`Verification/`). Thêm hạng mục kiểm = thêm một `Checks/*.cs` rồi cắm vào `Verifier`. `Decks/` giữ hai thứ: `DeckRebuilder.Rebuild(report, deck, catalog)` **dựng lại** chồng phiếu từ hạt giống — hai vòng đi theo loại căn dựng lại cả quỹ căn của loại đó trước (vòng ưu tiên: xáo danh mục; vòng bốc thẳng: `QuyCanConDu` suy quỹ căn còn dư từ bảng kết quả rồi mới xáo — hạng mục kiểm và lưới cùng dùng một bản dựng lại), vòng căn dư dựng lại quỹ căn dư **chung** (cũng suy ra từ bảng kết quả) cộng **hoán vị số dự khuyết** từ nhãn hạt giống riêng — bằng chứng trực tiếp rằng hạng dự khuyết không phụ thuộc thời điểm bấm; quy mô danh sách dự khuyết không nằm trong mã băm đầu vào nên lấy từ `waitlistSize` của báo cáo, báo cáo không công bố thì bản dựng lại mang theo giả định và lệch mã băm chỉ cho KHÔNG KIỂM ĐƯỢC. Còn `DeckGridBuilder.Build(report, catalog)` là phép **trình bày** — lưới ô phiếu + số liệu tóm tắt + bộ lọc, không kết luận ĐẠT/KHÔNG ĐẠT. `Units/` giữ **danh mục căn nhúng sẵn** (509 căn, 5 loại — bản do ban tổ chức công bố, nhúng thẳng vào assembly) kèm phép nạp file danh mục khác đè lên; danh mục đi vào lõi qua `VerificationInput.Catalog`. |
| `src/Noxh.XacMinh.Web/` | Vỏ giao diện Blazor WebAssembly: nạp file/nội dung dán, gọi lõi, vẽ kết luận. Không tự kiểm gì cả. Khuôn hiển thị chung ở `Components/` (`KetQuaKiem` vẽ mọi hạng mục; `KhungLuoiPhieu` giữ trạng thái xem lưới, `LuoiOPhieu` chỉ vẽ lưới, `DanhMucCan` hiện danh mục căn đang dùng), chế độ hiển thị và danh mục đang dùng ở `HienThi/`. |
| `tests/` | Test. `Noxh.XacMinh.Fixtures.Tests` là hàng rào của fixture; `Noxh.XacMinh.Core.Tests` kiểm lõi qua đúng seam, bằng fixture chuẩn vàng và các bản bị sửa dựng từ chính nó; `Noxh.XacMinh.Web.Tests` vẽ component ra HTML tĩnh để kiểm khuôn hiển thị hai chế độ. |

```bash
dotnet test          # toàn bộ test trong solution
dotnet run --project src/Noxh.XacMinh.Web      # chạy thử tại http://localhost:5xxx
```

Dựng bản tĩnh (không cần máy chủ ứng dụng — đây là thứ đem lên GitHub Pages):

```bash
dotnet publish src/Noxh.XacMinh.Web -c Release -o publish
python3 -m http.server 8080 --directory publish/wwwroot
```

## Hạng mục đã kiểm được

| # | Hạng mục | Trạng thái |
|---|---|---|
| 1 | Cam kết ngẫu nhiên máy chủ — `SHA-256(rServer) == rServerCommit`, từng vòng một | ✅ |
| 2 | Hạt giống gốc — `masterSeed == SHA-256(rServer ‖ rSupervisor ‖ blockHash)`, từng vòng một | ✅ |
| 3 | Mã băm chồng phiếu — `deckHash == SHA-256(canonical(tickets))`, từng chồng phiếu một | ✅ |
| 4 | Chuỗi băm nhật ký bốc — **tính lại** `entryHash` từ đúng preimage, nêu rõ bước lệch đầu tiên | ✅ |
| 5 | Vé từng lượt bốc — payload khớp vé ở đúng vị trí trong chồng phiếu, từng vòng một, liệt kê đủ điểm lệch | ✅ |
| 6 | Mã băm bảng kết quả chung cuộc — `resultsHash == SHA-256(canonical(rows))`, phủ cả những dòng không có vé nào | ✅ |
| 7 | Dấu thời gian mốc cam kết — băm lại chuỗi đóng dấu của từng token, bóc từng trường đối chiếu cam kết neo và cam kết ngẫu nhiên máy chủ; không có token thì cảnh báo | ✅ |
| ★ | **Tái lập chồng phiếu vòng quyền mua (A1)** — dựng lại chồng phiếu từ `MASTER_SEED` bằng phép xáo copy nguyên văn từ backend rồi so `deckHash`; lưới ô phiếu đánh dấu từng ô khớp/lệch bản dựng lại | ✅ |
| ★ | **Tái lập vòng phân căn ưu tiên (A2)** — dựng lại **quỹ căn ưu tiên của từng loại** từ hạt giống (`POOL:{loại}`) rồi dựng chồng phiếu của loại đó (`A2:deck:{loại}`) và so `deckHash`; quỹ căn dựng lại hiện thẳng trên lưới. Cần danh mục căn — thiếu thì KHÔNG KIỂM ĐƯỢC kèm hướng dẫn nạp | ✅ |
| ★ | **Tái lập vòng bốc thẳng theo loại căn (B)** — **suy ra quỹ căn còn dư của từng loại** (danh mục trừ đi những căn bảng kết quả nói đã phân ở vòng ưu tiên), xáo bằng `B:units:{loại}` rồi dựng chồng phiếu (`B:deck:{loại}`) và so `deckHash`. Quỹ căn còn dư là **dữ liệu suy diễn**, màn hình nói rõ điều đó; suy diễn mâu thuẫn (căn phân hai lần, căn không có trong danh mục, vé trúng nằm ngoài quỹ suy ra) ra KHÔNG KIỂM ĐƯỢC kèm chính mâu thuẫn, không ra KHÔNG ĐẠT | ✅ |
| 8–9 và tái lập C | | chưa (vé tiếp theo) |

## Vì sao C# WebAssembly chứ không phải JavaScript

Tái lập kết quả đòi hỏi trùng khít ngữ nghĩa .NET ở ba chỗ mà port sang JS gần như chắc chắn sai:

- `Guid.ToByteArray()` dùng layout **mixed-endian**, không phải thứ tự byte RFC 4122 — nằm trong
  preimage của `entryHash`.
- PRNG của phép xáo dùng counter **8 byte little-endian**.
- `ListHash` băm trên chuỗi do `System.Text.Json` **mặc định** sinh (enum ra số, non-ASCII escape
  `\uXXXX`).

Viết bằng C# thì ba cái bẫy này tự biến mất — dùng đúng thư viện đã sinh ra giá trị gốc.
