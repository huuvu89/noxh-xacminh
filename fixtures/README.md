# Fixture chuẩn vàng — JSON minh bạch của một dự án đã hoàn tất

`transparency-golden.json` là phản hồi **thật** của `GET /projects/{id}/transparency`, sinh ra từ
chính backend (`backend-noxh-lottery`) chạy trọn một buổi lễ bốc thăm bốn vòng tới trạng thái
`Completed`. Đây là dữ liệu gốc để mọi vé sau kiểm bằng dữ liệu thay vì bằng niềm tin.

`transparency-golden.meta.json` ghi nguồn gốc: commit backend đã sinh ra fixture, mốc thời gian
sinh, và SHA-256 của chính file fixture (`sha256sum transparency-golden.json` phải khớp).

`apartment-units-golden.json` là **danh mục căn của chính dự án trong fixture** (20 căn, 2 loại), ở
đúng định dạng file danh mục người kiểm nạp vào công cụ. Tái lập vòng phân căn ưu tiên phải dựng lại
quỹ căn ưu tiên từ danh mục, mà bản danh mục **nhúng sẵn** trong công cụ là của một dự án khác — nạp
nhầm bản nhúng vào fixture này thì hạng mục đó ra KHÔNG KIỂM ĐƯỢC, đúng như thiết kế. Generator ghi
lại file này mỗi lần sinh fixture.

## Vì sao không bịa JSON

Chồng phiếu trong fixture phải **mọc ra từ `MASTER_SEED`** theo đúng phép xáo của engine. Một
fixture viết tay sẽ buộc mọi test tái lập của công cụ kiểm chứng phải bám theo con số đã bịa — tức
là công cụ kiểm chính nó, không kiểm backend. Fixture này đi qua đúng đường mà buổi lễ thật đi:
endpoint HTTP, Postgres/Redis thật (Testcontainers), engine bốc thăm thật.

## Kịch bản trong fixture

40 hồ sơ · 20 căn thuộc 2 loại (`2PN` 12 căn / 4 suất ưu tiên, `1PN` 8 căn / 2 suất) ·
`waitlistSize = 5`.

| Chồng phiếu | Số vé | Vé trúng | Ghi chú |
|---|---|---|---|
| `A1` | 6 | 4 | 6 U2 xác nhận và bốc hết; 2 vé `KHONG_TRUNG_UU_TIEN` |
| `A2:1PN` | 1 | 1 | quỹ ưu tiên 1PN còn dư suất → chảy sang máy gom |
| `A2:2PN` | 5 | 4 | 5 người tranh 4 suất → **1 vé `CHO_PHAN_LOAI_DU`** |
| `B:1PN` | 14 | 6 | có ô phiếu không ai bốc |
| `B:2PN` | 20 | 8 | có ô phiếu không ai bốc |
| `C` | 21 | 4 | **5 vé `DU_KHUYET:{n}` đã đánh số** + vé `KHONG_TRUNG` |

Những ca được cố ý dựng vào fixture, vì thiếu chúng thì công cụ kiểm chứng không có mẫu để kiểm:

- **Đủ bốn loại vé** của lưới phiếu: `TRUNG_QUYEN_MUA` · `KHONG_TRUNG_UU_TIEN` ·
  `CHO_PHAN_LOAI_DU` · `TRUNG:{mã căn}` · `DU_KHUYET:{n}` · `KHONG_TRUNG`.
- **Vé do máy bốc thay** (`autoDrawn = true`, sinh ở `close-draw-a2`) bên cạnh vé do người bấm.
- **Ô phiếu không ai bốc** (người xác nhận tham gia nhưng không bấm) ở vòng B và C — căn của
  những vé đó chảy xuống vòng sau.
- **Dòng kết quả không có vé**: người giữ vé `CHO_PHAN_LOAI_DU` được máy gom A2g phân căn.
- **Dự khuyết đánh số 1–5** là hoán vị từ hạt giống, không theo thứ tự bấm.
- **16 token dấu thời gian** phủ 8 phạm vi (`FREEZE`, `STEPCHAIN:{vòng}`, `RESULTS:{vòng}`) × 2
  authority — `preimage` của `FREEZE` mang `listHash` của danh sách đã khoá thật.
- **Tên tiếng Việt có dấu** trong danh sách hồ sơ: `ListHash` băm trên chuỗi `System.Text.Json`
  mặc định (escape `\uXXXX`), fixture toàn ASCII sẽ giấu mất cái bẫy đó.

## Fixture này KHÔNG chứng minh điều gì

- **Mốc neo chuỗi khối là giả.** Môi trường test dùng `StubBlockchainAnchor`, nên
  `blockHeight`/`blockHash` trong fixture **không** tra được trên chuỗi khối công khai. Hạng mục
  kiểm mốc neo (vé #15) phải ra **KHÔNG KIỂM ĐƯỢC** với fixture này — đó là hành vi đúng, không
  phải lỗi. Chỉ dữ liệu buổi lễ thật mới nghiệm thu được hạng mục đó.
- **Token dấu thời gian là stub**, không phải RFC 3161 ký thật: kiểm được
  `SHA-256(preimage) == digest` và nội dung preimage, **không** kiểm được chữ ký/chuỗi chứng thư.
- **Không kèm danh sách hồ sơ gốc + `K_idx`**, nên `listHash` trong preimage `FREEZE` chưa tái lập
  được từ fixture này (hạng mục `ListHash`, vé #16/#17, cần fixture riêng).
- Toàn bộ hồ sơ là **dữ liệu tổng hợp** (CCCD/SĐT/tên do generator sinh), không phải người thật.

## Sinh lại fixture

Cần: **.NET 8 SDK**, **Docker** đang chạy (integration test của backend dựng Postgres + Redis thật
bằng Testcontainers), và repo `backend-noxh-lottery` nằm cạnh repo này trong monorepo.

```bash
bash fixtures/generator/sinh-lai-fixture.sh
# hoặc trỏ tới backend ở chỗ khác:
bash fixtures/generator/sinh-lai-fixture.sh /đường/dẫn/backend-noxh-lottery
```

Script chạy `fixtures/generator/GoldenTransparencyFixtureGenerator.cs` (một test xUnit trong dự án
`Noxh.XacMinh.FixtureGen.csproj`, tham chiếu thẳng bộ integration test của backend), rồi ghi đè
`transparency-golden.json` + `transparency-golden.meta.json` + `apartment-units-golden.json`.

Không dùng script được thì chạy tay:

```bash
dotnet test fixtures/generator/Noxh.XacMinh.FixtureGen.csproj \
  -p:BackendRepo=/đường/dẫn/backend-noxh-lottery \
  -e NOXH_FIXTURE_OUT=$PWD/fixtures/transparency-golden.json \
  -e NOXH_BACKEND_COMMIT=$(git -C /đường/dẫn/backend-noxh-lottery log -1 --format=%H) \
  -e NOXH_BACKEND_COMMITTED_AT=$(git -C /đường/dẫn/backend-noxh-lottery log -1 --format=%cI)
```

Lưu ý khi sinh lại:

- **`Noxh.XacMinh.FixtureGen.csproj` không nằm trong solution và không chạy trong CI** — nó tham
  chiếu repo anh em bằng đường dẫn tương đối, chỉ build được trên máy có cả monorepo.
- Generator tự **chặn ở nguồn**: fixture thiếu bốn vòng, thiếu vé dự khuyết đã đánh số, thiếu vé
  máy bốc… là test đỏ ngay, không ghi file.
- Mỗi lần sinh ra một bộ số **khác** (entropy máy chủ, GUID, mốc thời gian là ngẫu nhiên/thời điểm
  chạy). `R_supervisor` thì cố định để tra ngược bằng mắt. Sinh lại ⇒ mọi giá trị ghim trong test
  của công cụ kiểm chứng phải cập nhật theo — sinh lại là việc có chủ ý, không phải thao tác dọn dẹp.
- Cây làm việc của backend phải **sạch**, nếu không `backendCommit` trong manifest không truy được
  về đúng mã đã sinh ra fixture (script có cảnh báo).

## Khi backend đổi định dạng

Đó chính là lúc fixture này có giá trị: sinh lại fixture từ backend mới, chạy toàn bộ test của
công cụ kiểm chứng, và **so `backendCommit` trong manifest với commit mới** để biết lệch bắt đầu
từ đâu. Test đỏ ở đây là tín hiệu hợp đồng API đã đổi — không được sửa fixture bằng tay cho khớp.
