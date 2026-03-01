# 01 — YAML Frontmatter Cho DOCX

YAML frontmatter là khối `---` ở đầu file, kiểm soát metadata và một số
tùy chọn của pandoc khi tạo DOCX.

---

## Template YAML Tối Thiểu

```yaml
---
title: "Tên Tài Liệu"
author: "Nguyen Van A"
date: "2024-01-15"
---
```

## Template YAML Đầy Đủ Cho DOCX

```yaml
---
# === Metadata ===
title: "Tiêu Đề Tài Liệu: Có Dấu Hai Chấm Phải Dùng Nháy"
subtitle: "Phụ đề (nếu có)"
author:
  - Nguyen Van A
  - Tran Thi B          # Nhiều tác giả → dùng list
date: "15 tháng 1, 2024"
lang: vi                # Ngôn ngữ — ảnh hưởng hyphenation và spell check

# === Cấu trúc ===
toc: true               # Tạo mục lục tự động
toc-depth: 3            # Độ sâu mục lục (H2, H3, H4)
number-sections: true   # Đánh số section tự động (1. 1.1 1.1.1)

# === Reference doc (template Word) ===
# Đặt ở đây hoặc truyền qua CLI --reference-doc
# reference-doc: template.docx   ← comment out nếu dùng CLI

# === Abstract (nếu cần) ===
abstract: |
  Tóm tắt ngắn về nội dung tài liệu.
  Có thể nhiều dòng dùng block scalar `|`.
---
```

---

## Lưu Ý Quan Trọng Về YAML

### Chuỗi có ký tự đặc biệt → phải dùng nháy đôi
```yaml
# ❌ Lỗi YAML: dấu : không được bỏ trống
title: Báo Cáo: Phần 1

# ✅ Đúng
title: "Báo Cáo: Phần 1"

# Các ký tự cần nháy: : # & * ? | > ' " { } [ ] , !
```

### Multiline string dùng `|` (giữ nguyên xuống dòng)
```yaml
abstract: |
  Dòng đầu tiên.
  Dòng thứ hai.
  
  Đoạn mới sau blank line.
```

### Multiline string dùng `>` (gộp thành một dòng)
```yaml
description: >
  Tất cả dòng này
  sẽ được nối thành
  một dòng duy nhất.
```

### Số bắt đầu bằng 0 phải dùng nháy
```yaml
# ❌ YAML parse thành số 3366, mất số 0
some-color: 003366

# ✅
some-color: "003366"
```

### Boolean đúng chuẩn YAML
```yaml
toc: true    # ✅
toc: True    # ❌ Python style
toc: yes     # ❌ không phải YAML boolean chuẩn
```

---

## Fields Ảnh Hưởng Đến DOCX

| Field | Tác dụng | Ví dụ |
|---|---|---|
| `title` | Tiêu đề, map sang style `Title` trong Word | `title: "Báo Cáo"` |
| `subtitle` | Phụ đề, map sang style `Subtitle` | `subtitle: "Phiên bản 1"` |
| `author` | Tác giả, map sang style `Author` | `author: "Nguyen Van A"` |
| `date` | Ngày, map sang style `Date` | `date: "2024-01-15"` |
| `abstract` | Tóm tắt, map sang style `Abstract` | (xem ví dụ trên) |
| `toc` | Chèn mục lục vào DOCX | `toc: true` |
| `toc-depth` | Độ sâu mục lục | `toc-depth: 3` |
| `number-sections` | Đánh số heading tự động | `number-sections: true` |
| `lang` | Ngôn ngữ tài liệu | `lang: vi` |

---

## Mapping YAML → Word Styles

Pandoc map các YAML fields sang Word paragraph styles:

```
title      →  Word style "Title"
subtitle   →  Word style "Subtitle"  
author     →  Word style "Author"
date       →  Word style "Date"
abstract   →  Word style "Abstract"
```

Muốn thay đổi font/size/color → chỉnh style tương ứng trong `reference-doc.docx`
(xem `references/03-reference-doc.md`).
