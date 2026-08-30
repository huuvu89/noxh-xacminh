using System.Text;
using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Gói trail: kho bằng chứng tải sẵn bằng script rồi nạp vào công cụ — đường thứ hai, có vì trình
/// duyệt chỉ đọc được kho khi chính kho bật CORS, và vì bản offline vốn không có mạng.
///
/// Điều các test này canh, và cũng là chỗ đường này dễ trở thành đồ trang trí nhất: <b>công cụ
/// không được tin lời khai của gói</b>. Danh sách lô suy ra từ XML nguyên văn, mã băm tính lại trên
/// đúng byte trong gói, thiếu byte thì nói là chưa kiểm được — chứ không phải "kho giấu lô".
/// </summary>
public class GoiTrailTests
{
    private const string MaTienTrinh = "a1b2c3d4";

    private static readonly DateTime BatDau = new(2026, 8, 15, 7, 30, 0, DateTimeKind.Utc);

    /// <summary>Chuỗi lô móc xích đúng khuôn backend đẩy lên: mỗi lô trỏ về lô liền trước.</summary>
    private static List<DoiTuongKho> Chuoi(params long[] soLo)
    {
        var doiTuong = new List<DoiTuongKho>();
        string? keyTruoc = null;
        string? shaTruoc = null;

        foreach (var so in soLo)
        {
            var luc = BatDau.AddSeconds(2 * so);
            var noiDung = DungLoTrail.NoiDung(
                so, MaTienTrinh, luc, [DungLoTrail.VeDaBoc($"HS{so:D3}", (int)so)], keyTruoc, shaTruoc);

            keyTruoc = DungLoTrail.Key(so, MaTienTrinh, luc);
            shaTruoc = DungLoTrail.Sha256Hex(noiDung);
            doiTuong.Add(new DoiTuongKho(keyTruoc, noiDung));
        }

        return doiTuong;
    }

    private static CheckResult ChuoiMocXich(KhoBangChung kho)
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);

        return Verifier.Verify(new VerificationInput(nap.Report!, Kho: kho))
            .Items.Single(i => i.Id == CheckIds.TrailChuoiLo);
    }

    // ── Bóc được gói đầy đủ ─────────────────────────────────────────────────────────────────

    [Fact]
    public void GoiDayDu_BocRaDuLoVaKhaiDaLietKeHet()
    {
        var lo = Chuoi(1, 2, 3);

        var kho = DocGoiTrail.Doc(DungGoiTrail.Goi(lo), "goi-trail.zip");

        Assert.Null(kho.Loi);
        Assert.True(kho.DaLietKeHet);
        Assert.Equal(3, kho.Lo.Count);
        Assert.All(kho.Lo, l => Assert.Null(l.Loi));
        Assert.Equal([1L, 2L, 3L], kho.Lo.Select(l => l.SoLo));
    }

    [Fact]
    public void GoiDayDu_ChuoiMocXichKiemDuocQuaDungSeamCuaLoi()
    {
        var kho = DocGoiTrail.Doc(DungGoiTrail.Goi(Chuoi(1, 2, 3)), "goi-trail.zip");

        Assert.Equal(CheckStatus.Dat, ChuoiMocXich(kho).Status);
    }

    /// <summary>
    /// Mệnh đề đắt nhất của cả đường này: mã băm KHÔNG lấy theo lời khai của script mà tính lại
    /// trên byte trong gói — nên sửa một byte là chuỗi móc xích tố cáo ngay. Mất mệnh đề này thì
    /// gói chỉ còn là lời khai, và cả hạng mục trail thành đồ trang trí.
    /// </summary>
    [Fact]
    public void SuaMotByteTrongGoi_ChuoiMocXichLoRaNgay()
    {
        var lo = Chuoi(1, 2, 3);

        // Sửa đúng một ký tự trong nội dung lô đầu, và sửa sao cho lô vẫn là JSONL hợp lệ — ca khó
        // chịu nhất: gói trông lành lặn, chỉ mã băm là khác.
        var da = Encoding.UTF8.GetString(lo[0].NoiDung).Replace("HS001", "HS009", StringComparison.Ordinal);
        lo[0] = lo[0] with { NoiDung = Encoding.UTF8.GetBytes(da) };

        var kho = DocGoiTrail.Doc(DungGoiTrail.Goi(lo), "goi-trail.zip");

        Assert.Equal(CheckStatus.KhongDat, ChuoiMocXich(kho).Status);
    }

    [Fact]
    public void CheDoLuonLaGoiNhapTay_VaNoiRoDayKhongPhaiTrinhDuyetDocKho()
    {
        var kho = DocGoiTrail.Doc(DungGoiTrail.Goi(Chuoi(1)), "goi-trail.zip");

        Assert.Equal(CheDoDocKho.GoiNhapTay, kho.CheDo);
        Assert.Contains("KHÔNG phải trình duyệt này đọc thẳng kho", kho.MoTaCheDo, StringComparison.Ordinal);
    }

    // ── Gói thiếu, gói dở: chưa kiểm được, KHÔNG phải buộc tội ai ────────────────────────────

    [Fact]
    public void GoiThieuNoiDungMotLo_LoDoLaChuaDocDuoc_ChuKhongPhaiBiGiau()
    {
        var lo = Chuoi(1, 2, 3);
        var thieu = DungGoiTrail.Goi([DungGoiTrail.TrangDanhSach(lo.Select(l => l.Key))], lo.Skip(1).ToList());

        var kho = DocGoiTrail.Doc(thieu, "goi-trail.zip");

        Assert.Null(kho.Loi);
        Assert.Equal(3, kho.Lo.Count);
        var hong = Assert.Single(kho.Lo, l => l.Loi is not null);
        Assert.Equal(lo[0].Key, hong.Key);
        Assert.Contains("gói không chứa nội dung lô này", hong.Loi!, StringComparison.Ordinal);

        // Lô trong danh sách mà gói không chở nội dung: mắt xích đó chưa kiểm được, và "chưa kiểm
        // được" không bao giờ được biến thành lời buộc tội.
        Assert.Equal(CheckStatus.KhongKiemDuoc, ChuoiMocXich(kho).Status);
    }

    [Fact]
    public void TrangCuoiVanConDauTiepTuc_KhaiLaChuaLietKeHet()
    {
        var lo = Chuoi(1, 2);
        var goi = DungGoiTrail.Goi([DungGoiTrail.TrangDanhSach(lo.Select(l => l.Key), "con-nua")], lo);

        var kho = DocGoiTrail.Doc(goi, "goi-trail.zip");

        Assert.False(kho.DaLietKeHet);
        Assert.Equal(2, kho.Lo.Count);
    }

    [Fact]
    public void GoiGhepTuNhieuLanLietKeRoiRac_KhaiLaChuaLietKeHet()
    {
        var lo = Chuoi(1, 2);

        // Trang đầu đã khai hết mà vẫn còn trang sau: hai lần liệt kê rời rạc ghép vào một gói,
        // không phải một lần phân trang liền mạch.
        var goi = DungGoiTrail.Goi(
            [DungGoiTrail.TrangDanhSach([lo[0].Key]), DungGoiTrail.TrangDanhSach([lo[1].Key])], lo);

        var kho = DocGoiTrail.Doc(goi, "goi-trail.zip");

        Assert.False(kho.DaLietKeHet);
        Assert.Equal(2, kho.Lo.Count);
    }

    [Fact]
    public void PhanTrangDuHaiTrang_VanLaDaLietKeHet()
    {
        var lo = Chuoi(1, 2, 3);
        var goi = DungGoiTrail.Goi(
            [DungGoiTrail.TrangDanhSach(lo.Take(2).Select(l => l.Key), "tok2"),
             DungGoiTrail.TrangDanhSach([lo[2].Key])],
            lo);

        var kho = DocGoiTrail.Doc(goi, "goi-trail.zip");

        Assert.True(kho.DaLietKeHet);
        Assert.Equal(3, kho.Lo.Count);
    }

    // ── Gói hỏng: nói được vì sao hỏng ──────────────────────────────────────────────────────

    [Fact]
    public void TrangDanhSachLaLoiTuChoiCuaKho_GiuNguyenMaLoiDo()
    {
        var goi = DungGoiTrail.Goi([DungGoiTrail.TrangTuChoi("AccessDenied")], []);

        var kho = DocGoiTrail.Doc(goi, "goi-trail.zip");

        Assert.NotNull(kho.Loi);
        Assert.Contains("AccessDenied", kho.Loi!, StringComparison.Ordinal);
        Assert.Empty(kho.Lo);
    }

    [Fact]
    public void FileKhongPhaiZip_HongCoLoiChuKhongNemNgoaiLe()
    {
        var kho = DocGoiTrail.Doc(Encoding.UTF8.GetBytes("đây không phải file zip"), "nham.zip");

        Assert.NotNull(kho.Loi);
        Assert.Contains("không mở được gói", kho.Loi!, StringComparison.Ordinal);
        Assert.Equal(CheDoDocKho.GoiNhapTay, kho.CheDo);
    }

    [Fact]
    public void GoiKhongCoTrangDanhSach_Hong_ViKhongBietKhoKhaiCoNhungLoNao()
    {
        var goi = DungGoiTrail.Goi([], Chuoi(1, 2));

        var kho = DocGoiTrail.Doc(goi, "goi-trail.zip");

        Assert.NotNull(kho.Loi);
        Assert.Contains("không có trang danh sách", kho.Loi!, StringComparison.Ordinal);
    }

    // ── manifest chỉ là lời khai để hiển thị ────────────────────────────────────────────────

    [Fact]
    public void CoManifest_HienDiaChiKhoKemChuTheoGoiKhai()
    {
        var kho = DocGoiTrail.Doc(DungGoiTrail.Goi(Chuoi(1)), "goi-trail.zip");

        Assert.Contains("s3.thu-nghiem.vn/bang-chung/trail/", kho.MoTaNguon!, StringComparison.Ordinal);
        Assert.Contains("theo gói khai", kho.MoTaNguon!, StringComparison.Ordinal);
    }

    [Fact]
    public void ThieuManifest_VanBocDuocGoi_ChiLaKhongBietDiaChiKho()
    {
        var kho = DocGoiTrail.Doc(DungGoiTrail.Goi(Chuoi(1, 2), manifest: null), "goi-trail.zip");

        Assert.Null(kho.Loi);
        Assert.Equal(2, kho.Lo.Count);
        Assert.Contains("không khai địa chỉ kho", kho.MoTaNguon!, StringComparison.Ordinal);
    }

    [Fact]
    public void ObjectKhongCoTrongDanhSach_KhongDuocDuaVaoKiem_NhungPhaiNeuRa()
    {
        var lo = Chuoi(1, 2);
        var goi = DungGoiTrail.Goi([DungGoiTrail.TrangDanhSach([lo[0].Key])], lo);

        var kho = DocGoiTrail.Doc(goi, "goi-trail.zip");

        Assert.Single(kho.Lo);
        Assert.Contains("không có trong trang danh sách", kho.MoTaNguon!, StringComparison.Ordinal);
        Assert.Contains(lo[1].Key, kho.MoTaNguon!, StringComparison.Ordinal);
    }
}
