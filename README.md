# noxh-xacminh — Công cụ kiểm chứng độc lập kết quả bốc thăm NƠXH

Web app chạy **hoàn toàn trong trình duyệt** (Blazor WebAssembly) để bất kỳ ai — người dân, ban
giám sát, báo chí — tự kiểm chứng kết quả buổi bốc thăm nhà ở xã hội, không cần cài đặt, không cần
tin lời ban tổ chức.

> Đặc tả đầy đủ: issue **`quydautu/common_docs#3`**
> (`glab issue view 3` chạy trong repo `common_docs`).

## Công cụ này chứng minh điều gì

Nạp vào báo cáo minh bạch (`GET /projects/{id}/transparency`) đã công bố, công cụ chạy **13 hạng mục
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
10. Trail bằng chứng trên kho lưu trữ **chỉ-ghi** móc xích liền nhau — không lô nào ở giữa bị lấy
    bớt hay bị sửa sau khi đã lên kho (trong lễ: dán khoá chỉ-đọc; sau lễ: đọc ẩn danh).
11. Từng lượt bốc trên trail khớp nhật ký bốc đã công bố — có ở một bên mà thiếu ở bên kia, hay có
    ở cả hai nơi mà khai khác nhau, đều được nêu đích danh.
12. Cam kết ngẫu nhiên máy chủ trên trail khớp cam kết đã công bố (lần chốt sau cùng).
13. Đầu chuỗi băm từng vòng trên trail khớp giá trị **tính lại được** từ nhật ký công bố.

Ba hạng mục cuối là mỏ neo độc lập với cơ sở dữ liệu: chúng bắt được đúng thứ mà chuỗi băm không bắt
được — dữ liệu bị sửa **trước khi** chuỗi băm được vật chất hoá. Giới hạn phải nói kèm ở mọi kết
luận: trail chỉ chứng minh được thứ **đã** lên kho, bản ghi chưa bao giờ được đẩy lên thì nó không
thấy.

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
  độc lập" chỉ mạnh khi công cụ không cần nói chuyện với hệ thống bị nghi ngờ. Có đúng **hai** đường
  ra mạng, cả hai chỉ chạy khi người dùng bấm, và không bấm thì hạng mục tương ứng dừng ở KHÔNG KIỂM
  ĐƯỢC chứ không bao giờ ĐẠT: (a) đọc block ở độ cao đã cam kết từ **sổ cái công khai**
  (Ethereum/Bitcoin) — chỉ gửi đi độ cao block vốn đã công khai, không bấm thì còn link tra cứu tay;
  (b) đọc **kho bằng chứng chỉ-ghi** do ban tổ chức công bố — chỉ gửi đi địa chỉ kho người kiểm tự
  nhập kèm chữ ký của khoá chỉ-đọc nếu có. Không đường nào mang dữ liệu người dùng thả vào đi cả.
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
| `fixtures/` | Fixture chuẩn vàng: JSON minh bạch **thật** của một dự án đã chạy trọn 4 vòng, kèm danh mục căn và **bảng danh sách hồ sơ đã khoá** (+ khoá chỉ mục mù dev) của chính dự án đó, và mã nguồn sinh lại tất cả từ backend. Đọc [`fixtures/README.md`](fixtures/README.md) trước khi dùng — nó nói rõ fixture chứng minh được gì và **không** chứng minh được gì. |
| `src/Noxh.XacMinh.Core/` | Lõi kiểm chứng **thuần, không I/O**: lõi mật mã copy nguyên văn từ backend (`Crypto/`), model JSON minh bạch (`Transparency/`), và seam duy nhất `Verifier.Verify(VerificationInput) → VerificationReport` (`Verification/`). Thêm hạng mục kiểm = thêm một `Checks/*.cs` rồi cắm vào `Verifier`. `Decks/` giữ hai thứ: `DeckRebuilder.Rebuild(report, deck, catalog)` **dựng lại** chồng phiếu từ hạt giống — hai vòng đi theo loại căn dựng lại cả quỹ căn của loại đó trước (vòng ưu tiên: xáo danh mục; vòng bốc thẳng: `QuyCanConDu` suy quỹ căn còn dư từ bảng kết quả rồi mới xáo — hạng mục kiểm và lưới cùng dùng một bản dựng lại), vòng căn dư dựng lại quỹ căn dư **chung** (cũng suy ra từ bảng kết quả) cộng **hoán vị số dự khuyết** từ nhãn hạt giống riêng — bằng chứng trực tiếp rằng hạng dự khuyết không phụ thuộc thời điểm bấm; quy mô danh sách dự khuyết không nằm trong mã băm đầu vào nên lấy từ `waitlistSize` của báo cáo, báo cáo không công bố thì bản dựng lại mang theo giả định và lệch mã băm chỉ cho KHÔNG KIỂM ĐƯỢC. Còn `DeckGridBuilder.Build(report, catalog)` là phép **trình bày** — lưới ô phiếu + số liệu tóm tắt + bộ lọc, không kết luận ĐẠT/KHÔNG ĐẠT. `Units/` giữ **danh mục căn nhúng sẵn** (509 căn, 5 loại — bản do ban tổ chức công bố, nhúng thẳng vào assembly) kèm phép nạp file danh mục khác đè lên; danh mục đi vào lõi qua `VerificationInput.Catalog`. `DanhSach/` là hạng mục của tổ giám sát: nạp danh sách vào bằng **hai đường** — thả thẳng **file Excel gốc** (`FileExcel` đọc `.xlsx` không cần thư viện ngoài; `CotDanhSach` nhận cột theo tên tiêu đề rồi cắt khoảng trắng từng ô, đúng như backend đọc lúc nhập) hoặc **dán bảng** làm lối thoát khi gặp file lạ; cả hai đi vào chung `BangDanhSach`, rồi dựng lại `ListHash` bằng phép băm copy nguyên văn từ backend (`MaBamDanhSach` — JSON mặc định của .NET, nhóm ra số, tiếng Việt bị escape), và khi lệch thì thử các biến thể chuẩn hoá để nói "khớp nếu…" (`ChanDoanBang`) chứ **không** tự sửa dữ liệu người kiểm dán vào. `Kho/` là phần đọc **trail bằng chứng** trên kho lưu trữ chỉ-ghi: `SigV4` ký request bằng tay (hàm thuần, ghim bằng bộ vector chuẩn AWS công bố — và **chỉ ký GET/HEAD**, nên khoá có quyền ghi dán nhầm vào cũng không dùng được), `YeuCauDocKho` dựng request path-style, `LietKeKho`/`DocLoBangChung` bóc danh sách object và từng lô JSONL `NOXH-TRAIL-v1`; các lô đọc được đi vào lõi qua `VerificationInput.Kho`. |
| `src/Noxh.XacMinh.Web/` | Vỏ giao diện Blazor WebAssembly: nạp file/nội dung dán, gọi lõi, vẽ kết luận. Không tự kiểm gì cả. Khuôn hiển thị chung ở `Components/` (`KetQuaKiem` vẽ mọi hạng mục; `KhungLuoiPhieu` giữ trạng thái xem lưới, `LuoiOPhieu` chỉ vẽ lưới, `DanhMucCan` hiện danh mục căn đang dùng, `MocNeoChuoiKhoi` là nút tra cứu block, `DanhSachHoSo` là khung thả file Excel gốc / dán bảng danh sách + khoá chỉ mục mù — cảnh báo mức nhạy cảm hiện trước khi mở ô nạp, và có nút xoá khoá khỏi bộ nhớ; `TrailBangChung` là khung nhập địa chỉ kho bằng chứng + khoá chỉ-đọc), chế độ hiển thị / danh mục / danh sách hồ sơ / kết quả tra cứu mốc neo / trail đã đọc ở `HienThi/`. Hai chỗ gọi mạng nằm gọn ở `MocNeo/` (đọc block từ sổ cái công khai → `VerificationInput.Blocks`) và `Kho/` (đọc lô bằng chứng từ kho chỉ-ghi → `VerificationInput.Kho`); mọi kiểu hỏng (mạng đứt, CORS, kho từ chối, quá hạn) thành một kết quả mang lỗi đi vào lõi, không thành ngoại lệ bị nuốt. |
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
| 8 | Mốc neo chuỗi khối — đọc block ở **độ cao đã cam kết** của chính chuỗi từng vòng dùng, so mã băm với bản công bố, rồi so thời điểm cấp dấu thời gian với thời điểm block được đào (cam kết phải có **trước** khi block tồn tại). Chưa tra cứu / nguồn ngoài hỏng ⇒ KHÔNG KIỂM ĐƯỢC kèm link tra cứu thủ công | ✅ |
| 9 | Danh sách hồ sơ đầu vào — thả **file Excel gốc** (hoặc dán bảng) + khoá chỉ mục mù, dựng lại `ListHash` rồi so với giá trị đã ghim (lấy từ chuỗi đã được đóng dấu thời gian, hoặc từ trường riêng nếu báo cáo có). Lệch thì **chẩn đoán** biến thể chuẩn hoá và nói "khớp nếu…" chứ không tự sửa dữ liệu; chưa dán ⇒ KHÔNG KIỂM ĐƯỢC | ✅ |
| ★ | **Tái lập vòng căn dư + dự khuyết (C)** — dựng lại quỹ căn dư **chung** (suy ra từ bảng kết quả) cộng **hoán vị số dự khuyết** từ nhãn hạt giống riêng, rồi so `deckHash`; quy mô danh sách dự khuyết lấy từ `waitlistSize`, báo cáo không công bố thì bản dựng lại mang theo giả định và lệch chỉ cho KHÔNG KIỂM ĐƯỢC | ✅ |
| 10 | Trail bằng chứng — chuỗi móc xích giữa các lô trên kho chỉ-ghi, và khoảng trống số thứ tự lô (chỉ cảnh báo: lô upload hỏng bị bỏ cũng để lại khoảng trống y hệt lô bị giấu) | ✅ |
| 11 | Trail bằng chứng — **đối chiếu với báo cáo minh bạch**: từng lượt bốc, cam kết ngẫu nhiên máy chủ, và đầu chuỗi băm từng vòng. Trail có mà báo cáo thiếu (hoặc hai bên khai khác nhau) ⇒ KHÔNG ĐẠT; báo cáo có mà trail thiếu ⇒ KHÔNG KIỂM ĐƯỢC, vì đường đẩy bằng chứng là best-effort và vé máy bốc thay không đi qua đó | ✅ |

## Vì sao C# WebAssembly chứ không phải JavaScript

Tái lập kết quả đòi hỏi trùng khít ngữ nghĩa .NET ở ba chỗ mà port sang JS gần như chắc chắn sai:

- `Guid.ToByteArray()` dùng layout **mixed-endian**, không phải thứ tự byte RFC 4122 — nằm trong
  preimage của `entryHash`.
- PRNG của phép xáo dùng counter **8 byte little-endian**.
- `ListHash` băm trên chuỗi do `System.Text.Json` **mặc định** sinh (enum ra số, non-ASCII escape
  `\uXXXX`).

Viết bằng C# thì ba cái bẫy này tự biến mất — dùng đúng thư viện đã sinh ra giá trị gốc.
