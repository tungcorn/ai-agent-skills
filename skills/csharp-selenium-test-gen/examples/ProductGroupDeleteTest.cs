// MẪU: DELETE TEST — Mô tả cách xử lý Confirmation Modal
// Pattern này khác Create/Edit: không fill form mà click nút Delete → xác nhận hoặc hủy trong modal.

using System;
using System.IO;
using NUnit.Framework;
using OfficeOpenXml;
using OpenQA.Selenium;

namespace TestProductGroup;

[NonParallelizable]
public class ProductGroupDeleteTest : BaseTest
{
    // --------------------------------------------------------
    // Helper: Tìm nút Delete đầu tiên trong danh sách
    // Selector: button có class nguy hiểm + data-bs-target modal
    // --------------------------------------------------------
    private IWebElement GetFirstDeleteButton()
    {
        OpenCategories();
        return wait.Until(d =>
        {
            var buttons = d.FindElements(By.CssSelector("button.btn-outline-danger[data-bs-target='#deleteModal']"));
            foreach (var button in buttons)
            {
                if (button.Displayed && button.Enabled)
                    return button;
            }
            return null;
        });
    }

    // --------------------------------------------------------
    // Thực thi từng test case Delete
    // C-01: Kiểm tra modal có hiện ra không
    // C-02: Xác nhận xóa → kiểm tra toast thành công
    // C-03: Hủy xóa → kiểm tra dữ liệu vẫn còn
    // --------------------------------------------------------
    private string ExecuteDeleteCase(string tcCode)
    {
        if (tcCode == "C-01")
        {
            // Click nút Delete → kiểm tra modal có class "show"
            GetFirstDeleteButton().Click();
            bool modalVisible = wait.Until(d =>
            {
                var modal = d.FindElement(By.Id("deleteModal"));
                return modal.Displayed && (modal.GetAttribute("class") ?? string.Empty).Contains("show");
            });
            return modalVisible
                ? "Hiển thị thông báo xác nhận xóa"
                : "Không hiển thị thông báo xác nhận xóa";
        }

        if (tcCode == "C-02")
        {
            // Click nút Delete → Click "Xác nhận" trong modal
            GetFirstDeleteButton().Click();
            var confirmButton = wait.Until(d =>
            {
                var btn = d.FindElement(By.CssSelector("#deleteModal button.btn-danger[type='submit']"));
                return btn.Displayed && btn.Enabled ? btn : null;
            });
            confirmButton.Click();

            // Lấy toast message sau khi xóa
            string message = GetFinalMessage(includeToast: true);
            if (message.Contains("thành công", StringComparison.OrdinalIgnoreCase))
                return "Xóa tham số thành công. Hiển thị lại danh sách nhóm hàng hóa.";
            return message;
        }

        if (tcCode == "C-03")
        {
            // Click nút Delete → Click "Hủy" trong modal → kiểm tra dữ liệu vẫn còn
            var firstDelete = GetFirstDeleteButton();
            string targetName = firstDelete.GetAttribute("data-name") ?? string.Empty;
            firstDelete.Click();
            driver.FindElement(By.CssSelector("#deleteModal button.btn-secondary")).Click();

            bool stillExists = driver.PageSource.Contains(targetName, StringComparison.OrdinalIgnoreCase);
            return stillExists
                ? "Không xóa dữ liệu, quay lại trang hiển thị danh sách nhóm hàng hóa."
                : "Đối tượng không còn trong danh sách sau khi hủy bỏ";
        }

        return "Không hỗ trợ mã kiểm thử";
    }

    // --------------------------------------------------------
    // Entry point: Vòng lặp đọc Excel và chạy test
    // Lưu ý: Delete Test không đọc Cột 5 (Input) vì không fill form
    // --------------------------------------------------------
    [Test]
    public void TestCasesDelete()
    {
        using var package = new ExcelPackage(new FileInfo(ExcelPath));
        var sheet = RequireWorksheet(package, "Xóa nhóm hàng hóa", fallbackIndex: 2);
        int row = FindFirstDataRow(sheet);

        Login();

        while (true)
        {
            string tcCode = sheet.Cells[row, 1].Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tcCode)) break;

            if (!IsValidTestCaseCode(tcCode)) { row++; continue; }

            // Delete thường không có field (col 2) và input (col 5) — chỉ cần Expected (col 6)
            string expected = sheet.Cells[row, 6].Text ?? string.Empty;
            Console.WriteLine($"Running: {tcCode}");

            string actual = ExecuteDeleteCase(tcCode);
            Console.WriteLine($"Actual  : {actual}");
            Console.WriteLine($"Expected: {expected}");

            SaveCaseResult(sheet, row, actual, expected);
            row++;
        }

        SavePackageWithRetry(package);
    }
}
