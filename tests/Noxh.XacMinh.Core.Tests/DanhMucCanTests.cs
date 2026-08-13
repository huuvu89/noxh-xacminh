using System.Text;
using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Units;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #10 — danh mục căn hộ nhúng sẵn, có mã băm và cho nạp đè.
/// Danh mục là <b>dữ liệu đầu vào công bố</b>, nên test ở đây chỉ khẳng định công cụ đọc đúng bản
/// nó đang mang theo và nói thật về bản đó — không khẳng định danh mục đúng với thực tế dự án.
/// </summary>
public class DanhMucCanTests
{
    /// <summary>
    /// GHIM: SHA-256 của đúng file danh mục nhúng trong repo. Đổi dữ liệu mà quên nói ra thì test
    /// này đỏ — đó là mục đích của nó, không phải sửa số cho hết đỏ.
    /// </summary>
    private const string MaBamBanNhung = "c6a8f70c2786cb6204ae320f902e30b886e6f78af233ff829df401102b3d6f54";

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    private const string DanhMucNho = """
        {
          "2PN-1WC": [
            { "unitCode": "2PN-1WC-D301", "block": "D", "floor": 3 },
            { "unitCode": "2PN-1WC-D302", "block": "D", "floor": 3 }
          ],
          "1PN-1WC": [
            { "unitCode": "1PN-1WC-D312", "block": "D", "floor": 3 }
          ]
        }
        """;

    // ── AC1: danh mục nhúng sẵn, hiện tổng số căn và số căn theo từng loại ────────────────

    [Fact]
    public void AC1_DanhMucNhung_Co509Can()
    {
        Assert.Equal(509, EmbeddedUnitCatalog.Value.Total);
    }

    [Theory]
    [InlineData("1PN-1WC", 30)]
    [InlineData("2PN-1WC", 220)]
    [InlineData("2PN-2WC", 210)]
    [InlineData("3PN-2WC", 30)]
    [InlineData("TANG1-2", 19)]
    public void AC1_DanhMucNhung_DuSoCanTungLoai(string maLoai, int soCan)
    {
        var loai = Assert.Single(EmbeddedUnitCatalog.Value.Types, t => t.TypeCode == maLoai);

        Assert.Equal(soCan, loai.UnitCodes.Count);
    }

    [Fact]
    public void AC1_DanhMucNhung_DuNamLoai_VaKhongCanNaoTrungMa()
    {
        var danhMuc = EmbeddedUnitCatalog.Value;
        var ma = danhMuc.Types.SelectMany(t => t.UnitCodes).ToList();

        Assert.Equal(5, danhMuc.Types.Count);
        Assert.Equal(ma.Count, ma.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AC1_DanhMucNhung_ThuTuLoai_TatDinh_KhongPhuThuocThuTuKhoaTrongFile()
    {
        var maLoai = EmbeddedUnitCatalog.Value.Types.Select(t => t.TypeCode).ToList();

        Assert.Equal(maLoai.OrderBy(m => m, StringComparer.Ordinal), maLoai);
    }

    // ── AC2: hiện mã băm của bản danh mục đang dùng ───────────────────────────────────────

    [Fact]
    public void AC2_MaBamBanNhung_DungBangGiaTriDaGhim()
    {
        Assert.Equal(MaBamBanNhung, EmbeddedUnitCatalog.Value.Sha256);
    }

    /// <summary>
    /// Mã băm phải là băm trên <b>đúng byte của file</b> — có vậy người kiểm mới đối chiếu được
    /// bằng <c>sha256sum</c> mà không cần tin công cụ.
    /// </summary>
    [Fact]
    public void AC2_MaBamBanNhung_LaSha256CuaDungFileTrongRepo()
    {
        var file = File.ReadAllBytes(RepoPaths.Src("Noxh.XacMinh.Core", "Units", "apartment-units.json"));

        Assert.Equal(Hex.Sha256Hex(file), EmbeddedUnitCatalog.Value.Sha256);
    }

    [Fact]
    public void AC2_MaBam_CuaBanNguoiDungNap_LaSha256CuaDungByteHoNap()
    {
        var noiDung = Bytes(DanhMucNho);

        var ketQua = UnitCatalogJson.Parse(noiDung);

        Assert.True(ketQua.Success, ketQua.ErrorMessage);
        Assert.Equal(Hex.Sha256Hex(noiDung), ketQua.Catalog!.Sha256);
    }

    // ── AC3: nạp file danh mục khác đè được ───────────────────────────────────────────────

    [Fact]
    public void AC3_NapFileKhac_RaSoLieuCuaFileDo_ChuKhongPhaiBanNhung()
    {
        var ketQua = UnitCatalogJson.Parse(Bytes(DanhMucNho));

        Assert.True(ketQua.Success, ketQua.ErrorMessage);
        Assert.Equal(3, ketQua.Catalog!.Total);
        Assert.Equal(["1PN-1WC", "2PN-1WC"], ketQua.Catalog.Types.Select(t => t.TypeCode));
        Assert.NotEqual(EmbeddedUnitCatalog.Value.Sha256, ketQua.Catalog.Sha256);
    }

    [Fact]
    public void AC3_NapLaiChinhFileNhung_RaDungBanNhung()
    {
        var ketQua = UnitCatalogJson.Parse(EmbeddedUnitCatalog.Bytes());

        Assert.True(ketQua.Success, ketQua.ErrorMessage);
        Assert.Equal(EmbeddedUnitCatalog.Value.Sha256, ketQua.Catalog!.Sha256);
        Assert.Equal(EmbeddedUnitCatalog.Value.Total, ketQua.Catalog.Total);
    }

    /// <summary>BOM lọt vào khi file được ghi lại bằng Notepad/Excel — không phải file hỏng.</summary>
    [Fact]
    public void AC3_FileCoBOM_VanNapDuoc()
    {
        var ketQua = UnitCatalogJson.Parse([0xEF, 0xBB, 0xBF, .. Bytes(DanhMucNho)]);

        Assert.True(ketQua.Success, ketQua.ErrorMessage);
        Assert.Equal(3, ketQua.Catalog!.Total);
    }

    // ── AC5: file sai định dạng cho thông báo dễ hiểu, không ném ngoại lệ ─────────────────

    [Fact]
    public void AC5_FileRong_ChoThongBao_KhongNemNgoaiLe()
    {
        var ketQua = UnitCatalogJson.Parse([]);

        Assert.False(ketQua.Success);
        Assert.False(string.IsNullOrWhiteSpace(ketQua.ErrorMessage));
    }

    [Fact]
    public void AC5_KhongPhaiJson_NoiRoLaKhongPhaiJson()
    {
        var ketQua = UnitCatalogJson.Parse(Bytes("<html>404 Not Found</html>"));

        Assert.False(ketQua.Success);
        Assert.Contains("JSON", ketQua.ErrorMessage);
    }

    [Fact]
    public void AC5_JsonHopLeNhungKhongPhaiDanhMuc_NoiRoSaiCauTruc()
    {
        var ketQua = UnitCatalogJson.Parse(Bytes("""["2PN-1WC-D301","2PN-1WC-D302"]"""));

        Assert.False(ketQua.Success);
        Assert.Contains("danh mục", ketQua.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC5_KhongCoLoaiCanNao_ChoThongBao()
    {
        var ketQua = UnitCatalogJson.Parse(Bytes("{}"));

        Assert.False(ketQua.Success);
        Assert.Contains("loại căn", ketQua.ErrorMessage);
    }

    [Fact]
    public void AC5_LoaiCanRong_ChoThongBaoKemTenLoai()
    {
        var ketQua = UnitCatalogJson.Parse(Bytes("""{"2PN-1WC": []}"""));

        Assert.False(ketQua.Success);
        Assert.Contains("2PN-1WC", ketQua.ErrorMessage);
    }

    [Fact]
    public void AC5_LoaiCanKhongPhaiDanhSach_ChoThongBaoKemTenLoai()
    {
        var ketQua = UnitCatalogJson.Parse(Bytes("""{"2PN-1WC": 220}"""));

        Assert.False(ketQua.Success);
        Assert.Contains("2PN-1WC", ketQua.ErrorMessage);
    }

    [Fact]
    public void AC5_ThieuMaCan_ChoThongBaoChiRaChoThieu()
    {
        var ketQua = UnitCatalogJson.Parse(Bytes("""{"2PN-1WC": [{"block": "D", "floor": 3}]}"""));

        Assert.False(ketQua.Success);
        Assert.Contains("unitCode", ketQua.ErrorMessage);
    }

    /// <summary>
    /// Mã căn trùng là danh mục hỏng chứ không phải danh mục lạ: quỹ căn trùng mã thì phép tái lập
    /// vòng sau chọn căn kiểu gì cũng sai, nên chặn ngay lúc nạp.
    /// </summary>
    [Fact]
    public void AC5_MaCanTrung_ChoThongBaoKemMaTrung()
    {
        var trung = """
            {
              "2PN-1WC": [{ "unitCode": "2PN-1WC-D301" }],
              "1PN-1WC": [{ "unitCode": "2PN-1WC-D301" }]
            }
            """;

        var ketQua = UnitCatalogJson.Parse(Bytes(trung));

        Assert.False(ketQua.Success);
        Assert.Contains("2PN-1WC-D301", ketQua.ErrorMessage);
    }

    [Fact]
    public void AC5_MaLoaiTrung_ChoThongBaoKemMaLoai()
    {
        var trung = """
            {
              "2PN-1WC": [{ "unitCode": "2PN-1WC-D301" }],
              "2PN-1WC": [{ "unitCode": "2PN-1WC-D302" }]
            }
            """;

        var ketQua = UnitCatalogJson.Parse(Bytes(trung));

        Assert.False(ketQua.Success);
        Assert.Contains("2PN-1WC", ketQua.ErrorMessage);
    }

    [Fact]
    public void AC5_MoiKieuRac_DeuRaThongBao_KhongNemNgoaiLe()
    {
        string[] rac =
        [
            "null", "   ", "{", "[]", "123", "\"chuỗi\"",
            """{"2PN-1WC": [null]}""",
            """{"2PN-1WC": ["2PN-1WC-D301"]}""",
            """{"2PN-1WC": [{"unitCode": ""}]}""",
            """{"2PN-1WC": [{"unitCode": "   "}]}""",
            """{"2PN-1WC": [{"unitCode": 301}]}""",
        ];

        Assert.All(rac, json =>
        {
            var ketQua = UnitCatalogJson.Parse(Bytes(json));

            Assert.False(ketQua.Success, $"'{json}' không được coi là danh mục hợp lệ.");
            Assert.False(string.IsNullOrWhiteSpace(ketQua.ErrorMessage), $"'{json}' thiếu thông báo lỗi.");
        });
    }

    /// <summary>Giá trị lạ đi vào thông báo phải được cắt như mọi giá trị khác, kẻo file bị sửa nhồi
    /// một chuỗi dài làm vỡ màn hình.</summary>
    [Fact]
    public void AC5_TenLoaiDaiBatThuong_ThongBaoVanNganGon()
    {
        var dai = new string('X', 5000);

        var ketQua = UnitCatalogJson.Parse(Bytes($$"""{"{{dai}}": []}"""));

        Assert.False(ketQua.Success);
        Assert.True(ketQua.ErrorMessage!.Length < 300, "Thông báo lỗi không được dài như file bị sửa.");
    }
}
