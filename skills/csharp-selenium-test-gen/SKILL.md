---
name: csharp-selenium-test-gen
description: Hướng dẫn AI Agent tự động viết code C# NUnit Selenium Test (Data-Driven) sử dụng dữ liệu từ ExcelDumperTool và UI-Map YAML.
---

# Hướng Dẫn Tự Động Viết Test C# NUnit Selenium

Skill này hướng dẫn AI Agent cách viết thêm Test tự động mới cho dự án C# NUnit + Selenium, dựa trên dữ liệu Excel và file UI-Map. Khi **thêm test cho một module mới**, Agent CHỈ CẦN tạo **1 file** (ví dụ `EmployeeCreateTest.cs`) và kế thừa từ `BaseTest` — vì `BaseTest.cs` và `ExcelHelper.cs` đã có sẵn trong dự án, không cần tạo lại.

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

> ### ⛔ Điều Kiện Tiên Quyết — Hỏi Trước Khi Làm
>
> Trước khi thực hiện bất kỳ bước nào, Agent **PHẢI** kiểm tra và hỏi người dùng nếu chưa được cung cấp:
>
> 1. **Đường dẫn file Excel** (test data) — nếu chưa có, hỏi:
>    > "Bạn vui lòng cung cấp đường dẫn tuyệt đối đến file Excel chứa test cases (VD: `D:\project\test-data.xlsx`)?"
>
> 2. **File UI-Map YAML** (tọa độ các element trên UI) — nếu chưa có, hỏi:
>    > "Bạn vui lòng cung cấp nội dung hoặc đường dẫn file `ui-map.yaml` cho module cần viết test?"
>
> **Không được tự bịa hoặc giả định** hai thông tin trên. Nếu thiếu một trong hai, dừng lại và hỏi trước.

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

- [ ] **Loại hành động:** Xác định đây là loại test gì (Create / Edit / Delete / Search / Export / ...). Mỗi loại cần hàm điều hướng khác nhau (`OpenCreateForm`, `OpenFirstEditForm`, `OpenSearchPage`...). Không dùng mẫu của loại khác
- [ ] **Cách đọc kết quả:** Create/Edit/Delete thường đọc toast hoặc lỗi validation. Loại khác (VD: Search) có thể cần đọc số dòng trong bảng, nội dung cell, hoặc trạng thái hiển thị — **hỏi người dùng nếu chưa rõ expected output là gì**
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
| **Edit test** — mở form chỉnh sửa | Không điều hướng tới `/create`. Dùng helper `OpenFirstEditForm()`: `OpenCategories()` rồi click `//a[contains(@href,'/categories/') and contains(@href,'/edit')]`. Xem mẫu `examples/ProductGroupEditTest.cs` |

### Quy ước `fallbackIndex` trong `RequireWorksheet`

`RequireWorksheet` tìm sheet **theo tên (keyword) trước** — `fallbackIndex` chỉ là lưới an toàn cuối cùng nếu không tìm thấy sheet theo tên.

> ⚠️ **Không giả định thứ tự sheet.** Số lượng và loại test (Create, Edit, Delete, Search, Export, ...) hoàn toàn phụ thuộc vào file Excel của từng dự án.

**Nguyên tắc xác định `fallbackIndex`:**
1. **Ưu tiên dùng keyword** — đặt keyword khớp với tên sheet thực tế trong Excel (VD: `"Tìm kiếm"`, `"Xuất dữ liệu"`).
2. **Hỏi người dùng** nếu chưa biết tên sheet — đừng tự đoán `fallbackIndex`.
3. Nếu biết thứ tự sheet (VD: sheet 0=Create, 1=Edit, 2=Delete, 3=Search), dùng đúng index đó.

**Ví dụ dự án có 4 loại test:**
```csharp
// Create
var sheet = RequireWorksheet(package, "Thêm", fallbackIndex: 0);
// Edit
var sheet = RequireWorksheet(package, "Chỉnh sửa", fallbackIndex: 1);
// Delete
var sheet = RequireWorksheet(package, "Xóa", fallbackIndex: 2);
// Search — loại bổ sung, keyword ưu tiên
var sheet = RequireWorksheet(package, "Tìm kiếm", fallbackIndex: 3);
```

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
| `examples/ProductGroupEditTest.cs` | Test mẫu: Edit — điều hướng đến record đã có, mở form Edit |
| `examples/ProductGroupDeleteTest.cs` | Test mẫu: Delete — xử lý confirmation modal |

**Thứ tự đọc bắt buộc:** `BaseTest.cs` → `ExcelHelper.cs` → `ProductGroupCreateTest.cs` → `ProductGroupEditTest.cs` → `ProductGroupDeleteTest.cs` → viết file mới.
