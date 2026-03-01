# Reference-Doc Guide — Tạo & Chỉnh Word Template

## 1. Reference-Doc Là Gì?

`reference-doc.docx` là file Word mà pandoc dùng để lấy **styles**.
Pandoc đọc styles từ file này rồi áp dụng vào nội dung convert từ markdown.

```
input.md  ──pandoc──▶  content (từ .md)
                   +   styles (từ reference-doc.docx)
                   ──▶  output.docx (đẹp, đúng format)
```

**Content của reference-doc bị bỏ qua hoàn toàn** — chỉ lấy styles.

---

## 2. Bước 1: Tạo Reference-Doc Mặc Định

```bash
# Tạo file reference-doc mặc định từ pandoc
pandoc -o reference.docx --print-default-data-file reference.docx

# Mở file này trong Word hoặc LibreOffice Writer để chỉnh styles
```

---

## 3. Bước 2: Chỉnh Styles Trong Word

### Cách mở Styles pane trong Word

```
Home tab → Styles group → click mũi tên góc dưới phải
Hoặc: Alt + Ctrl + Shift + S
```

### Cách chỉnh một style

```
1. Click vào text có style muốn chỉnh (VD: click vào Heading 1)
2. Trong Styles pane → right-click tên style → "Modify..."
3. Chỉnh font, size, color, spacing, indent...
4. OK → Save file
```

### Styles quan trọng nhất cần chỉnh

| Style | Chỉnh gì | Gợi ý |
|---|---|---|
| `Normal` | Font, size, line spacing | Font chính của tài liệu |
| `Heading 1` | Font, size, color, spacing | Section title chính |
| `Heading 2` | Font, size, color | Subsection |
| `Heading 3` | Font, size | Sub-subsection |
| `Title` | Font lớn, bold, center | Tiêu đề trang bìa |
| `Subtitle` | Font vừa, italic | Phụ đề |
| `Author` | Font, align | Tên tác giả |
| `Table` | Border style | Định dạng bảng |
| `Verbatim Char` | Font mono, background | Inline code |
| `Block Text` | Indent, border left | Blockquote |
| `First Paragraph` | Indent đầu dòng | Paragraph sau heading |

### Chỉnh Table style

```
1. Click vào bảng bất kỳ
2. Table Design tab (xuất hiện khi click vào bảng)
3. Chỉnh Table Styles
4. Right-click style đang dùng → Modify Table Style
```

---

## 4. Bước 3: Chỉnh Page Setup Trong Reference-Doc

Pandoc cũng đọc **page settings** (margins, paper size, orientation, header/footer)
từ reference-doc:

```
Layout tab → Page Setup:
- Paper: A4 (21cm × 29.7cm)
- Margins: Top 2.5cm, Bottom 2.5cm, Left 3cm, Right 2.5cm
- Orientation: Portrait

Header & Footer:
- Insert tab → Header / Footer
- Thêm page number, tên tài liệu, logo công ty...
```

---

## 5. Pandoc Commands Thông Dụng

### Cơ bản nhất

```bash
pandoc input.md -o output.docx
```

### Có reference-doc

```bash
pandoc input.md \
  --reference-doc=reference.docx \
  -o output.docx
```

### Có TOC (Table of Contents)

```bash
pandoc input.md \
  --reference-doc=reference.docx \
  --toc \
  -o output.docx
```

### Có TOC + đánh số section tự động

```bash
pandoc input.md \
  --reference-doc=reference.docx \
  --toc \
  --toc-depth=3 \
  --number-sections \
  -o output.docx
```

### Tài liệu nhiều file (ghép nhiều .md)

```bash
pandoc chapter1.md chapter2.md chapter3.md \
  --reference-doc=reference.docx \
  --toc \
  -o full-document.docx
```

### Dùng defaults file (clean nhất, dùng khi convert thường xuyên)

```yaml
# defaults.yaml
from: markdown+smart
to: docx
reference-doc: reference.docx
toc: true
toc-depth: 3
number-sections: true
```

```bash
pandoc input.md -d defaults.yaml -o output.docx
```

---

## 6. Tips & Gotchas

### TOC không update tự động

Sau khi mở output.docx trong Word:

```
Ctrl + A (chọn tất cả) → F9 (update fields)
Hoặc: Right-click vào TOC → "Update Field" → "Update entire table"
```

### Numbered headings không hiển thị đúng

Pandoc dùng `--number-sections` để thêm số vào heading.
Nếu muốn Word tự đánh số (linh hoạt hơn), cần chỉnh "List Number" style trong reference-doc.

### Images quá lớn hoặc quá nhỏ

```markdown
<!-- Kiểm soát width trong markdown -->
![Caption](image.png){width=80%}
![Caption](image.png){width=10cm}
```

### Font tiếng Việt bị lỗi

- Đảm bảo reference-doc.docx dùng font hỗ trợ Unicode (Times New Roman, Arial, Calibri)
- Thêm `lang: vi` vào YAML frontmatter

### Bảng bị vỡ layout

- Giảm số cột
- Dùng `{width=X%}` cho các cột
- Hoặc dùng Grid Table thay Pipe Table cho bảng phức tạp

### Code block không có syntax highlighting trong DOCX

DOCX không hỗ trợ syntax highlighting natively như PDF.
Giải pháp: Chỉnh "Source Code" hoặc "Verbatim Char" style trong reference-doc
để có background color (VD: màu xám nhạt) và font monospace.

---

## 7. Cấu Trúc Thư Mục Khuyến Nghị

```
project/
├── input.md              # File markdown chính
├── reference.docx        # Word template (chỉnh styles)
├── defaults.yaml         # Pandoc defaults (tùy chọn)
├── images/               # Thư mục chứa ảnh
│   ├── diagram.png
│   └── chart.png
└── output.docx           # Output (được tạo tự động)
```

