using Noxh.XacMinh.Core.Kho;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #18 — AC1: đọc danh sách lô trên kho. Kho trả về XML kiểu S3; bóc XML là hàm thuần nên kiểm
/// được bằng đúng những câu trả lời khó chịu mà kho thật hay trả: có phân trang, và từ chối truy cập.
/// </summary>
public class LietKeKhoTests
{
    private const string Ns = "http://s3.amazonaws.com/doc/2006-03-01/";

    [Fact]
    public void AC1_LietKeMotTrang_LayDuKeyTheoDungThuTuKho()
    {
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ListBucketResult xmlns="{Ns}">
              <Name>bang-chung</Name>
              <IsTruncated>false</IsTruncated>
              <Contents><Key>trail/2026/08/15/073000000Z-b000001-a1b2c3d4.jsonl</Key><Size>512</Size></Contents>
              <Contents><Key>trail/2026/08/15/073002000Z-b000002-a1b2c3d4.jsonl</Key><Size>512</Size></Contents>
            </ListBucketResult>
            """;

        var ketQua = LietKeKho.Doc(xml);

        Assert.Null(ketQua.Loi);
        Assert.Equal(
            ["trail/2026/08/15/073000000Z-b000001-a1b2c3d4.jsonl",
             "trail/2026/08/15/073002000Z-b000002-a1b2c3d4.jsonl"],
            ketQua.Key);
        Assert.Null(ketQua.DauTiepTuc);
    }

    [Fact]
    public void AC1_LietKeConTrangSau_TraVeDauTiepTucDeDocNot()
    {
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ListBucketResult xmlns="{Ns}">
              <IsTruncated>true</IsTruncated>
              <NextContinuationToken>1/abc+def</NextContinuationToken>
              <Contents><Key>trail/2026/08/15/073000000Z-b000001-a1b2c3d4.jsonl</Key></Contents>
            </ListBucketResult>
            """;

        var ketQua = LietKeKho.Doc(xml);

        Assert.Equal("1/abc+def", ketQua.DauTiepTuc);
    }

    [Fact]
    public void AC4_KhoTuChoi_BocDungMaLoiCuaKho_ChuKhongNuotDi()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Error><Code>AccessDenied</Code><Message>Access Denied</Message></Error>
            """;

        var ketQua = LietKeKho.Doc(xml);

        Assert.Empty(ketQua.Key);
        Assert.Contains("AccessDenied", ketQua.Loi ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void AC1_KhoTraRac_BaoLoiChuKhongNemNgoaiLe()
    {
        var ketQua = LietKeKho.Doc("<không phải xml");

        Assert.Empty(ketQua.Key);
        Assert.NotNull(ketQua.Loi);
    }
}
