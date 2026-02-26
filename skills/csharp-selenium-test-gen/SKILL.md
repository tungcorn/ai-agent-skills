---
name: csharp-selenium-test-gen
description: Hướng dẫn AI Agent tự động viết code C# NUnit Selenium Test (Data-Driven) sử dụng dữ liệu từ ExcelDumperTool và UI-Map YAML.
---

# Hướng Dẫn Tự Động Viết Test C# NUnit Selenium

Skill này hướng dẫn AI Agent cách viết thêm Test tự động mới cho dự án C# NUnit + Selenium, dựa trên dữ liệu Excel và file UI-Map. Agent CHỈ CẦN tạo 1 file duy nhất cho mỗi bài Test và kế thừa từ `BaseTest`.

---

## 1. Cấu Trúc Dự Án & Dependencies

### NuGet Packages (kiểm tra file `.csproj`)
```xml
<PackageReference Include="NUnit" Version="4.*" />
<PackageReference Include="Selenium.WebDriver" Version="4.*" />
<PackageReference Include="Selenium.Support" Version="4.*" />
<PackageReference Include="EPPlus" Version="7.*" />
<PackageReference Include="NUnit3TestAdapter" Version="4.*" />
```

### `using` Statements bắt buộc ở đầu mỗi file Test mới
```csharp
using System;
using System.IO;
using NUnit.Framework;
using OfficeOpenXml;
using OpenQA.Selenium;

namespace TestProductGroup;   // namespace giống project gốc
```

### Vị trí file
- **Code Test:** `TestProductGroup/TestProductGroup/<ModuleName>Test.cs`
- **ExcelDumperTool:** Công cụ riêng, chạy bằng CLI để trích xuất Excel → text
- **Kế thừa:** Mọi file Test ĐỀU PHẢI `public class XYZTest : BaseTest`

---

## 2. Các Lớp Dùng Chung (KHÔNG VIẾT LẠI)

### `BaseTest` — Bắt buộc đọc `examples/BaseTest.cs`

Bạn được gọi trực tiếp trong class con:

| Thành phần | Mô tả |
|---|---|
| `ExcelPath` | const — Đường dẫn tuyệt đối tới file Excel |
| `UrlLogin`, `UrlCategories` | const — URL các trang web |
| `Username`, `Password` | Đọc từ biến môi trường hoặc default |
| `driver`, `wait` | Protected fields — ChromeDriver + WebDriverWait |
| `Setup()` / `TearDown()` | [SetUp]/[TearDown] — **không override** |
| `Login()` | Điều hướng và đăng nhập tự động |
| `OpenCreateForm()` | Mở form `/categories/create` |
| `OpenCategories()` | Mở trang danh sách `/categories` |
| `SetText(id, value)` | Clear + SendKeys vào input theo ID |
| `ClickSubmitButton()` | Click nút Lưu / submit |
| `GetNameError()` | Lấy lỗi validation bên dưới input#name |
| `GetToastMessage()` | Lấy thông báo toast (SweetAlert2) |
| `GetFinalMessage(bool)` | Kết hợp: NameError > Toast > "Hợp lệ" |
| `IsValidTestCaseCode(code)` | ⚠️ Xem mục 5 bên dưới |
| `RequireWorksheet(pkg, keyword, idx)` | Mở sheet theo tên/keyword, fallback index |
| `FindFirstDataRow(sheet)` | Bỏ qua tiêu đề, tìm dòng TC đầu tiên |
| `ResolveTestData(raw)` | Làm sạch: `"-"` → `""`, bỏ ngoặc kép |
| `ParseMultiField(raw)` | ⚠️ Xem mục 5 bên dưới |
| `SaveCaseResult(sheet, row, actual, expected)` | Ghi PASS/FAIL vào cột 7, 8 |
| `SavePackageWithRetry(package)` | Lưu file Excel, retry nếu file đang mở |

### `ExcelHelper` — Bắt buộc đọc `examples/ExcelHelper.cs`

Lớp `static` chứa logic xử lý Excel ở mức thấp. `BaseTest` chỉ là wrapper gọi tới đây. Agent **không cần** viết lại.

---

## 3. Quy Trình 4 Bước Viết Test Mới

### Bước 1: Trích Xuất Dữ Liệu Excel
Chạy ExcelDumperTool và xuất ra file:
```bash
dotnet run --project "<ABSOLUTE_PATH_TO_EXCEL_DUMPER_TOOL>" -- "<ABSOLUTE_PATH_TO_EXCEL_FILE>" all > all_sheets_output.txt
```
Đọc file `all_sheets_output.txt`. Mỗi dòng có dạng:
```
R001:   [Cột1-MãTC]   [Cột2-Trường]   [Cột3]   [Cột4]   [Cột5-Input]   [Cột6-Expected]
```
Cột 5 = dữ liệu nhập vào, Cột 6 = kết quả kỳ vọng.

### Bước 2: Đọc Tọa Độ UI (UI-Map)
Yêu cầu QA cung cấp `ui-map.yaml`. Tham khảo `ui-map-template.yaml` để hiểu format.

> ⚠️ **Radix UI / Shadcn UI** sinh ID dạng `_r_6_` tự động, không ổn định. BẮT BUỘC dùng XPath Text cho combobox: `//div[@role='option'][contains(text(), 'Giá trị')]`

### Bước 3: Tùy Biến Cho Module Mới (Checklist)
Trước khi sinh code, xác định các điểm cần thay đổi:

- [ ] **Tên class & file:** `<Module><Action>Test.cs` (VD: `EmployeeCreateTest.cs`)
- [ ] **Tên sheet Excel:** Sheet chứa test cases của module mới
- [ ] **Mã TC prefix:** VD: module Nhóm hàng dùng `A-`, `B-`, `C-`. Module mới có thể khác → **sửa regex trong `IsValidTestCaseCode`** (xem mục 5)
- [ ] **URL điều hướng:** Nếu module dùng URL khác `/categories`, cần thêm const và hàm `Open...Form()` trong class con
- [ ] **ParseMultiField:** Nếu dữ liệu Excel có nhiều trường (không chỉ Tên + Mô tả), phải viết hàm parse riêng (xem mục 5)
- [ ] **Element IDs / Selectors:** Lấy từ UI-Map, kiểm tra bằng DevTools

### Bước 4: Sinh File Test C#
Tạo file tuân thủ cấu trúc:
1. `[NonParallelizable]` attribute trên class.
2. Class kế thừa `BaseTest`.
3. Hàm `ExecuteCase(tcCode, field, rawData)` → fill form → trả về `string actual`.
4. `[Test] public void TestCases()` → vòng lặp đọc Excel → gọi `ExecuteCase` → `SaveCaseResult`.

---

## 4. Edge Cases Quan Trọng

| Tình huống | Cách xử lý |
|---|---|
| TC "Quay lại / Hủy bỏ" | Click XPath `//a[contains(.,'Quay lại')]`, trả về string mô tả URL hiện tại |
| Dấu `-` trong Excel | `ResolveTestData` đã tự chuyển thành `""` |
| Cần nhập 2 trường trong 1 TC (multi-field) | Gọi `ParseMultiField` (xem cảnh báo mục 5) |
| Combobox / Dropdown ảo (không phải `<select>`) | Click trigger → Click option bằng XPath Text (2 bước) |
| Delete với Confirmation Modal | Xem mẫu `examples/ProductGroupDeleteTest.cs` |

---

## 5. Cảnh Báo Quan Trọng — PHẢI ĐỌC

### ⚠️ `IsValidTestCaseCode` — Regex cứng theo module
```csharp
// BaseTest.cs hiện tại:
Regex.IsMatch(tcCode, @"^[ABC]-\d{2}$")   // Chỉ khớp A-01, B-02, C-03...
```
Nếu module mới dùng mã khác (VD: `EMP-01`, `D-01`), bạn **PHẢI override hoặc điều chỉnh** logic này. Cách đơn giản nhất là đặt điều kiện check rộng hơn trong vòng lặp test của class con.

### ⚠️ `ParseMultiField` — Hardcode cho "Nhóm hàng hóa"
```csharp
// ExcelHelper.cs hiện tại chỉ nhận biết:
line.StartsWith("Tên nhóm hàng hóa") → name
line.StartsWith("Mô tả")             → description
```
**Nếu module mới có trường khác** (VD: Nhân viên có `Họ tên`, `Ngày sinh`, `Giới tính`, `Địa chỉ`), hàm này sẽ trả về rỗng. Bạn cần viết hàm parse riêng trong class con hoặc file Helper mới.

**Mẫu viết hàm parse riêng:**
```csharp
private (string fullName, string birthDate, string gender, string address) ParseEmployeeData(string raw)
{
    string fullName = "", birthDate = "", gender = "", address = "";
    if (string.IsNullOrWhiteSpace(raw)) return (fullName, birthDate, gender, address);

    var lines = raw.Replace("\r", "").Split('\n');
    foreach (var line in lines.Select(x => x.Trim()))
    {
        if (line.StartsWith("Họ tên", StringComparison.OrdinalIgnoreCase))
            fullName = line[(line.IndexOf(':') + 1)..].Trim();
        else if (line.StartsWith("Ngày sinh", StringComparison.OrdinalIgnoreCase))
            birthDate = line[(line.IndexOf(':') + 1)..].Trim();
        else if (line.StartsWith("Giới tính", StringComparison.OrdinalIgnoreCase))
            gender = line[(line.IndexOf(':') + 1)..].Trim();
        else if (line.StartsWith("Địa chỉ", StringComparison.OrdinalIgnoreCase))
            address = line[(line.IndexOf(':') + 1)..].Trim();
    }
    return (fullName, birthDate, gender, address);
}
```

---

## 6. Tham Khảo Code Mẫu

| File | Nội dung |
|---|---|
| `examples/BaseTest.cs` | Lớp cơ sở đầy đủ với null-guard, hàm navigation, Excel helpers |
| `examples/ExcelHelper.cs` | Lớp static xử lý parse/ghi Excel |
| `examples/ProductGroupCreateTest.cs` | Test mẫu: Create — vòng lặp Data-Driven cơ bản |
| `examples/ProductGroupDeleteTest.cs` | Test mẫu: Delete — xử lý confirmation modal |

**Thứ tự đọc bắt buộc:** `BaseTest.cs` → `ExcelHelper.cs` → `ProductGroupCreateTest.cs` → `ProductGroupDeleteTest.cs` → viết file mới.
