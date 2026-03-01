---
name: md-to-docx
description: >
  Skill viết file Markdown chuẩn để convert sang DOCX đẹp bằng pandoc.
  LUÔN dùng skill này khi người dùng muốn: viết báo cáo/tài liệu/proposal bằng markdown,
  tạo file .md để export ra Word (.docx), viết tài liệu kỹ thuật/hành chính bằng md,
  hoặc bất kỳ yêu cầu nào có nhắc đến "pandoc", "convert sang docx", "xuất ra Word",
  "tài liệu Word từ markdown". Kể cả khi user chỉ nói "viết báo cáo" mà không rõ format
  hãy tự apply skill này để output ra markdown chuẩn pandoc-docx.
---

# MD to DOCX Skill

Skill dạy AI viết file `.md` đúng chuẩn để khi chạy `pandoc input.md -o output.docx`
cho ra file Word đúng style, đẹp, không bị lỗi.

## Quy Trình (Bắt Buộc)

1. Xác định loại tài liệu (báo cáo? proposal? tài liệu kỹ thuật? luận văn?)
2. Viết YAML frontmatter đúng chuẩn
3. Viết nội dung theo pandoc markdown chuẩn cho DOCX
4. Kèm pandoc command để convert

## Khi Nào Đọc Reference Files

| Tình huống | File cần đọc |
|---|---|
| Viết YAML frontmatter, hiểu style mapping | references/docx-styles.md |
| Cú pháp markdown hoạt động tốt với DOCX | references/md-syntax-for-docx.md |
| Tạo và chỉnh reference-doc.docx | references/reference-doc-guide.md |

Luôn đọc references/docx-styles.md trước khi viết.

## Nguyên Tắc Cốt Lõi

### 1. Pandoc map markdown sang Word Styles

Mọi element trong markdown đều được map sang một Word Style cụ thể.
Muốn output đẹp thì chỉnh styles trong reference-doc.docx, không phải trong .md.

### 2. Blank line là bắt buộc

Phải có blank line trước mỗi heading, list, table, code block.

### 3. Không dùng raw HTML trong md khi convert sang DOCX

HTML tags bị ignore hoàn toàn khi convert sang DOCX.

### 4. Luôn kèm pandoc command

Sau khi viết file md, luôn cung cấp command convert:

```bash
# Co ban
pandoc input.md -o output.docx

# Co reference-doc (khuyen nghi)
pandoc input.md --reference-doc=reference.docx -o output.docx

# Day du nhat
pandoc input.md \
  --reference-doc=reference.docx \
  --toc \
  --number-sections \
  -o output.docx
```

## Template Output Chuan

```markdown
---
title: "Ten Tai Lieu"
author: "Ten Tac Gia"
date: "DD/MM/YYYY"
---

## 1. Gioi Thieu

Noi dung paragraph...

## 2. Noi Dung Chinh

### 2.1 Phan Con

Noi dung...

| Cot 1 | Cot 2 | Cot 3 |
|:------|:-----:|------:|
| Data  | Data  | Data  |

## 3. Ket Luan

Tom tat...
```

## Checklist Truoc Khi Xuat

- YAML co title, author, date?
- Headings dung thu tu (H1 -> H2 -> H3, khong skip)?
- Co blank line truoc moi heading, list, table, code block?
- Bang dung pipe table chuan?
- Khong dung raw HTML?
- Code block co language tag?
- Hinh anh dung duong dan tuong doi?

## Reference Files

- references/docx-styles.md -- Word Style mapping + YAML frontmatter cho DOCX
- references/md-syntax-for-docx.md -- Cu phap markdown hoat dong tot voi DOCX
- references/reference-doc-guide.md -- Cach tao & chinh reference-doc.docx
