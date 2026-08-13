using System.Globalization;
using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 8: mốc neo của từng vòng có phải một block <b>có thật</b> trên chuỗi khối công khai, và
/// cam kết có được chốt <b>trước</b> khi block đó tồn tại hay không. Đây là lá chắn chống việc thử đi
/// thử lại hạt giống: mã băm của một block chưa được đào thì không ai đoán được, nên cam kết chốt
/// trước là bằng chứng ban tổ chức không chọn được kết quả vừa ý.
///
/// Hạng mục duy nhất cần dữ liệu ngoài — nhưng lõi vẫn thuần: block do vỏ UI đọc từ nguồn công khai
/// rồi đưa vào <see cref="VerificationInput.Blocks"/> dưới dạng dữ liệu. Chưa đọc được (chưa bấm tra
/// cứu, mạng hỏng, bị chặn) là KHÔNG KIỂM ĐƯỢC kèm link tra cứu thủ công — không bao giờ là ĐẠT.
/// </summary>
internal static class MocNeoCheck
{
    private const string TenChung = "Mốc neo chuỗi khối";

    private const string YNghia =
        "Vì lúc chốt cam kết chưa ai biết mã băm của một block chưa được đào, ban tổ chức không thể thử đi thử "
        + "lại hạt giống cho tới khi ra kết quả vừa ý.";

    public static IEnumerable<CheckResult> Run(VerificationInput input) =>
        NguonNgauNhienChung.TungVong(
            input,
            CheckIds.MocNeo,
            TenChung,
            "Báo cáo không công bố nguồn ngẫu nhiên vòng nào, nên không biết vòng nào neo vào block nào để đối "
            + "chiếu với chuỗi khối công khai.",
            (nguon, id, ten) => Kiem(input, nguon, id, ten));

    private static CheckResult Kiem(VerificationInput input, EntropySource nguon, string id, string ten)
    {
        var bc = input.Report;
        var title = $"Mốc neo chuỗi khối vòng {ten}";

        var soLieu = new List<CheckMetric>
        {
            new("Vòng", NguonNgauNhienChung.Co(nguon.Round)),
            new("Chuỗi khối neo báo cáo công bố", NguonNgauNhienChung.Co(nguon.AnchorChain)),
        };

        // Giá trị kỳ vọng của hạng mục này là mã băm khối neo báo cáo công bố: kể cả khi công cụ
        // chưa hỏi được nguồn công khai, người kiểm vẫn phải thấy con số phải đi so bằng mắt.
        CheckResult ChuaKiemDuoc(string vi, string? kyVong = null) =>
            new(id, title, CheckStatus.KhongKiemDuoc, vi, Expected: kyVong) { Metrics = soLieu };

        var chuoiKhoi = ChuoiKhoiNeo.Chuan(nguon.AnchorChain);

        if (chuoiKhoi is null)
            return ChuaKiemDuoc(string.IsNullOrWhiteSpace(nguon.AnchorChain)
                ? "Vòng này không nói neo vào chuỗi khối nào, nên không biết phải hỏi sổ cái công khai nào để "
                  + "biết block có thật hay không."
                : $"Vòng này khai neo vào chuỗi khối «{MoTaGiaTri.Gon(nguon.AnchorChain!.Trim())}» — công cụ chỉ "
                  + "biết đọc Ethereum và Bitcoin. Đoán bừa sổ cái rồi kết luận là điều nguy hiểm hơn nói chưa "
                  + "kiểm được.");

        var tenChuoi = ChuoiKhoiNeo.TenHienThi(chuoiKhoi);
        var daCamKet = MocNeoTraCuu.DoCaoDaCamKet(bc, chuoiKhoi);

        soLieu.Add(new CheckMetric(
            $"Độ cao block đích đã cam kết ({tenChuoi})",
            daCamKet?.ToString(CultureInfo.InvariantCulture) ?? NguonNgauNhienChung.KhongCo));
        soLieu.Add(new CheckMetric(
            "Độ cao khối neo vòng này công bố",
            nguon.BlockHeight?.ToString(CultureInfo.InvariantCulture) ?? NguonNgauNhienChung.KhongCo));

        if (daCamKet is null)
            return ChuaKiemDuoc(
                $"Báo cáo không công bố độ cao block đích đã cam kết cho {tenChuoi}, nên không biết phải đọc block "
                + "nào để đối chiếu. Thiếu chính con số được chốt trước thì lập luận \"cam kết có trước block\" "
                + "không có chỗ bám.");

        var link = ChuoiKhoiNeo.Link(chuoiKhoi, daCamKet.Value);

        soLieu.Add(new CheckMetric("Link tra cứu thủ công", link));

        if (nguon.BlockHeight is null)
            return ChuaKiemDuoc(
                "Vòng này chưa công bố độ cao khối neo (cổng chưa đóng), nên chưa đối chiếu được với độ cao đã "
                + "cam kết.");

        if (nguon.BlockHeight != daCamKet)
            return new CheckResult(
                id,
                title,
                CheckStatus.KhongDat,
                $"Vòng này lấy hạt giống từ block ở độ cao {nguon.BlockHeight.Value} trên {tenChuoi}, KHÁC độ cao "
                + $"{daCamKet.Value} đã được chốt trước và đóng dấu thời gian. Cam kết chốt một block rồi lại dùng "
                + "block khác thì lá chắn chống thử đi thử lại không còn: block được dùng có thể đã tồn tại từ "
                + "trước lúc chốt.",
                Expected: daCamKet.Value.ToString(CultureInfo.InvariantCulture),
                Actual: nguon.BlockHeight.Value.ToString(CultureInfo.InvariantCulture))
            {
                Metrics = soLieu,
            };

        var maBamCongBo = ChuanMaBam(nguon.BlockHash);

        if (maBamCongBo is null)
            return ChuaKiemDuoc(string.IsNullOrWhiteSpace(nguon.BlockHash)
                ? "Vòng này chưa công bố mã băm khối neo (cổng chưa đóng), nên chưa có gì để đối chiếu với block "
                  + "đọc từ nguồn công khai."
                : "Mã băm khối neo vòng này công bố không phải chuỗi mã băm hợp lệ, nên không đối chiếu được với "
                  + "block đọc từ nguồn công khai.");

        var quanSat = (input.Blocks ?? [])
            .FirstOrDefault(q => q is not null
                                 && string.Equals(ChuoiKhoiNeo.Chuan(q.ChuoiKhoi), chuoiKhoi, StringComparison.Ordinal)
                                 && q.DoCao == daCamKet.Value);

        if (quanSat is null)
            return ChuaKiemDuoc(
                $"Chưa đọc được block {daCamKet.Value} trên {tenChuoi} từ nguồn công khai, nên chưa biết mốc neo có "
                + $"phải block có thật hay không. Hãy tự đối chiếu bằng mắt tại {link} — mã băm ở đó phải trùng mã "
                + "băm khối neo báo cáo công bố.",
                maBamCongBo);

        if (quanSat.Nguon is not null)
            soLieu.Add(new CheckMetric("Nguồn công khai đã hỏi", quanSat.Nguon));

        if (quanSat.Loi is not null)
            return ChuaKiemDuoc(
                $"Hỏi nguồn công khai về block {daCamKet.Value} trên {tenChuoi} không được: {quanSat.Loi}. Đây là "
                + "hạng mục duy nhất cần dịch vụ ngoài, nên hỏng nguồn không phải bằng chứng gian lận, cũng không "
                + $"phải cớ để bỏ qua — hãy tự đối chiếu bằng mắt tại {link}.",
                maBamCongBo);

        var maBamDoc = ChuanMaBam(quanSat.MaBam);

        if (maBamDoc is null)
            return ChuaKiemDuoc(
                $"Nguồn công khai không trả về mã băm đọc được cho block {daCamKet.Value} trên {tenChuoi}, nên chưa "
                + $"đối chiếu được. Hãy tự đối chiếu bằng mắt tại {link}.",
                maBamCongBo);

        soLieu.Add(new CheckMetric("Mã băm block đọc từ nguồn công khai", maBamDoc));

        if (!string.Equals(maBamDoc, maBamCongBo, StringComparison.Ordinal))
            return new CheckResult(
                id,
                title,
                CheckStatus.KhongDat,
                $"Block {daCamKet.Value} trên {tenChuoi} có thật, nhưng mã băm của nó KHÁC mã băm mà báo cáo đang "
                + "công bố làm khối neo của vòng này: hạt giống không mọc ra từ block đã cam kết, mà từ một con số "
                + $"do bên khác đưa ra. Tự đối chiếu tại {link}.",
                Expected: maBamCongBo,
                Actual: maBamDoc)
            {
                Metrics = soLieu,
            };

        return SoThoiGian(bc, id, title, soLieu, quanSat, daCamKet.Value, tenChuoi, link, maBamDoc);
    }

    /// <summary>
    /// Mã băm khớp rồi mới tới câu quan trọng hơn: cam kết chốt trước hay sau khi block ra đời. So
    /// mốc do <b>bên thứ ba</b> cấp là chính; không có dấu nào thì chỉ còn con số ban tổ chức tự khai
    /// — đủ để bắt lỗi ngược chiều, không đủ để tuyên bố lá chắn còn nguyên.
    /// </summary>
    private static CheckResult SoThoiGian(
        TransparencyReport bc,
        string id,
        string title,
        List<CheckMetric> soLieu,
        QuanSatKhoi quanSat,
        long doCao,
        string tenChuoi,
        string link,
        string maBamKhop)
    {
        var daoLuc = quanSat.ThoiDiemDao;

        if (daoLuc is not null)
            soLieu.Add(new CheckMetric(
                "Thời điểm block được đào (theo nguồn công khai)", DauThoiGianChung.HienThi(daoLuc.Value)));

        var dauDocLap = DauThoiGianChung.MocCamKet(DauThoiGianChung.TatCa(bc))
            .Select(t => DauThoiGianChung.DocMoc(t.GenTime))
            .Where(m => m is not null)
            .Select(m => m!.Value)
            .ToList();

        // Chốt lại (refreeze) là thao tác hợp lệ: lần chốt SAU CÙNG mới là lần ràng buộc dữ liệu
        // đang công bố, nên nó là mốc phải đem so — lấy lần chốt đầu là tự nới cho ban tổ chức.
        var chotLuc = dauDocLap.Count > 0 ? dauDocLap.Max() : DauThoiGianChung.DocMoc(bc.AnchorCommitment?.AnchorFrozenAt);
        var tuKhai = dauDocLap.Count == 0;

        if (chotLuc is not null)
            soLieu.Add(new CheckMetric(
                tuKhai
                    ? "Thời điểm chốt cam kết (ban tổ chức tự khai)"
                    : "Thời điểm chốt cam kết (dấu thời gian bên thứ ba)",
                DauThoiGianChung.HienThi(chotLuc.Value)));

        // Mã băm đã khớp: giữ nguyên cặp giá trị đó cho chế độ chuyên sâu, kẻo phần kiểm được của
        // hạng mục biến mất chỉ vì phần so thời gian còn dở.
        CheckResult ChuaKiemDuoc(string vi) =>
            new(id, title, CheckStatus.KhongKiemDuoc, vi, Expected: maBamKhop, Actual: maBamKhop) { Metrics = soLieu };

        var khopMaBam =
            $"Mã băm block {doCao} trên {tenChuoi} đọc từ nguồn công khai trùng khít mã băm khối neo báo cáo đang "
            + "công bố, nên mốc neo đúng là một block có thật. ";

        if (daoLuc is null)
            return ChuaKiemDuoc(
                khopMaBam
                + "Nhưng nguồn công khai không cho biết block được đào lúc nào, nên chưa kiểm được cam kết có "
                + $"trước khi block ra đời hay không — phần lá chắn quan trọng nhất còn bỏ ngỏ. Tự đối chiếu giờ "
                + $"đào tại {link}.");

        if (chotLuc is null)
            return ChuaKiemDuoc(
                khopMaBam
                + "Nhưng báo cáo không công bố mốc thời gian nào đọc được cho lần chốt cam kết (không dấu thời "
                + "gian, không thời điểm chốt), nên không có gì để so với lúc block được đào.");

        if (chotLuc.Value >= daoLuc.Value)
            return new CheckResult(
                id,
                title,
                CheckStatus.KhongDat,
                khopMaBam
                + $"Nhưng block đã được đào lúc {DauThoiGianChung.HienThi(daoLuc.Value)}, TRƯỚC (hoặc đúng lúc) "
                + $"cam kết được chốt lúc {DauThoiGianChung.HienThi(chotLuc.Value)}"
                + (tuKhai ? " theo chính con số ban tổ chức tự khai" : " theo dấu thời gian của bên thứ ba")
                + ": lúc chốt thì mã băm dùng làm hạt giống đã tồn tại và ai cũng xem được, nên lá chắn chống thử "
                + "đi thử lại hạt giống không còn hiệu lực với vòng này. Lưu ý giờ ghi trong block là giờ do người "
                + "đào khai (Bitcoin cho phép lệch tới hai tiếng), nên nếu hai mốc chỉ cách nhau vài phút thì hãy "
                + "đối chiếu thêm với biên bản buổi lễ trước khi kết luận.",
                Expected: DauThoiGianChung.HienThi(chotLuc.Value),
                Actual: DauThoiGianChung.HienThi(daoLuc.Value))
            {
                Metrics = soLieu,
            };

        var khoangCach = MoTaKhoang(daoLuc.Value - chotLuc.Value);

        if (tuKhai)
            return ChuaKiemDuoc(
                khopMaBam
                + $"Thời điểm chốt cam kết ({DauThoiGianChung.HienThi(chotLuc.Value)}) sớm hơn lúc block được đào "
                + $"({DauThoiGianChung.HienThi(daoLuc.Value)}) {khoangCach}, nhưng đó là con số ban tổ chức tự "
                + "khai chứ không có dấu thời gian của bên thứ ba đóng lên — bên bị kiểm tự khai giờ của mình thì "
                + "chưa chứng minh được thứ tự trước sau.");

        return new CheckResult(
            id,
            title,
            CheckStatus.Dat,
            khopMaBam
            + $"Cam kết đã được bên thứ ba đóng dấu lúc {DauThoiGianChung.HienThi(chotLuc.Value)}, còn block chỉ "
            + $"được đào lúc {DauThoiGianChung.HienThi(daoLuc.Value)} — sau đó {khoangCach}. " + YNghia,
            Expected: DauThoiGianChung.HienThi(chotLuc.Value),
            Actual: DauThoiGianChung.HienThi(daoLuc.Value))
        {
            Metrics = soLieu,
        };
    }

    /// <summary>Mã băm block: nguồn Ethereum trả kèm tiền tố <c>0x</c>, báo cáo thì không — cắt cho về một dạng.</summary>
    private static string? ChuanMaBam(string? maBam)
    {
        var chuan = Hex.ChuanHoa(maBam);
        if (chuan is null) return null;

        if (chuan.StartsWith("0x", StringComparison.Ordinal)) chuan = chuan[2..];

        return Hex.Doc(chuan) is null ? null : chuan;
    }

    private static string MoTaKhoang(TimeSpan khoang) =>
        khoang.TotalMinutes < 1
            ? $"{khoang.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} giây"
            : khoang.TotalHours < 1
                ? $"{khoang.TotalMinutes.ToString("0", CultureInfo.InvariantCulture)} phút"
                : $"{khoang.TotalHours.ToString("0.#", CultureInfo.InvariantCulture)} giờ";
}
