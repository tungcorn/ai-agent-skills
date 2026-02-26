using System;
using System.IO;
using NUnit.Framework;
using OfficeOpenXml;
using OpenQA.Selenium;

namespace TestProductGroup;

[NonParallelizable]
public class ProductGroupCreateTest : BaseTest
{
    private string ExecuteCreateCase(string tcCode, string field, string rawData)
    {
        // 1. Điều hướng tới Form Thêm Mới
        OpenCreateForm();
        string testData = ResolveTestData(rawData);

        // 2. Xử lý logic Hủy bỏ / Quay lại (TC-13)
        if (tcCode == "A-13")
        {
            driver.FindElement(By.XPath("//a[contains(@href,'/categories') and contains(.,'Quay lại')]")).Click();
            return driver.Url.Contains("/categories", StringComparison.OrdinalIgnoreCase)
                ? "Không lưu, quay về trang danh sách nhóm hàng hóa"
                : "Không quay về trang danh sách nhóm hàng hóa";
        }

        // 3. Xử lý điền Data chuẩn (Dùng chung cho nhiều trường)
        if (tcCode is "A-11" or "A-12" or "A-14" or "A-15" or "A-16")
        {
            var data = ParseMultiField(rawData); // Phân rã dữ liệu có dấu xuống dòng \n
            SetText("name", data.name);
            SetText("description", data.description);

            ClickSubmitButton();
            return GetFinalMessage(includeToast: true);
        }

        // 4. Fill Data ngầm định cho Test Validate 1 trường duy nhất
        SetText("name", "NhomTestHopLe");
        SetText("description", "Mo ta hop le");

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

    [Test]
    public void TestCasesCreate()
    {
        using var package = new ExcelPackage(new FileInfo(ExcelPath));
        var sheet = RequireWorksheet(package, "Thêm nhóm hàng hóa", fallbackIndex: 0);
        int row = FindFirstDataRow(sheet);

        Login();

        while (true)
        {
            string tcCode = sheet.Cells[row, 1].Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tcCode))
            {
                break;
            }

            if (!IsValidTestCaseCode(tcCode))
            {
                row++;
                continue;
            }

            string field = sheet.Cells[row, 2].Text ?? string.Empty;
            string rawValue = sheet.Cells[row, 5].Text ?? string.Empty;
            string expected = sheet.Cells[row, 6].Text ?? string.Empty;

            Console.WriteLine($"Running: {tcCode}");
            string actual = ExecuteCreateCase(tcCode, field, rawValue);
            Console.WriteLine($"Actual  : {actual}");
            Console.WriteLine($"Expected: {expected}");

            // 5. Ghi kết quả vào Excel (Pass nếu actual giống y hệt expected)
            SaveCaseResult(sheet, row, actual, expected);
            row++;
        }

        SavePackageWithRetry(package);
    }
}
