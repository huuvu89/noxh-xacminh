using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #19 — đối chiếu từng bản ghi trên trail bằng chứng với báo cáo minh bạch. Đây là mỏ neo độc
/// lập thứ ba: nó bắt được đúng thứ mà chuỗi băm trong cơ sở dữ liệu không bắt được — dữ liệu bị sửa
/// trong khoảng thời gian <b>trước khi</b> chuỗi băm được vật chất hoá, vì bản đã lên kho chỉ-ghi thì
/// không sửa lại được nữa.
///
/// Ranh giới của bộ test này (nói thẳng để không ai đọc nhầm sức nặng của nó): trail trong fixture
/// <b>dựng lại</b> từ chính báo cáo chuẩn vàng, trừ hai chỗ lấy từ giá trị backend thật đã ghim ở
/// khối dấu thời gian — cam kết ngẫu nhiên máy chủ (preimage <c>FREEZE</c>) và đầu chuỗi băm từng
/// vòng (preimage <c>STEPCHAIN:{vòng}</c>). Nên ca ĐẠT của hạng mục lượt bốc mới chỉ chứng minh
/// đường ống chạy đúng, còn sức nặng thật nằm ở các ca lệch: sửa một bên thì công cụ phải nêu ra.
/// </summary>
public class TrailDoiChieuVerificationTests
{
    private static TransparencyReport Golden() => TrailGolden.Bao();

    /// <summary>Sửa JSON gốc bằng JsonNode — đúng thứ người dùng thả vào, không phải model đã nạp.</summary>
    private static TransparencyReport BaoCaoSua(Action<JsonObject> sua)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        sua(root);

        var nap = TransparencyJson.Parse(root.ToJsonString());
        Assert.True(nap.Success, nap.ErrorMessage);

        return nap.Report!;
    }

    private static JsonArray NhatKyJson(JsonObject root) => root["nhatKyBoc"]!.AsArray();

    private static CheckResult HangMuc(KhoBangChung? kho, string id, TransparencyReport? bao = null) =>
        Verifier.Verify(new VerificationInput(bao ?? Golden(), Kho: kho)).Items.Single(i => i.Id == id);

    private static CheckResult LuotBoc(KhoBangChung? kho, TransparencyReport? bao = null) =>
        HangMuc(kho, CheckIds.TrailLuotBoc, bao);

    private static CheckResult CamKet(KhoBangChung? kho, TransparencyReport? bao = null) =>
        HangMuc(kho, CheckIds.TrailCamKet, bao);

    private static CheckResult DauChuoi(KhoBangChung? kho, TransparencyReport? bao = null) =>
        HangMuc(kho, CheckIds.TrailDauChuoi, bao);

    private static KhoBangChung KhoDay() => TrailGolden.Doc();

    // ── Ca gốc: trail đầy đủ, báo cáo nguyên vẹn ────────────────────────────────────────────

    [Fact]
    public void TrailDayDuVaBaoCaoNguyenVen_CaBaHangMucDeuDat()
    {
        var kho = KhoDay();

        Assert.Equal(CheckStatus.Dat, LuotBoc(kho).Status);
        Assert.Equal(CheckStatus.Dat, CamKet(kho).Status);
        Assert.Equal(CheckStatus.Dat, DauChuoi(kho).Status);
    }

    // ── AC1: lượt bốc có ở một bên mà thiếu ở bên kia ───────────────────────────────────────

    [Fact]
    public void AC1_LuotBocCoTrenTrailNhungThieuTrongBaoCao_KhongDat_VaNeuDichLuotNao()
    {
        var boBot = TrailGolden.NhatKy().First(e => e.AutoDrawn != true);
        var bao = BaoCaoSua(root =>
        {
            var nhatKy = NhatKyJson(root);
            var thu = nhatKy.Single(e => e!["entryHash"]!.GetValue<string>() == boBot.EntryHash);
            nhatKy.Remove(thu);
        });

        var ketQua = LuotBoc(KhoDay(), bao);

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains(boBot.DeckId!, ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(boBot.Position!.Value.ToString(), ketQua.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    /// Chiều ngược lại nhẹ hơn hẳn: đẩy trail là best-effort (hàng đợi đầy thì máy chủ bỏ bản ghi,
    /// không chặn lượt bốc), nên "báo cáo có mà trail không có" phải là KHÔNG KIỂM ĐƯỢC. Biến nó
    /// thành KHÔNG ĐẠT là vu cho ban tổ chức vì một hàng đợi đầy.
    /// </summary>
    [Fact]
    public void AC1_LuotBocCoTrongBaoCaoNhungKhongCoTrenTrail_KhongKiemDuoc_ChuKhongVuOanKhongDat()
    {
        var thieu = TrailGolden.LuotBoc().Skip(1).ToList();

        var ketQua = LuotBoc(TrailGolden.Kho([.. thieu, TrailGolden.CamKet(), .. TrailGolden.DauChuoi()]));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("best-effort", ketQua.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    /// Vé do máy bốc thay (<c>autoDrawn</c>) sinh ở close-draw, không đi qua đường bốc vé nên KHÔNG
    /// bao giờ lên trail. Đòi nó có trên trail là biến một thiết kế đúng thành báo động giả.
    /// </summary>
    [Fact]
    public void AC1_VeDoMayBocThay_KhongCoTrenTrailLaBinhThuong_VanDat()
    {
        var mayBoc = TrailGolden.NhatKy().Where(e => e.AutoDrawn == true).ToList();
        Assert.NotEmpty(mayBoc);

        var ketQua = LuotBoc(KhoDay());

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
        Assert.Contains(ketQua.Metrics, m => m.Value == mayBoc.Count.ToString());
    }

    // ── AC2: có ở cả hai nơi nhưng khác nội dung ────────────────────────────────────────────

    [Fact]
    public void AC2_CungOPhieuNhungKhacNguoiBoc_NeuRoRang_KhongDat()
    {
        var buoc = TrailGolden.NhatKy().First(e => e.AutoDrawn != true);
        var nguoiKhac = TrailGolden.NhatKy().First(e => e.ApplicantId != buoc.ApplicantId).ApplicantId;

        var ketQua = LuotBoc(TrailGolden.Kho(TrailGolden.ThayLuotBoc(buoc, maHoSoMoi: nguoiKhac)));

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains(nguoiKhac!, ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC2_CungOPhieuNhungKhacNoiDungVe_KhongDat()
    {
        var buoc = TrailGolden.NhatKy().First(e => e.AutoDrawn != true && e.Payload == "TRUNG_QUYEN_MUA");

        var ketQua = LuotBoc(TrailGolden.Kho(TrailGolden.ThayLuotBoc(buoc, noiDungVeMoi: "KHONG_TRUNG_UU_TIEN")));

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains("KHONG_TRUNG_UU_TIEN", ketQua.Explanation, StringComparison.Ordinal);
        Assert.Contains("TRUNG_QUYEN_MUA", ketQua.Explanation, StringComparison.Ordinal);
    }

    /// <summary>Trail của dự án khác không làm chứng hộ — so nó với báo cáo này là so nhầm hai lễ.</summary>
    [Fact]
    public void AC2_TrailCuaDuAnKhac_KhongKiemDuoc_ChuKhongDoiChieuBua()
    {
        var kho = TrailGolden.Kho(TrailGolden.TatCaBanGhi(maDuAn: "99999999-9999-9999-9999-999999999999"));

        Assert.All(new[] { LuotBoc(kho), CamKet(kho), DauChuoi(kho) }, k =>
        {
            Assert.Equal(CheckStatus.KhongKiemDuoc, k.Status);
            Assert.Contains("mã dự án KHÁC", k.Explanation, StringComparison.Ordinal);
        });
    }

    // ── AC3: cam kết ngẫu nhiên máy chủ trên trail ↔ cam kết công bố ────────────────────────

    [Fact]
    public void AC3_BaoCaoDoiCamKetNgauNhienSauLe_KhongDat_VaNeuRoVongNao()
    {
        var bao = BaoCaoSua(root =>
        {
            var vongB = root["nguonNgauNhien"]!.AsArray().Single(n => n!["round"]!.GetValue<string>() == "B")!;
            vongB["rServerCommit"] = new string('a', 64);
        });

        var ketQua = CamKet(KhoDay(), bao);

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains("B", ketQua.Explanation, StringComparison.Ordinal);
        Assert.Contains(new string('a', 64), ketQua.Actual ?? ketQua.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_TrailKhongCoBanGhiCamKetNao_KhongKiemDuoc()
    {
        var kho = TrailGolden.Kho([.. TrailGolden.LuotBoc(), .. TrailGolden.DauChuoi()]);

        Assert.Equal(CheckStatus.KhongKiemDuoc, CamKet(kho).Status);
    }

    /// <summary>
    /// Chốt lại entropy là thao tác hợp lệ, nên lần chốt SAU CÙNG mới là lần ràng buộc dữ liệu đang
    /// công bố. Báo cáo khớp một lần chốt cũ thì phải nói thẳng ra là khớp lần nào.
    /// </summary>
    [Fact]
    public void AC3_ChotLaiEntropy_BaoCaoKhopLanChotCu_KhongDat_VaNoiRoKhopLanNao()
    {
        var chotSau = TrailGolden.CamKet(lanChot: 2, camKetBMoi: new string('b', 64));

        var ketQua = CamKet(TrailGolden.Kho([.. TrailGolden.LuotBoc(), TrailGolden.CamKet(), chotSau,
            .. TrailGolden.DauChuoi()]));

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains("chốt", ketQua.Explanation, StringComparison.Ordinal);
    }

    // ── AC4: đầu chuỗi băm từng vòng trên trail ↔ giá trị tính lại được ─────────────────────

    /// <summary>
    /// Đầu chuỗi trong fixture là giá trị backend thật đã ghim (preimage <c>STEPCHAIN:{vòng}</c>),
    /// còn giá trị đem so là chuỗi băm công cụ tính lại từ nhật ký bốc — hai đường độc lập, nên ca
    /// ĐẠT này nói lên điều gì đó thật.
    /// </summary>
    [Fact]
    public void AC4_DauChuoiTungVongKhopGiaTriTinhLaiDuoc_Dat_VaNeuSoVongDaDoiChieu()
    {
        var ketQua = DauChuoi(KhoDay());

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
        Assert.Contains("A1", ketQua.Actual ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(ketQua.Metrics, m => m.Label.Contains("Đầu chuỗi vòng A1", StringComparison.Ordinal));
    }

    [Fact]
    public void AC4_NhatKyBocBiSuaSauKhiDauChuoiLenKho_KhongDat_VaNeuRoVongLechDauTien()
    {
        var bao = BaoCaoSua(root =>
        {
            var buoc = NhatKyJson(root).First(e => e!["round"]!.GetValue<string>() == "A1")!;
            buoc["payload"] = "KHONG_TRUNG_UU_TIEN";
        });

        var ketQua = DauChuoi(KhoDay(), bao);

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains("A1", ketQua.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_TrailKhaiSoBuocKhacSoBuocTinhLaiDuoc_KhongDat()
    {
        var kho = TrailGolden.Kho([.. TrailGolden.LuotBoc(), TrailGolden.CamKet(),
            .. TrailGolden.DauChuoi(soBuocSaiCuaVong: "A1")]);

        var ketQua = DauChuoi(kho);

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains("A1", ketQua.Explanation, StringComparison.Ordinal);
        Assert.Contains("khai", ketQua.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    /// Đầu chuỗi là giá trị cộng dồn "tới hết vòng X". Nhật ký mang một nhãn vòng công cụ không biết
    /// thì ranh giới ấy không vạch được — kết luận lệch lúc đó chỉ phản ánh việc công cụ không hiểu
    /// nhãn vòng, nên phải dừng ở KHÔNG KIỂM ĐƯỢC.
    /// </summary>
    [Fact]
    public void AC4_NhatKyMangVongCongCuKhongBiet_KhongKiemDuoc_ChuKhongVuOanKhongDat()
    {
        var bao = BaoCaoSua(root => NhatKyJson(root)[0]!["round"] = "Z9");

        var ketQua = DauChuoi(KhoDay(), bao);

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("Z9", ketQua.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_TrailKhongCoBanGhiDauChuoiNao_KhongKiemDuoc()
    {
        var kho = TrailGolden.Kho([.. TrailGolden.LuotBoc(), TrailGolden.CamKet()]);

        Assert.Equal(CheckStatus.KhongKiemDuoc, DauChuoi(kho).Status);
    }

    [Fact]
    public void AC4_BaoCaoKhongCongBoNhatKyBoc_KhongKiemDuoc_ChuKhongPhaiKhongDat()
    {
        var bao = BaoCaoSua(root => root["nhatKyBoc"] = new JsonArray());

        Assert.Equal(CheckStatus.KhongKiemDuoc, DauChuoi(KhoDay(), bao).Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, LuotBoc(KhoDay(), bao).Status);
    }

    // ── AC5: giới hạn phải nói ở MỌI kết luận, kể cả kết luận đẹp nhất ──────────────────────

    [Fact]
    public void AC5_MoiKetLuanCuaBaHangMuc_DeuNoiRoTrailKhongThayThuChuaTungDuocDayLen()
    {
        var kho = KhoDay();
        var moiKetLuan = new List<CheckResult>
        {
            LuotBoc(kho), CamKet(kho), DauChuoi(kho),
            LuotBoc(null), CamKet(null), DauChuoi(null),
            LuotBoc(TrailGolden.Kho(TrailGolden.ThayLuotBoc(
                TrailGolden.NhatKy().First(e => e.AutoDrawn != true), noiDungVeMoi: "KHONG_TRUNG"))),
        };

        Assert.All(moiKetLuan, k =>
            Assert.Contains("chưa bao giờ được đẩy lên", k.Explanation, StringComparison.Ordinal));
    }

    // ── AC6: không có dữ liệu trail ⇒ KHÔNG KIỂM ĐƯỢC, không phải KHÔNG ĐẠT ────────────────

    [Fact]
    public void AC6_ChuaDocKho_CaBaHangMucDeuKhongKiemDuoc()
    {
        Assert.Equal(CheckStatus.KhongKiemDuoc, LuotBoc(null).Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, CamKet(null).Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, DauChuoi(null).Status);
    }

    [Fact]
    public void AC6_KhoTuChoiTruyCap_KhongKiemDuoc_VaNoiLaiLyDoChoNguoiKiem()
    {
        var kho = KhoBangChung.Hong(CheDoDocKho.KhoaChiDoc, "HTTP 403 AccessDenied", "s3.thu-nghiem.vn");

        Assert.All(new[] { LuotBoc(kho), CamKet(kho), DauChuoi(kho) }, k =>
        {
            Assert.Equal(CheckStatus.KhongKiemDuoc, k.Status);
            Assert.Contains("403", k.Explanation, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AC6_KhoDocDuocNhungKhongCoBanGhiNao_KhongKiemDuoc()
    {
        var kho = TrailGolden.Kho([]);

        Assert.Equal(CheckStatus.KhongKiemDuoc, LuotBoc(kho).Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, CamKet(kho).Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, DauChuoi(kho).Status);
    }

    /// <summary>
    /// Danh sách lô còn dở thì "trail thiếu lượt bốc này" chỉ là hệ quả của việc đọc thiếu — nhưng
    /// "trail CÓ mà báo cáo thiếu" vẫn kết luận được, vì bản ghi đó đã đọc được bằng mắt.
    /// </summary>
    [Fact]
    public void AC6_LietKeChuaHet_ThieuTrenTrailKhongKetLuan_NhungThieuTrongBaoCaoVanKetLuanDuoc()
    {
        var kho = TrailGolden.Kho(TrailGolden.TatCaBanGhi(), daLietKeHet: false);
        Assert.Equal(CheckStatus.KhongKiemDuoc, LuotBoc(kho).Status);
        Assert.Contains("còn dở", LuotBoc(kho).Explanation, StringComparison.Ordinal);

        var bao = BaoCaoSua(root =>
        {
            var nhatKy = NhatKyJson(root);
            nhatKy.Remove(nhatKy.First(e => e!["autoDrawn"]!.GetValue<bool>() == false));
        });

        Assert.Equal(CheckStatus.KhongDat, LuotBoc(kho, bao).Status);
    }

    /// <summary>Ba hạng mục mới không được làm đổi kết luận của bất kỳ hạng mục nào khác.</summary>
    [Fact]
    public void KhoHongHayKhongCoKho_CacHangMucKhacGiuNguyenKetLuan()
    {
        static List<CheckResult> KhongPhaiTrail(KhoBangChung? kho) =>
            Verifier.Verify(new VerificationInput(Golden(), Kho: kho))
                .Items.Where(i => !i.Id.StartsWith("trail-", StringComparison.Ordinal)).ToList();

        Assert.Equal(KhongPhaiTrail(null), KhongPhaiTrail(KhoDay()));
        Assert.Equal(KhongPhaiTrail(null), KhongPhaiTrail(KhoBangChung.Hong(CheDoDocKho.AnDanh, "CORS")));
    }
}
