using System.Text;
using Noxh.XacMinh.Core.DanhSach;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #17 — thả thẳng file Excel gốc thay cho việc copy-dán bảng. Đi qua đúng seam
/// <see cref="Verifier.Verify"/>: byte của file vào lõi dưới dạng dữ liệu, lõi không mở file trên
/// đĩa cũng không gọi mạng — vỏ giao diện đọc byte rồi đưa vào.
///
/// Bảng chuẩn vàng ở đây là bảng đã khoá thật của dự án trong fixture, nên "đọc file ra đúng mã băm
/// đã ghim" là một khẳng định về backend, không phải về chính test.
/// </summary>
public class DanhSachHoSoExcelVerificationTests
{
    private const string TenFile = "danh-sach-goc.xlsx";

    private static readonly string KhoaDung = GoldenFixture.DanhSachKhoaHex();

    private static CheckResult HangMuc(DanhSachDauVao danhSach)
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);

        return Verifier.Verify(new VerificationInput(nap.Report!, DanhSach: danhSach)).Items
            .Single(i => i.Id == CheckIds.DanhSachHoSo);
    }

    private static CheckResult KiemFile(byte[] noiDung, string? khoa = null) =>
        HangMuc(new DanhSachDauVao(new NguonBang.Excel(TenFile, noiDung), khoa ?? KhoaDung));

    private static CheckResult KiemDan(string bang, string? khoa = null) =>
        HangMuc(new DanhSachDauVao(bang, khoa ?? KhoaDung));

    /// <summary>Bảng chuẩn vàng tách thành ô; dòng 0 là dòng tiêu đề.</summary>
    private static List<string[]> OChuanVang() =>
        GoldenFixture.DanhSach()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(dong => dong.Split('\t'))
            .ToList();

    /// <summary>File Excel gốc mang đúng nội dung bảng chuẩn vàng, mỗi ô đi qua <paramref name="suaO"/>.</summary>
    private static byte[] FileChuanVang(Func<string, string>? suaO = null) =>
        DungFileExcel.Tu(OChuanVang()
            .Select(dong => (IReadOnlyList<object?>)dong.Select(o => (object?)(suaO is null ? o : suaO(o))).ToList())
            .ToList());

    // ── AC1: thả file Excel gốc cho ra kết quả GIỐNG HỆT khi dán bảng cùng nội dung ───────

    [Fact]
    public void AC1_ThaFileExcel_ChoKetQuaGiongHetKhiDanBangCungNoiDung()
    {
        var tuFile = KiemFile(FileChuanVang());
        var tuDan = KiemDan(GoldenFixture.DanhSach());

        Assert.Equal(tuDan.Status, tuFile.Status);
        Assert.Equal(tuDan.Expected, tuFile.Expected);
        Assert.Equal(tuDan.Actual, tuFile.Actual);
    }

    [Fact]
    public void AC1_FileExcelGoc_RaDungMaBamDanhSachBackendDaGhim()
    {
        var ketQua = KiemFile(FileChuanVang());

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
        Assert.Equal(GoldenFixture.DanhSachListHash(), ketQua.Actual);
    }

    /// <summary>Số hồ sơ và cơ cấu nhóm phải hiện ra y như đường dán — đây là bộ số đối chiếu biên bản.</summary>
    [Fact]
    public void AC1_FileExcelGoc_VanHienSoHoSoVaCoCauNhom()
    {
        var ketQua = KiemFile(FileChuanVang());

        Assert.Contains(ketQua.Metrics, m => m.Label.Contains("Số hồ sơ đọc được") && m.Value == "40");
        Assert.Contains(ketQua.Metrics, m => m.Label.Contains("U6") && m.Value == "28");
        Assert.Contains("40 hồ sơ", ketQua.Explanation, StringComparison.Ordinal);
    }

    /// <summary>Đọc từ file hay từ bảng dán là hai chuyện khác nhau — kết quả phải nói ra đọc từ đâu.</summary>
    [Fact]
    public void AC1_KetQuaNoiRoDocTuFileNao()
    {
        var ketQua = KiemFile(FileChuanVang());

        Assert.Contains(ketQua.Metrics, m => m.Label.Contains("Nguồn") && m.Value.Contains(TenFile, StringComparison.Ordinal));
        Assert.Contains(TenFile, ketQua.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC1_FileExcelGocNhungKhoaSai_KhongDat()
    {
        Assert.Equal(CheckStatus.KhongDat, KiemFile(FileChuanVang(), khoa: new string('0', 64)).Status);
    }

    // ── AC2: nhận cột theo TÊN tiêu đề, không phụ thuộc thứ tự cột ────────────────────────

    [Fact]
    public void AC2_DoiThuTuCotVaThemCotLa_VanRaDungMaBam()
    {
        // Tiêu đề đổi sang đúng chữ file nhập của hệ thống, cột xáo thứ tự, chèn thêm hai cột hệ
        // thống có mà mã băm không dùng.
        var goc = OChuanVang();
        var dong = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Nhóm đối tượng", "Số điện thoại", "Họ và tên", "Ghi chú", "Mã hồ sơ", "CCCD" },
        };

        dong.AddRange(goc.Skip(1).Select(o => new object?[]
        {
            o[3], "0901234567", o[1], "không có", o[0], o[2],
        }));

        var ketQua = KiemFile(DungFileExcel.Tu(dong));

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
        Assert.Equal(GoldenFixture.DanhSachListHash(), ketQua.Actual);
    }

    /// <summary>File thật hay có mấy dòng trống ở đầu; dòng CÓ DỮ LIỆU đầu tiên mới là tiêu đề.</summary>
    [Fact]
    public void AC2_TieuDeKhongNamODongDauFile_VanNhanDuocCot()
    {
        var dong = new List<IReadOnlyList<object?>> { new object?[] { null }, new object?[] { null } };
        dong.AddRange(OChuanVang().Select(o => (IReadOnlyList<object?>)o.Cast<object?>().ToList()));

        Assert.Equal(CheckStatus.Dat, KiemFile(DungFileExcel.Tu(dong)).Status);
    }

    // ── AC3: thiếu cột bắt buộc ⇒ nói rõ thiếu cột nào ───────────────────────────────────

    [Fact]
    public void AC3_ThieuCotHoTen_NoiRoThieuCotNao()
    {
        var dong = OChuanVang()
            .Select(o => (IReadOnlyList<object?>)new object?[] { o[0], o[2], o[3] })
            .ToList();

        var ketQua = KiemFile(DungFileExcel.Tu(dong));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("thiếu cột bắt buộc", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("họ tên", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
        // Cột có mặt thì không được kể tên trong lời than.
        Assert.DoesNotContain("mã hồ sơ»", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC3_ThieuNhieuCot_KeDuTenTungCot()
    {
        var dong = OChuanVang()
            .Select(o => (IReadOnlyList<object?>)new object?[] { o[0] })
            .ToList();

        var ketQua = KiemFile(DungFileExcel.Tu(dong));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("họ tên", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("số định danh", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nhóm đối tượng", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Tiêu đề lạ hoàn toàn: không đoán bừa theo thứ tự cột, vì đoán sai thì mã băm lệch
    /// mà chẳng ai biết vì sao.</summary>
    [Fact]
    public void AC3_TieuDeKhongNhanRa_KhongKiemDuoc_ChuKhongDoanTheoThuTuCot()
    {
        var dong = OChuanVang()
            .Select((o, i) => (IReadOnlyList<object?>)(i == 0 ? ["Cột 1", "Cột 2", "Cột 3", "Cột 4"] : o.Cast<object?>().ToArray()))
            .ToList();

        var ketQua = KiemFile(DungFileExcel.Tu(dong));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("thiếu cột bắt buộc", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC4: ô thừa khoảng trắng + họ tên ghi dạng tổ hợp dấu vẫn cho kết quả đúng ────────

    /// <summary>
    /// Hệ thống <c>Trim()</c> từng ô lúc nhập, nên khoảng trắng thừa trong file gốc KHÔNG nằm trong
    /// mã băm đã ghim. Cắt ở đây không phải công cụ tự sửa dữ liệu — đó là đọc đúng thứ đã được ghi.
    /// </summary>
    [Fact]
    public void AC4_OThuaKhoangTrang_VanRaDungMaBamDaGhim()
    {
        var ketQua = KiemFile(FileChuanVang(o => $"  {o}\t "));

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
        Assert.Equal(GoldenFixture.DanhSachListHash(), ketQua.Actual);
    }

    /// <summary>
    /// Dấu tiếng Việt ghi kiểu tổ hợp rời thì file gốc và bản đã khoá là hai chuỗi khác nhau thật —
    /// điều phải đúng ở đây là công cụ đọc file ra ĐÚNG chuỗi trong file (không tự chuẩn hoá), rồi
    /// nói được nguyên nhân lệch, y hệt khi dán bảng.
    /// </summary>
    [Fact]
    public void AC4_HoTenGhiDangToHopDauVaThuaKhoangTrang_DocRaDungNhuDanBangCungNoiDung()
    {
        var toHopRoi = FileChuanVang(o => $" {o.Normalize(NormalizationForm.FormD)} ");
        var tuFile = KiemFile(toHopRoi);
        var tuDan = KiemDan(GoldenFixture.DanhSach().Normalize(NormalizationForm.FormD));

        Assert.Equal(tuDan.Actual, tuFile.Actual);
        Assert.Equal(CheckStatus.KhongKiemDuoc, tuFile.Status);
        Assert.Contains("dấu tiếng Việt", tuFile.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC5: đường dán bảng vẫn dùng được như cũ ─────────────────────────────────────────

    [Fact]
    public void AC5_DuongDanBang_VanDatVaVanKhongCatKhoangTrangGiuaChung()
    {
        Assert.Equal(CheckStatus.Dat, KiemDan(GoldenFixture.DanhSach()).Status);

        // Đường dán KHÔNG được học thói cắt khoảng trắng của đường file: người dán không biết chuỗi
        // gốc là gì, công cụ cắt hộ là công cụ tự sửa dữ liệu cho khớp.
        var thuaKhoangTrang = KiemDan(GoldenFixture.DanhSach().Replace("\tU1\n", " \tU1\n", StringComparison.Ordinal));

        Assert.Equal(CheckStatus.KhongKiemDuoc, thuaKhoangTrang.Status);
        Assert.Contains("khớp nếu", thuaKhoangTrang.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    // ── File hỏng / dòng hỏng: chưa kiểm được, và nói ra chỗ hỏng ────────────────────────

    [Fact]
    public void FileKhongDocDuoc_KhongKiemDuoc_VaChiRaConDuongDanBang()
    {
        var ketQua = KiemFile(Encoding.UTF8.GetBytes("đây không phải file Excel"));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains(".xlsx", ketQua.Explanation, StringComparison.Ordinal);
        Assert.Contains("dán bảng", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DongKhongDocDuocNhom_ChiRoDUNGSoDongTrongFile()
    {
        var dong = OChuanVang()
            .Select((o, i) => (IReadOnlyList<object?>)(i == 8 ? [o[0], o[1], o[2], "nhóm lạ"] : o.Cast<object?>().ToArray()))
            .ToList();

        // Chèn hai dòng trống lên đầu: số dòng báo ra phải là số dòng THẬT trong file, không phải
        // thứ tự hồ sơ — người kiểm mở Excel ra là nhảy thẳng tới đúng dòng đó.
        dong.InsertRange(0, [new object?[] { null }, new object?[] { null }]);

        var ketQua = KiemFile(DungFileExcel.Tu(dong));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("dòng 11", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Dòng trống xen giữa danh sách là chuyện thường của file thật — bỏ, không thành lỗi.</summary>
    [Fact]
    public void DongTrongXenGiua_BiBoQua_VanRaDungMaBam()
    {
        var dong = OChuanVang()
            .Select(o => (IReadOnlyList<object?>)o.Cast<object?>().ToList())
            .ToList();

        dong.Insert(5, [null, null, null, null]);
        dong.Insert(20, [null, null, null, null]);

        Assert.Equal(GoldenFixture.DanhSachListHash(), KiemFile(DungFileExcel.Tu(dong)).Actual);
    }

    /// <summary>
    /// Lệch mã băm khi thả file gốc có một nguyên nhân riêng mà đường dán không có: hệ thống loại
    /// bớt dòng ngay lúc nhập. Không nói ra thì người kiểm đi tìm sai chỗ.
    /// </summary>
    [Fact]
    public void LechMaBamTuFileGoc_NoiRaKhaNangHeThongDaLoaiBotDongLucNhap()
    {
        var dong = OChuanVang()
            .Select(o => (IReadOnlyList<object?>)o.Cast<object?>().ToList())
            .ToList();

        dong.Add(["HS999", "Người bị loại lúc nhập", "12345", "U6"]);

        var ketQua = KiemFile(DungFileExcel.Tu(dong));

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains("loại", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("biên bản khoá danh sách", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Họ tên trong file là dữ liệu cá nhân — kết quả kiểm không được mang nó ra ngoài.</summary>
    [Fact]
    public void KetQuaTuFileExcel_KhongMangTheoHoTen_VaKhongMangChuoiDemBam()
    {
        var ketQua = KiemFile(FileChuanVang());

        Assert.Null(ketQua.Preimage);

        foreach (var giaTri in new[] { ketQua.Explanation, ketQua.Expected, ketQua.Actual }
                     .Concat(ketQua.Metrics.Select(m => m.Value)))
            Assert.DoesNotContain("Vũ Hữu Mai", giaTri ?? string.Empty, StringComparison.Ordinal);
    }
}
