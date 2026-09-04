namespace Noxh.XacMinh.Core.Verification;

/// <summary>
/// Ba trạng thái, không phải hai. Thiếu dữ liệu mà báo ĐẠT là nói dối, nên
/// <see cref="KhongKiemDuoc"/> là một kết luận đầy đủ chứ không phải một dạng bỏ qua.
/// </summary>
public enum CheckStatus
{
    Dat,
    KhongDat,
    KhongKiemDuoc,
}

public static class CheckStatusText
{
    public static string Nhan(this CheckStatus status) => status switch
    {
        CheckStatus.Dat => "ĐẠT",
        CheckStatus.KhongDat => "KHÔNG ĐẠT",
        _ => "CHƯA ĐỦ DỮ LIỆU",
    };
}
