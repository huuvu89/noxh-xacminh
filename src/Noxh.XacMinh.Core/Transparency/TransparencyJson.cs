using System.Text.Json;

namespace Noxh.XacMinh.Core.Transparency;

/// <summary>
/// Nạp JSON minh bạch thành model. Không ném ngoại lệ ra ngoài: JSON hỏng là chuyện thường ngày
/// (tải nhầm trang lỗi, copy thiếu đuôi), và một ngoại lệ lọt lên Blazor là một trang trắng.
/// </summary>
public static class TransparencyJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ParseResult.Failed(
                "Chưa có nội dung để kiểm. Hãy thả file JSON minh bạch vào trang, hoặc dán nội dung vào ô văn bản.");

        // BOM lọt vào khi người dùng dán từ file do Notepad/Excel ghi ra.
        var text = json.Trim().TrimStart('﻿');

        TransparencyReport? report;
        try
        {
            report = JsonSerializer.Deserialize<TransparencyReport>(text, Options);
        }
        catch (JsonException ex)
        {
            return ParseResult.Failed(MoTaLoiCuPhap(ex));
        }
        catch (NotSupportedException)
        {
            return ParseResult.Failed(
                "Nội dung không phải báo cáo minh bạch: có trường sai kiểu dữ liệu so với bản công bố.");
        }

        if (report is null)
            return ParseResult.Failed("Nội dung rỗng — không có gì để kiểm.");

        if (report.Decks is null)
            return ParseResult.Failed(
                "Đây là JSON hợp lệ nhưng không phải báo cáo minh bạch: không thấy khối chồng phiếu ('decks'). "
                + "Hãy tải lại file từ trang công bố kết quả.");

        return ParseResult.Ok(report);
    }

    private static string MoTaLoiCuPhap(JsonException ex)
    {
        var viTri = ex.LineNumber is { } dong
            ? $" (dòng {dong + 1}, ký tự {(ex.BytePositionInLine ?? 0) + 1})"
            : string.Empty;

        return $"Nội dung không phải JSON hợp lệ{viTri}. "
               + "Hãy kiểm tra lại file tải từ trang công bố — thường là do copy thiếu hoặc lưu nhầm trang lỗi.";
    }
}

/// <summary>Kết quả nạp: hoặc có báo cáo, hoặc có một câu giải thích cho người dùng.</summary>
public sealed class ParseResult
{
    private ParseResult(TransparencyReport? report, string? errorMessage)
    {
        Report = report;
        ErrorMessage = errorMessage;
    }

    public TransparencyReport? Report { get; }

    public string? ErrorMessage { get; }

    public bool Success => Report is not null;

    public static ParseResult Ok(TransparencyReport report) => new(report, null);

    public static ParseResult Failed(string message) => new(null, message);
}
