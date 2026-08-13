using Noxh.XacMinh.Core.Kho;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #18 — AC5: "phần ký request là hàm thuần có test theo bộ vector chuẩn, không cần mạng".
///
/// Vector lấy từ tài liệu AWS công bố, không phải do công cụ tự sinh: bộ <c>aws-sig-v4-test-suite</c>
/// (<c>get-vanilla</c>) và hai ví dụ ký request S3 trong tài liệu "Signature Calculation Examples",
/// cộng thêm ví dụ dẫn xuất khoá ký. Ký sai một byte thì kho từ chối, mà lúc đó đang giữa lễ — nên
/// chỗ này phải bị ghim bằng số của bên ngoài, không phải bằng kết quả của chính nó.
/// </summary>
public class SigV4Tests
{
    /// <summary>Khoá ví dụ của bộ vector AWS — không phải khoá thật của ai cả.</summary>
    private static readonly KhoaKho KhoaBoVector =
        new("AKIDEXAMPLE", "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY");

    private static readonly KhoaKho KhoaViDuS3 =
        new("AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY");

    // ── AC5: bộ vector chuẩn AWS ────────────────────────────────────────────────────────────

    [Fact]
    public void AC5_VectorChuan_GetVanilla_RaDungChuoiUyQuyenAwsCongBo()
    {
        var yeu = new YeuCauKy(
            "GET",
            "/",
            [],
            [new TieuDeYeuCau("Host", "example.amazonaws.com"),
             new TieuDeYeuCau("X-Amz-Date", "20150830T123600Z")],
            SigV4.MaBamThanRong,
            new DateTimeOffset(2015, 8, 30, 12, 36, 0, TimeSpan.Zero),
            "us-east-1",
            "service");

        Assert.Equal(
            "AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20150830/us-east-1/service/aws4_request, "
            + "SignedHeaders=host;x-amz-date, "
            + "Signature=5fa00fa31553b73ebf1942676e86291e8372ff2a2260956d9b8aae1d763fbf31",
            SigV4.Ky(yeu, KhoaBoVector));
    }

    [Fact]
    public void AC5_VectorChuan_GetObjectS3_RaDungChuKyTaiLieuAwsCongBo()
    {
        var yeu = new YeuCauKy(
            "GET",
            "/test.txt",
            [],
            [new TieuDeYeuCau("Host", "examplebucket.s3.amazonaws.com"),
             new TieuDeYeuCau("Range", "bytes=0-9"),
             new TieuDeYeuCau("x-amz-content-sha256", SigV4.MaBamThanRong),
             new TieuDeYeuCau("x-amz-date", "20130524T000000Z")],
            SigV4.MaBamThanRong,
            new DateTimeOffset(2013, 5, 24, 0, 0, 0, TimeSpan.Zero),
            "us-east-1",
            "s3");

        Assert.Equal(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request, "
            + "SignedHeaders=host;range;x-amz-content-sha256;x-amz-date, "
            + "Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41",
            SigV4.Ky(yeu, KhoaViDuS3));
    }

    /// <summary>Vector có tham số truy vấn — đúng hình dạng của lệnh liệt kê lô mà công cụ dùng.</summary>
    [Fact]
    public void AC5_VectorChuan_GetBucketLifecycle_ThamSoTruyVanVaoDungChuoiChuanHoa()
    {
        var yeu = new YeuCauKy(
            "GET",
            "/",
            [new ThamSoTruyVan("lifecycle", string.Empty)],
            [new TieuDeYeuCau("Host", "examplebucket.s3.amazonaws.com"),
             new TieuDeYeuCau("x-amz-content-sha256", SigV4.MaBamThanRong),
             new TieuDeYeuCau("x-amz-date", "20130524T000000Z")],
            SigV4.MaBamThanRong,
            new DateTimeOffset(2013, 5, 24, 0, 0, 0, TimeSpan.Zero),
            "us-east-1",
            "s3");

        Assert.Equal(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request, "
            + "SignedHeaders=host;x-amz-content-sha256;x-amz-date, "
            + "Signature=fea454ca298b7da1c68078a5d1bdbfbbe0d65c699e0f91ac7a200a0136783543",
            SigV4.Ky(yeu, KhoaViDuS3));
    }

    [Fact]
    public void AC5_VectorChuan_DanXuatKhoaKy_RaDungKhoaTaiLieuAwsCongBo()
    {
        Assert.Equal(
            "f4780e2d9f65fa895f9c67b32ce1baf0b0d8a43505a000a1a9e090d414db404d",
            SigV4.KhoaKyHex("wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY", "20120215", "us-east-1", "iam"));
    }

    // ── AC4: từ chối quyền ghi — công cụ không ký nổi một request làm thay đổi kho ───────────

    [Theory]
    [InlineData("PUT")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public void AC4_KhongKyBatKyYeuCauGhiNao_DuNguoiDungDanKhoaCoQuyenGhi(string phuongThuc)
    {
        var yeu = new YeuCauKy(
            phuongThuc,
            "/kho-bang-chung/trail/2026/08/15/000000000Z-b000001-abcdef12.jsonl",
            [],
            [new TieuDeYeuCau("Host", "s3.example.vn")],
            SigV4.MaBamThanRong,
            new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero),
            "us-east-1",
            "s3");

        var loi = Assert.Throws<NotSupportedException>(() => SigV4.Ky(yeu, KhoaBoVector));

        Assert.Contains("chỉ đọc", loi.Message, StringComparison.Ordinal);
    }

    // ── Hình dạng request thật công cụ phát ra ──────────────────────────────────────────────

    [Fact]
    public void AC1_DocKemKhoaChiDoc_RequestMangDuChuKyVaMaBamThanRong()
    {
        var kho = new ThongSoKho("https://s3.example.vn", "bang-chung", "hcm", "trail/");

        var yeu = YeuCauDocKho.LietKe(
            kho,
            new KhoaKho("AKIDEXAMPLE", "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY"),
            null,
            new DateTimeOffset(2026, 8, 15, 7, 30, 0, TimeSpan.Zero));

        Assert.Equal("GET", yeu.PhuongThuc);
        Assert.Equal("https://s3.example.vn/bang-chung?list-type=2&prefix=trail%2F", yeu.Url);
        Assert.Contains(yeu.TieuDe, t => t.Ten == "x-amz-date" && t.GiaTri == "20260815T073000Z");
        Assert.Contains(yeu.TieuDe, t => t.Ten == "x-amz-content-sha256" && t.GiaTri == SigV4.MaBamThanRong);
        Assert.Contains(yeu.TieuDe, t => t.Ten == "Authorization"
                                         && t.GiaTri.StartsWith("AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20260815/hcm/s3/aws4_request",
                                             StringComparison.Ordinal));
    }

    /// <summary>
    /// Sau lễ kho mở công khai: đọc ẩn danh thì <b>không</b> gắn tiêu đề riêng nào — request giản đơn
    /// không kích hoạt preflight, nên bớt đúng một chỗ CORS có thể chặn người dân giữa chừng.
    /// </summary>
    [Fact]
    public void AC1_DocAnDanh_KhongGanTieuDeRieng_VaKhongCoChuKy()
    {
        var kho = new ThongSoKho("https://s3.example.vn", "bang-chung", "hcm", "trail/");

        var yeu = YeuCauDocKho.Doc(kho, null, "trail/2026/08/15/070000000Z-b000001-abcdef12.jsonl",
            new DateTimeOffset(2026, 8, 15, 7, 30, 0, TimeSpan.Zero));

        Assert.Equal("GET", yeu.PhuongThuc);
        Assert.Equal(
            "https://s3.example.vn/bang-chung/trail/2026/08/15/070000000Z-b000001-abcdef12.jsonl",
            yeu.Url);
        Assert.Empty(yeu.TieuDe);
    }

    [Fact]
    public void AC1_LietKeTiepTrang_MangTheoDauTiepTucDaMaHoa()
    {
        var kho = new ThongSoKho("https://s3.example.vn", "bang-chung", "hcm", "trail/");

        var yeu = YeuCauDocKho.LietKe(kho, null, "1/dau tiep+tuc",
            new DateTimeOffset(2026, 8, 15, 7, 30, 0, TimeSpan.Zero));

        Assert.Equal(
            "https://s3.example.vn/bang-chung?continuation-token=1%2Fdau%20tiep%2Btuc&list-type=2&prefix=trail%2F",
            yeu.Url);
    }
}
