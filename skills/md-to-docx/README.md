# md-to-docx Skill

Skill dạy AI viết Markdown chuẩn để convert sang DOCX đẹp bằng `pandoc`.

---

## Cấu Trúc

```
md-to-docx/
├── SKILL.md                          ← File chính của skill (AI đọc đầu tiên)
├── README.md                         ← File này
├── references/
│   ├── 01-yaml.md                    ← YAML frontmatter cho DOCX
│   ├── 02-syntax.md                  ← Cú pháp Markdown → Word styles mapping
│   ├── 03-reference-doc.md           ← Tùy chỉnh template Word
│   └── 04-commands.md                ← Lệnh pandoc & troubleshooting
└── templates/
    └── document-template.md          ← Template md mẫu dùng ngay
```

---

## Cách Cài Vào Claude

1. Zip toàn bộ thư mục `md-to-docx/`
2. Đổi đuôi thành `.skill`
3. Upload vào **Settings → Skills** trong Claude.ai

---

## Cách Dùng Nhanh

```bash
# Lệnh cơ bản
pandoc input.md -o output.docx

# Có template Word
pandoc input.md --reference-doc=template.docx --toc -o output.docx

# Lấy default reference.docx của pandoc để chỉnh
pandoc --print-default-data-file reference.docx > my-reference.docx
```

---

## Pandoc Styles Mapping

| Markdown | Word Style |
|---|---|
| `# H1` | Heading 1 |
| `## H2` | Heading 2 |
| `### H3` | Heading 3 |
| Paragraph | Body Text |
| Paragraph đầu sau heading | First Paragraph |
| `` `code` `` | Verbatim Char |
| ```` ```code``` ```` | Source Code |
| `> quote` | Block Text |
| Bảng | Normal Table |
| `Table: caption` | Table Caption |
