// MẪU: EDIT TEST — Mô tả cách mở form chỉnh sửa record đã có rồi thực thi test
// Pattern này khác Create: không điều hướng tới /create mà click link Edit của record đầu tiên trong danh sách.

using System;
using System.IO;
using NUnit.Framework;
using OfficeOpenXml;
using OpenQA.Selenium;

namespace TestProductGroup;

[NonParallelizable]
public class ProductGroupEditTest : BaseTest
{
    // --------------------------------------------------------
    // Helper: Điều hướng tới form Edit của record đầu tiên trong danh sách
    // Selector: link <a> có href chứa '/categories/' và '/edit'
    // --------------------------------------------------------
    private void OpenFirstEditForm()
    {
        OpenCategories();
        driver.FindElement(By.XPath("//a[contains(@href,'/categories/') and contains(@href,'/edit')]")).Click();
    }

    // --------------------------------------------------------
    // Thực thi từng test case Edit
    // Pattern gần giống Create, chỉ khác hàm mở form (OpenFirstEditForm thay vì OpenCreateForm)
    // --------------------------------------------------------
    private string ExecuteEditCase(string tcCode, string field, string rawData)
    {
        // 1. Mở form Edit của record đầu tiên
        OpenFirstEditForm();
        string testData = ResolveTestData(rawData);

        // 2. Xử lý TC Quay lại / Hủy bỏ (B-13)
        if (tcCode == "B-13")
        {
            driver.FindElement(By.XPath("//a[contains(@href,'/categories') and contains(.,'Quay lại')]")).Click();
            return driver.Url.Contains("/categories", StringComparison.OrdinalIgnoreCase)
                ? "Không lưu, quay về trang danh sách nhóm hàng hóa"
                : "Không quay về trang danh sách nhóm hàng hóa";
        }

        // 3. TC điền đầy đủ nhiều trường (multi-field)
        if (tcCode is "B-11" or "B-12" or "B-14" or "B-15" or "B-16")
        {
            var data = ParseMultiField(rawData);
            SetText("name", data.name);
            SetText("description", data.description);

            ClickSubmitButton();
            return GetFinalMessage(includeToast: true);
        }

        // 4. TC validate 1 trường: điền default hợp lệ trước, rồi ghi đè trường cần test
        SetText("name", "NhomEditHopLe");
        SetText("description", "Mo ta edit hop le");

        if (field.Contains("Tên nhóm", StringComparison.OrdinalIgnoreCase))
        {
            SetText("name", testData);
        }
        else if (field.Contains("Mô tả", StringComparison.OrdinalIgnoreCase))
        {
            SetText("description", testData);
        }

        ClickSubmitButton();
        return GetFinalMessage(includeToast: false);
    }

    // --------------------------------------------------------
    // Entry point: Vòng lặp đọc Excel và chạy test
    // fallbackIndex: 1 → sheet thứ 2 (index 1) = sheet Edit
    // --------------------------------------------------------
    [Test]
    public void TestCasesEdit()
    {
        using var package = OpenExcelForWrite();
        var sheet = RequireWorksheet(package, "Chỉnh sửa nhóm hàng hóa", fallbackIndex: 1);
        int row = FindFirstDataRow(sheet);

        Login();

        while (true)
        {
            string tcCode = sheet.Cells[row, 1].Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tcCode)) break;

            if (!IsValidTestCaseCode(tcCode)) { row++; continue; }

            string field = sheet.Cells[row, 2].Text ?? string.Empty;
            string rawValue = sheet.Cells[row, 5].Text ?? string.Empty;
            string expected = sheet.Cells[row, 6].Text ?? string.Empty;

            Console.WriteLine($"Running: {tcCode}");
            string actual = ExecuteEditCase(tcCode, field, rawValue);
            Console.WriteLine($"Actual  : {actual}");
            Console.WriteLine($"Expected: {expected}");

            SaveCaseResult(sheet, row, actual, expected);
            row++;
        }

        SavePackageWithRetry(package);
    }
}
