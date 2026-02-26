using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using OfficeOpenXml;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace TestProductGroup;

public class BaseTest
{
    protected IWebDriver driver = null!;
    protected WebDriverWait wait = null!;
    private bool driverInitialized;
    private bool runLockHeld;
    private static readonly object TestRunLock = new();

    // ====================================================
    // CẤU HÌNH CHUNG — SỬA CÁC HẰNG VÀ URL CHO TỪNG DỰ ÁN
    // ====================================================
    protected const string ExcelPath =
        @"<ABSOLUTE_PATH_TO_EXCEL_FILE>";   // VD: @"D:\project\test-data.xlsx"

    // Đặt false trong class con nếu muốn ghi đè file gốc thay vì tạo file kết quả mới
    protected virtual bool SaveToNewFile => true;

    /// <summary>
    /// Mở file Excel để ghi kết quả test.
    /// Mặc định (SaveToNewFile = true): sao chép file gốc sang file mới có timestamp (yyyy-MM-dd_HHmmss).
    /// Đặt SaveToNewFile = false trong class con để ghi đè file gốc.
    /// </summary>
    protected ExcelPackage OpenExcelForWrite()
    {
        if (!SaveToNewFile)
            return new ExcelPackage(new FileInfo(ExcelPath));

        string dir  = Path.GetDirectoryName(ExcelPath)!;
        string name = Path.GetFileNameWithoutExtension(ExcelPath);
        string outputPath = Path.Combine(dir, $"{name}_result_{DateTime.Now:yyyy-MM-dd_HHmmss}.xlsx");

        File.Copy(ExcelPath, outputPath, overwrite: true);
        Console.WriteLine($"Sao chép file gốc sang: {outputPath}");
        return new ExcelPackage(new FileInfo(outputPath));
    }

    protected const string UrlLogin      = "http://<SERVER_IP>/login";
    protected const string UrlCategories = "http://<SERVER_IP>/categories";

    protected static readonly string Username =
        Environment.GetEnvironmentVariable("ROBOT_USERNAME") ?? "your_username";

    protected static readonly string Password =
        Environment.GetEnvironmentVariable("ROBOT_PASSWORD") ?? "your_password";

    // ====================================================
    // SETUP / TEARDOWN — KHÔNG OVERRIDE TRONG CLASS CON
    // ====================================================
    [SetUp]
    public void Setup()
    {
        Monitor.Enter(TestRunLock, ref runLockHeld);
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        driver = new ChromeDriver();
        driverInitialized = true;
        driver.Manage().Window.Maximize();
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
        wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
    }

    [TearDown]
    public void TearDown()
    {
        if (driverInitialized)
        {
            try { driver.Quit(); } catch { }
            driver.Dispose();
        }

        if (runLockHeld)
        {
            Monitor.Exit(TestRunLock);
            runLockHeld = false;
        }
    }

    // ====================================================
    // ĐIỀU HƯỚNG BROWSER
    // ====================================================
    protected void Login()
    {
        if (driver == null || wait == null)
            throw new AssertionException("WebDriver chưa được khởi tạo (Setup failed).");

        driver.Navigate().GoToUrl(UrlLogin);
        driver.FindElement(By.Name("username")).Clear();
        driver.FindElement(By.Name("username")).SendKeys(Username);
        driver.FindElement(By.Name("password")).Clear();
        driver.FindElement(By.Name("password")).SendKeys(Password);
        driver.FindElement(By.CssSelector("button[type='submit']")).Click();
        wait.Until(d => d.Url.Contains("/categories") || !d.Url.Contains("/login"));
    }

    protected void OpenCreateForm()
    {
        if (driver == null || wait == null)
            throw new AssertionException("WebDriver chưa được khởi tạo (Setup failed).");

        driver.Navigate().GoToUrl($"{UrlCategories}/create");
        wait.Until(d => d.Url.Contains("/categories/create"));
    }

    protected void OpenCategories()
    {
        if (driver == null || wait == null)
            throw new AssertionException("WebDriver chưa được khởi tạo (Setup failed).");

        driver.Navigate().GoToUrl(UrlCategories);
        wait.Until(d => d.Url.Contains("/categories"));
    }

    // ====================================================
    // TƯƠNG TÁC FORM CƠ BẢN
    // ====================================================
    protected void SetText(string elementId, string value)
    {
        if (driver == null)
            throw new AssertionException("WebDriver chưa được khởi tạo (Setup failed).");

        var element = driver.FindElement(By.Id(elementId));
        element.Clear();
        element.SendKeys(value ?? string.Empty);
    }

    protected void ClickSubmitButton()
    {
        if (driver == null || wait == null)
            throw new AssertionException("WebDriver chưa được khởi tạo (Setup failed).");

        var submit = wait.Until(d =>
        {
            var candidates = d.FindElements(By.CssSelector("button#btnSubmit, button[type='submit']"));
            foreach (var element in candidates)
            {
                if (!element.Displayed || !element.Enabled) continue;
                string text = (element.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(text) || text.Contains("Lưu", StringComparison.OrdinalIgnoreCase))
                    return element;
            }
            return null;
        });

        try { submit.Click(); }
        catch (ElementClickInterceptedException)  { ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", submit); }
        catch (ElementNotInteractableException)   { ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", submit); }
    }

    // ====================================================
    // ĐỌC KẾT QUẢ / THÔNG BÁO
    // ====================================================
    protected string GetNameError()
    {
        if (driver == null) return string.Empty;
        try
        {
            var error = driver.FindElement(
                By.XPath("//input[@id='name']/following-sibling::div[contains(@class,'invalid-feedback')]"));
            return string.IsNullOrWhiteSpace(error.Text) ? string.Empty : error.Text.Trim();
        }
        catch { return string.Empty; }
    }

    protected string GetToastMessage()
    {
        if (driver == null) return string.Empty;
        try
        {
            var shortWait = new WebDriverWait(driver, TimeSpan.FromSeconds(2));
            var title = shortWait.Until(d =>
            {
                var elements = d.FindElements(By.CssSelector(".swal2-popup.swal2-toast .swal2-title"));
                return elements.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Text));
            });
            return title?.Text?.Trim() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    protected string GetFinalMessage(bool includeToast)
    {
        string nameError = GetNameError();
        if (!string.IsNullOrWhiteSpace(nameError)) return nameError;
        if (includeToast)
        {
            string toast = GetToastMessage();
            if (!string.IsNullOrWhiteSpace(toast)) return toast;
        }
        return "Hợp lệ";
    }

    // ====================================================
    // TIỆN ÍCH EXCEL (WRAPPER GỌI TỚI ExcelHelper)
    // ====================================================

    /// <summary>
    /// ⚠️ CẢNH BÁO: Regex ^[ABC]-\d{2}$ chỉ khớp mã A-01, B-02, C-03.
    /// Module mới có thể dùng prefix khác. Hãy điều chỉnh logic này.
    /// </summary>
    protected bool IsValidTestCaseCode(string tcCode)
    {
        if (string.IsNullOrWhiteSpace(tcCode)) return false;
        return Regex.IsMatch(tcCode.Trim(), @"^[ABC]-\d{2}$", RegexOptions.IgnoreCase);
    }

    protected string ResolveTestData(string raw) => ExcelHelper.ResolveTestData(raw);

    /// <summary>
    /// ⚠️ CẢNH BÁO: Chỉ parse 2 trường "Tên nhóm" và "Mô tả".
    /// Module mới có trường khác → viết hàm parse riêng trong class con.
    /// </summary>
    protected (string name, string description) ParseMultiField(string raw) =>
        ExcelHelper.ParseMultiField(raw);

    protected ExcelWorksheet RequireWorksheet(ExcelPackage package, string keyword, int fallbackIndex)
    {
        var sheet = package.Workbook.Worksheets.FirstOrDefault(
            ws => !string.IsNullOrWhiteSpace(ws.Name) &&
                  ws.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        if (sheet == null && package.Workbook.Worksheets.Count > fallbackIndex)
            sheet = package.Workbook.Worksheets[fallbackIndex];

        if (sheet == null) throw new AssertionException($"Không tìm thấy worksheet cho '{keyword}'.");
        return sheet;
    }

    protected int FindFirstDataRow(ExcelWorksheet sheet)
    {
        int row = 2;
        while (row <= sheet.Dimension.End.Row)
        {
            if (IsValidTestCaseCode(sheet.Cells[row, 1].Text)) return row;
            row++;
        }
        return 2;
    }

    protected void SaveCaseResult(ExcelWorksheet sheet, int row, string actual, string expected) =>
        ExcelHelper.SaveCaseResult(sheet, row, actual, expected);

    protected void SavePackageWithRetry(ExcelPackage package, int maxAttempts = 5)
    {
        for (int i = 1; i <= maxAttempts; i++)
        {
            try
            {
                package.Save();
                Console.WriteLine($"Đã lưu kết quả vào: {package.File?.FullName}");
                return;
            }
            catch (IOException) when (i < maxAttempts) { Thread.Sleep(400 * i); }
        }
        package.Save();
    }
}
