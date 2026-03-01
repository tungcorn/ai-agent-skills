# 03 — Reference-Doc: Tùy Chỉnh Style Word

`reference-doc.docx` là file Word dùng làm **template style** cho pandoc.
Pandoc bỏ qua nội dung của nó, nhưng lấy toàn bộ style definitions, margin,
font, header/footer để áp vào file output.

---

## Tại Sao Cần reference-doc?

Không có reference-doc → pandoc dùng style mặc định: font Calibri 11pt, không có
header/footer, margin mặc định, heading style đơn giản.

Có reference-doc → output DOCX dùng đúng font công ty, logo, margin, màu heading,
style bảng... như template chuẩn.

---

## Bước 1: Lấy Default Reference-Doc

```bash
# Tạo file reference-doc mặc định của pandoc
pandoc --print-default-data-file reference.docx > my-reference.docx

# Sau đó mở my-reference.docx trong Word để chỉnh
```

---

## Bước 2: Danh Sách Styles Pandoc Dùng (Từ Official Manual)

Chỉ chỉnh các styles này — **không thêm/xóa styles khác** để tránh lỗi:

### Paragraph Styles
| Style Name | Dùng cho |
|---|---|
| `Normal` | Paragraph mặc định |
| `Body Text` | Paragraph nội dung chính |
| `First Paragraph` | Paragraph đầu tiên sau heading |
| `Compact` | List item (compact list) |
| `Title` | Tiêu đề (từ YAML `title`) |
| `Subtitle` | Phụ đề (từ YAML `subtitle`) |
| `Author` | Tác giả (từ YAML `author`) |
| `Date` | Ngày (từ YAML `date`) |
| `Abstract` | Tóm tắt (từ YAML `abstract`) |
| `Bibliography` | Danh sách tài liệu tham khảo |
| `Heading 1` | Heading cấp 1 (`#`) |
| `Heading 2` | Heading cấp 2 (`##`) |
| `Heading 3` | Heading cấp 3 (`###`) |
| `Heading 4` | Heading cấp 4 (`####`) |
| `Heading 5` | Heading cấp 5 |
| `Heading 6` | Heading cấp 6 |
| `Block Text` | Block quote (`>`) |
| `Footnote Text` | Nội dung footnote |
| `Definition Term` | Definition list — term |
| `Definition` | Definition list — nội dung |
| `Caption` | Caption chung |
| `Table Caption` | Caption bảng (`Table: ...`) |
| `Image Caption` | Caption hình |
| `Figure` | Figure block |
| `Figure With Caption` | Figure có caption |
| `TOC Heading` | Tiêu đề của mục lục |

### Character Styles
| Style Name | Dùng cho |
|---|---|
| `Default Paragraph Font` | Font mặc định |
| `Body Text Char` | Inline body text |
| `Verbatim Char` | Inline code (`` `code` ``) |
| `Footnote Reference` | Số footnote superscript |
| `Hyperlink` | Link (`[text](url)`) |

### Table Styles
| Style Name | Dùng cho |
|---|---|
| `Normal Table` | Toàn bộ bảng |

**Lưu ý quan trọng:** Style `Source Code` cho code block **không có trong reference-doc mặc định** — pandoc tạo nó dynamically. Để chỉnh code block style → chỉnh `Verbatim Char` character style.

---

## Bước 3: Cách Chỉnh Style Trong Word

```
1. Mở my-reference.docx trong Word
2. Home tab → Styles pane (hoặc Ctrl+Alt+Shift+S)
3. Click phải vào style cần chỉnh → Modify
4. Chỉnh font, size, color, spacing, indent...
5. Save file
```

**Những gì nên chỉnh:**
- `Heading 1/2/3` → font, size, color, spacing before/after
- `Body Text` → font, size, line spacing, paragraph spacing
- `Normal Table` → border style, padding, alignment
- `Title/Subtitle/Author/Date` → font, size, alignment
- `Verbatim Char` → monospace font (Consolas, Courier New, JetBrains Mono)
- `Block Text` → indent, border-left, background (nếu có)
- Margin trang → Layout → Margins
- Header/Footer → Insert → Header/Footer

**Những gì KHÔNG nên làm:**
- Xóa styles mà pandoc cần
- Rename styles (pandoc tìm theo tên chính xác)
- Thêm content vào file (bị ignore, nhưng có thể gây nhầm lẫn)

---

## Bước 4: Sử Dụng reference-doc

```bash
# CLI
pandoc input.md --reference-doc=my-reference.docx -o output.docx

# Hoặc đặt mặc định (không cần truyền CLI mỗi lần)
mkdir -p ~/.pandoc
cp my-reference.docx ~/.pandoc/reference.docx
# Sau đó chỉ cần:
pandoc input.md -o output.docx
```

---

## Nhiều Templates Cho Nhiều Mục Đích

```bash
# Template khác nhau cho từng loại tài liệu
pandoc input.md --reference-doc=template-report.docx -o report.docx
pandoc input.md --reference-doc=template-proposal.docx -o proposal.docx
pandoc input.md --reference-doc=template-internal.docx -o internal.docx
```

---

## Chỉnh Bảng Căn Giữa (Hay Bị Lỗi)

Bảng thường bị căn trái mặc định. Để căn giữa:

```
1. Mở reference-doc.docx trong Word
2. Click vào một bảng bất kỳ (tạo dummy table nếu chưa có)
3. Table Tools → Layout → Properties → Table → Alignment → Center
4. Click phải vào style "Normal Table" → Update Normal Table to Match Selection
5. Save
```

**Lưu ý:** Nếu Header row có alignment riêng (Left) → nó sẽ override alignment của
"Normal Table". Phải check và xóa alignment override của Header row.

---

## Tips Nâng Cao

### Dùng .dotx (Word Template) thay vì .docx
```bash
# .dotx cũng được chấp nhận
pandoc input.md --reference-doc=company-template.dotx -o output.docx
```

### Kiểm Tra Style Nào Đang Dùng Trong Output
```bash
# Unzip DOCX để xem XML
unzip -o output.docx -d output_dir
cat output_dir/word/styles.xml | grep -o 'w:styleId="[^"]*"' | sort | uniq
```

### Lua Filter Để Thêm Logic Tùy Chỉnh
```lua
-- pagebreak.lua: convert --- thành page break thực sự
function HorizontalRule()
  return pandoc.RawBlock('openxml',
    '<w:p><w:r><w:br w:type="page"/></w:r></w:p>')
end
```
```bash
pandoc input.md --lua-filter=pagebreak.lua --reference-doc=template.docx -o output.docx
```
