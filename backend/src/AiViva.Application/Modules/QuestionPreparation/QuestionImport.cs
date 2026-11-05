using System.Globalization;
using System.Text;

namespace AiViva.Application.Questions;

public sealed record QuestionImportRow(
    int RowNumber, string Content, string? BloomLevel, int? DifficultyLevel,
    IReadOnlyList<string> ExpectedKeyPoints);

public sealed record QuestionImportError(int RowNumber, string Message);

public sealed record QuestionImportResult(
    int ImportedCount, IReadOnlyList<QuestionImportError> Errors, IReadOnlyList<QuestionDto> Questions);

public interface IQuestionImportService
{
    /// <summary>Imports every row or none of them. Returns null when the bank does not exist.</summary>
    Task<QuestionImportResult?> ImportAsync(Guid questionBankId, string csv, CancellationToken ct = default);
}

/// <summary>
/// Parses lecturer-authored CSV (Excel "CSV UTF-8" or Google Sheets export).
/// Columns: content (required), bloomLevel, difficultyLevel, expectedKeyPoints ("|" separated).
/// Vietnamese headers and Bloom labels are accepted so the template can be filled in either language.
/// </summary>
public static class QuestionCsvParser
{
    public const int MaxRows = 500;
    public const int MaxKeyPoints = 10;

    private static readonly Dictionary<string, string> Columns = new()
    {
        ["content"] = "content",
        ["noidung"] = "content",
        ["cauhoi"] = "content",
        ["question"] = "content",
        ["bloom"] = "bloom",
        ["bloomlevel"] = "bloom",
        ["mucbloom"] = "bloom",
        ["mucdonhanthuc"] = "bloom",
        ["difficulty"] = "difficulty",
        ["difficultylevel"] = "difficulty",
        ["dokho"] = "difficulty",
        ["expectedkeypoints"] = "points",
        ["keypoints"] = "points",
        ["ychinh"] = "points",
        ["ychinhkyvong"] = "points",
        ["dapan"] = "points"
    };

    private static readonly Dictionary<string, string> BloomLevels = new()
    {
        ["remember"] = "Remember",
        ["nho"] = "Remember",
        ["understand"] = "Understand",
        ["hieu"] = "Understand",
        ["apply"] = "Apply",
        ["vandung"] = "Apply",
        ["analyze"] = "Analyze",
        ["analyse"] = "Analyze",
        ["phantich"] = "Analyze"
    };

    public static (IReadOnlyList<QuestionImportRow> Rows, IReadOnlyList<QuestionImportError> Errors) Parse(string text)
    {
        var errors = new List<QuestionImportError>();
        var rows = new List<QuestionImportRow>();
        var records = ReadRecords(text.TrimStart('﻿'));
        if (records.Count == 0)
            return (rows, [new QuestionImportError(1, "Tệp CSV trống.")]);

        var header = records[0].Select(Normalize).ToArray();
        var index = new Dictionary<string, int>();
        for (var i = 0; i < header.Length; i++)
            if (Columns.TryGetValue(header[i], out var column)) index.TryAdd(column, i);
        if (!index.ContainsKey("content"))
            return (rows, [new QuestionImportError(1, "Thiếu cột \"content\" (nội dung câu hỏi).")]);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var r = 1; r < records.Count; r++)
        {
            var record = records[r];
            var rowNumber = r + 1;
            if (record.All(string.IsNullOrWhiteSpace)) continue;
            if (rows.Count + errors.Count >= MaxRows)
            {
                errors.Add(new(rowNumber, $"Mỗi lần nhập tối đa {MaxRows} câu hỏi."));
                break;
            }

            string Cell(string name) => index.TryGetValue(name, out var i) && i < record.Count ? record[i].Trim() : "";
            var rowErrors = new List<string>();
            var content = Cell("content");
            if (content.Length is 0 or > 2000) rowErrors.Add("nội dung cần từ 1 đến 2000 ký tự");
            else if (!seen.Add(content)) rowErrors.Add("câu hỏi bị trùng trong tệp");

            string? bloom = null;
            var bloomText = Cell("bloom");
            if (bloomText.Length > 0 && !BloomLevels.TryGetValue(Normalize(bloomText), out bloom))
                rowErrors.Add($"mức Bloom \"{bloomText}\" không hợp lệ (Nhớ/Hiểu/Vận dụng/Phân tích)");

            int? difficulty = null;
            var difficultyText = Cell("difficulty");
            if (difficultyText.Length > 0)
            {
                if (int.TryParse(difficultyText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
                    value is >= 1 and <= 5) difficulty = value;
                else rowErrors.Add("độ khó phải là số nguyên từ 1 đến 5");
            }

            var points = Cell("points").Split(['|', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (points.Length > MaxKeyPoints) rowErrors.Add($"tối đa {MaxKeyPoints} ý chính");
            if (points.Any(point => point.Length > 2000)) rowErrors.Add("mỗi ý chính tối đa 2000 ký tự");

            if (rowErrors.Count > 0) errors.Add(new(rowNumber, string.Join("; ", rowErrors)));
            else rows.Add(new(rowNumber, content, bloom, difficulty, points));
        }

        if (rows.Count == 0 && errors.Count == 0) errors.Add(new(2, "Tệp CSV không có câu hỏi nào."));
        return (rows, errors);
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
                builder.Append(c);
        return builder.ToString();
    }

    private static List<List<string>> ReadRecords(string text)
    {
        var delimiter = DetectDelimiter(text);
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"' && field.Length == 0) quoted = true;
            else if (c == delimiter) { record.Add(field.ToString()); field.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                record.Add(field.ToString());
                field.Clear();
                records.Add(record);
                record = [];
            }
            else field.Append(c);
        }
        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
        return records;
    }

    // Excel uses the regional list separator, so Vietnamese locales often save ";" instead of ",".
    private static char DetectDelimiter(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        var header = end < 0 ? text : text[..end];
        return new[] { ',', ';', '\t' }.OrderByDescending(d => header.Count(c => c == d)).First();
    }
}
