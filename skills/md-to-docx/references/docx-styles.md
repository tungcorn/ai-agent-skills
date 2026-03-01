# Word Style Mapping & YAML Frontmatter cho DOCX

## 1. Pandoc Markdown → Word Style Mapping (Đầy Đủ)

Đây là toàn bộ danh sách styles mà pandoc dùng khi convert sang DOCX.
Nguồn: pandoc manual chính thức.

### Paragraph Styles

| Markdown element | Word Style name |
|---|---|
| Paragraph bình thường | `Normal` |
| Paragraph sau heading | `First Paragraph` |
| Paragraph trong list | `Compact` |
| `##`, `###`... | `Heading 1` → `Heading 9` |
| `title:` trong YAML | `Title` |
| `subtitle:` trong YAML | `Subtitle` |
| `author:` trong YAML | `Author` |
| `date:` trong YAML | `Date` |
| `abstract:` trong YAML | `Abstract` |
| `> blockquote` | `Block Text` |
| Footnote content | `Footnote Text` |
| Definition term | `Definition Term` |
| Definition body | `Definition` |
| Caption của bảng | `Table Caption` |
| Caption của hình | `Image Caption` |
| Figure wrapper | `Figure` / `Captioned Figure` |
| Bibliography entry | `Bibliography` |
| TOC heading | `TOC Heading` |

### Character Styles

| Markdown element | Word Character Style |
|---|---|
| `**bold**`, `*italic*`... | `Default Paragraph Font` |
| `` `inline code` `` | `Verbatim Char` |
| `[^1]` footnote marker | `Footnote Reference` |
| `[link](url)` | `Hyperlink` |
| Normal text trong Body Text | `Body Text Char` |

### Table Style

| Element | Style |
|---|---|
| Mọi bảng | `Table` |

### Lưu ý quan trọng về Source Code

```
"Source Code" style được pandoc TẠO ĐỘNG — KHÔNG có trong reference-doc.docx mặc định.
Để chỉnh style của code block, hãy chỉnh "Verbatim Char" character style.
```

---

## 2. YAML Frontmatter cho DOCX

### Template cơ bản (luôn dùng)

```yaml
---
title: "Tên Tài Liệu"
author: "Nguyễn Văn A"
date: "15/01/2024"
---
```

### Template đầy đủ

```yaml
---
title: "Báo Cáo Kết Quả Dự Án ABC"
subtitle: "Quý 1 — 2024"
author:
  - Nguyễn Văn A
  - Trần Thị B
date: "15 tháng 1, 2024"
abstract: |
  Tóm tắt nội dung tài liệu.
  Viết ngắn gọn, 3-5 câu.
lang: vi
toc: true
toc-depth: 3
number-sections: true
reference-doc: reference.docx
---
```

### Giải thích từng field

| Field | Mục đích | Ví dụ |
|---|---|---|
| `title` | Tiêu đề chính → style "Title" | `"Báo Cáo Tháng 1"` |
| `subtitle` | Phụ đề → style "Subtitle" | `"Phiên bản 2.0"` |
| `author` | Tác giả → style "Author" | `"Nguyễn Văn A"` |
| `date` | Ngày → style "Date" | `"15/01/2024"` |
| `abstract` | Tóm tắt → style "Abstract" | Dùng `\|` cho multiline |
| `lang` | Ngôn ngữ (quan trọng!) | `vi` hoặc `en` |
| `toc` | Tạo Table of Contents | `true` / `false` |
| `toc-depth` | Độ sâu TOC | `2` hoặc `3` |
| `number-sections` | Đánh số section tự động | `true` / `false` |
| `reference-doc` | File DOCX template | `reference.docx` |

### Lỗi YAML phổ biến

```yaml
# SAI: Dấu : trong title không có nháy
title: Báo Cáo: Tháng 1     # lỗi YAML!

# ĐÚNG:
title: "Báo Cáo: Tháng 1"

# SAI: Tab indent
author:
	Nguyễn Văn A              # dùng tab!

# ĐÚNG: 2 spaces
author:
  - Nguyễn Văn A
  - Trần Thị B

# SAI: Multiline abstract không có pipe
abstract: Dòng 1
Dòng 2                        # dòng 2 sẽ bị parse thành key khác!

# ĐÚNG:
abstract: |
  Dòng 1
  Dòng 2
```

---

## 3. Custom Style với Fenced Divs và Spans

Pandoc cho phép áp dụng **custom Word style** trực tiếp từ markdown:

### Paragraph style tùy chỉnh (Fenced Div)

```markdown
::: {custom-style="Appendix Heading"}
Phụ Lục A — Tài Liệu Đính Kèm
:::

::: {custom-style="Warning Box"}
Lưu ý: Thông tin này chỉ dành cho nội bộ.
:::
```

### Character style tùy chỉnh (Span)

```markdown
Văn bản [quan trọng]{custom-style="Highlight"} cần chú ý.

Tên sản phẩm [ACME Pro]{custom-style="Product Name"} được...
```

**Yêu cầu:** Style đó phải tồn tại trong `reference-doc.docx` trước.

