using System;
using System.Linq;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace TestProductGroup;

/// <summary>
/// Lớp tiện ích xử lý dữ liệu Excel: đọc, parse và ghi kết quả PASS/FAIL.
/// BaseTest chỉ là wrapper gọi tới các hàm static này — agent không cần viết lại.
/// </summary>
public static class ExcelHelper
{
    /// <summary>
    /// Chuẩn hóa dữ liệu thô từ cell Excel.
    /// Quy tắc: dấu "-" → rỗng; chuỗi trong "..." → lấy nội dung bên trong.
    /// </summary>
    public static string ResolveTestData(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        string trimmed = raw.Trim();
        if (trimmed == "-") return string.Empty;
        return ExtractValue(trimmed);
    }

    /// <summary>
    /// Parse một ô Excel chứa nhiều trường phân tách bằng \n.
    /// Ví dụ cell: "Tên nhóm hàng hóa: ABC\nMô tả: XYZ"
    /// → (name: "ABC", description: "XYZ")
    /// </summary>
    public static (string name, string description) ParseMultiField(string raw)
    {
        string name = string.Empty, description = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return (name, description);

        var lines = raw.Replace("\r", string.Empty).Split('\n');
        foreach (var line in lines.Select(x => x.Trim()))
        {
            if (line.StartsWith("Tên nhóm hàng hóa", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Tên nhóm",          StringComparison.OrdinalIgnoreCase))
                name = ExtractValue(line);
            else if (line.StartsWith("Mô tả", StringComparison.OrdinalIgnoreCase))
                description = ExtractValue(line);
        }
        return (name, description);
    }

    /// <summary>
    /// Ghi kết quả kiểm thử vào cột 7 (Actual), cột 8 (PASS/FAIL) của sheet.
    /// Cell PASS tô xanh, FAIL tô đỏ.
    /// </summary>
    public static void SaveCaseResult(ExcelWorksheet sheet, int row, string actual, string expected)
    {
        sheet.Cells[row, 7].Value = actual;
        bool isPass = string.Equals(actual?.Trim(), expected?.Trim(), StringComparison.OrdinalIgnoreCase);
        var statusCell = sheet.Cells[row, 8];
        statusCell.Value = isPass ? "PASS" : "FAIL";
        ApplyStatusStyle(statusCell, isPass);
    }

    public static void ApplyStatusStyle(ExcelRange cell, bool isPass)
    {
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
        cell.Style.Fill.BackgroundColor.SetColor(isPass ? Color.LightGreen : Color.Red);
        cell.Style.Font.Color.SetColor(isPass ? Color.Black : Color.White);
        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        cell.Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
        cell.Style.Font.Bold = true;
    }

    // -------------------------------------------------------
    // INTERNAL — Trích xuất giá trị từ cú pháp Excel
    // -------------------------------------------------------
    private static string ExtractValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        string input = raw.Trim();
        if (input == "\"\"") return string.Empty;

        // VD: ""value""  (Excel double-quote style)
        var excelQuoted = Regex.Match(input, "\"\"(.*?)\"\"", RegexOptions.Singleline);
        if (excelQuoted.Success) return excelQuoted.Groups[1].Value;

        // VD: "value"
        var quoted = Regex.Match(input, "\"([^\"]*)\"", RegexOptions.Singleline);
        if (quoted.Success) return quoted.Groups[1].Value;

        // VD: Tên nhóm: value
        int colon = input.IndexOf(':');
        if (colon >= 0 && colon < input.Length - 1)
            return input[(colon + 1)..].Trim().Trim('"');

        return input.Replace("\"\"", "\"");
    }
}
