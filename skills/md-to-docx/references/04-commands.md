# 04 — Lệnh Pandoc & Troubleshooting

---

## Lệnh Pandoc Theo Tình Huống

### Đơn giản nhất
```bash
pandoc input.md -o output.docx
```

### Có mục lục
```bash
pandoc input.md --toc -o output.docx
```

### Có template + mục lục
```bash
pandoc input.md \
  --reference-doc=template.docx \
  --toc \
  --toc-depth=3 \
  -o output.docx
```

### Đánh số section tự động
```bash
pandoc input.md \
  --reference-doc=template.docx \
  --toc \
  --number-sections \
  -o output.docx
```

### Nhiều file md ghép thành 1 docx
```bash
pandoc chapter1.md chapter2.md chapter3.md \
  --reference-doc=template.docx \
  --toc \
  -o full-document.docx
```

### Có citations (BibTeX)
```bash
pandoc input.md \
  --reference-doc=template.docx \
  --bibliography=references.bib \
  --csl=apa.csl \
  --citeproc \
  -o output.docx
```

### Dùng defaults file (sạch nhất, khuyến nghị khi có nhiều options)
```bash
pandoc input.md -d defaults.yaml -o output.docx
```

---

## Defaults File (Khuyến Nghị)

Thay vì gõ options dài trên CLI, tạo file `defaults.yaml`:

```yaml
# defaults.yaml — cho MD to DOCX
from: markdown+smart+footnotes+pipe_tables+fenced_code_blocks
to: docx
reference-doc: template.docx
toc: true
toc-depth: 3
number-sections: false   # true nếu muốn đánh số
standalone: true
```

Sử dụng:
```bash
pandoc input.md -d defaults.yaml -o output.docx
```

---

## Pandoc Extensions Quan Trọng Cho DOCX

Thêm vào `--from` để bật tính năng:

```bash
# Bật nhiều extensions
pandoc input.md \
  --from markdown+smart+footnotes+pipe_tables+fenced_code_blocks+definition_lists \
  -o output.docx
```

| Extension | Tác dụng |
|---|---|
| `+smart` | Smart quotes, em dash, ellipsis tự động |
| `+footnotes` | Footnotes `[^1]` và inline `^[...]` |
| `+pipe_tables` | Bảng dạng pipe `\|col\|col\|` |
| `+fenced_code_blocks` | Code block dạng ` ``` ` |
| `+definition_lists` | Definition list `Term\n:   Definition` |
| `+strikeout` | Gạch ngang `~~text~~` |
| `+superscript` | Superscript `x^2^` |
| `+subscript` | Subscript `H~2~O` |

---

## Kiểm Tra Phiên Bản & Cài Đặt

```bash
# Kiểm tra pandoc đã cài chưa
pandoc --version

# Xem output formats hỗ trợ
pandoc --list-output-formats

# Xem extensions của markdown
pandoc --list-extensions markdown

# Lấy default reference.docx
pandoc --print-default-data-file reference.docx > my-reference.docx

# Lấy default CSS (cho HTML)
pandoc --print-default-data-file pandoc.css > pandoc.css
```

---

## Cài Đặt Pandoc

```bash
# macOS
brew install pandoc

# Ubuntu/Debian
sudo apt install pandoc

# Windows (Chocolatey)
choco install pandoc

# Windows (Winget)
winget install JohnMacFarlane.Pandoc

# Hoặc download installer tại: https://pandoc.org/installing.html
```

---

## Troubleshooting Thường Gặp

### ❌ Lỗi: Table không hiển thị đúng

**Nguyên nhân:** Thiếu separator row hoặc sai cú pháp

```markdown
# ❌ Sai
| Col 1 | Col 2 |
| Data  | Data  |

# ✅ Đúng
| Col 1 | Col 2 |
|-------|-------|
| Data  | Data  |
```

---

### ❌ Lỗi: Heading không dùng style đúng trong Word

**Nguyên nhân:** Heading ngay sau text không có blank line

```markdown
# ❌ Sai — không có blank line trước heading
Paragraph text...
## Heading 2

# ✅ Đúng
Paragraph text...

## Heading 2
```

---

### ❌ Lỗi: Code block bị ghép vào list item

**Nguyên nhân:** Code block indent 4 spaces sau list item

```markdown
# ❌ Sai
1. Item đầu tiên

    ```python
    code...
    ```

# ✅ Cách 1: Kết thúc list bằng comment HTML
1. Item đầu tiên

<!-- end list -->

```python
code...
```

# ✅ Cách 2: Dùng fenced div
1. Item đầu tiên

~~~python
code...
~~~
```

---

### ❌ Lỗi: Ảnh không load trong DOCX

**Nguyên nhân:** Đường dẫn ảnh sai

```bash
# ❌ Absolute path thường bị lỗi khi share docx
![img](/Users/name/project/image.png)

# ✅ Relative path từ vị trí chạy pandoc
![img](./images/image.png)

# ✅ Chỉ định resource path rõ ràng
pandoc input.md --resource-path=./images -o output.docx
```

---

### ❌ Lỗi: Reference-doc không được áp dụng

**Nguyên nhân:** File path sai hoặc tên style không khớp

```bash
# Kiểm tra file tồn tại
ls -la template.docx

# Dùng path tuyệt đối để chắc chắn
pandoc input.md --reference-doc=/full/path/to/template.docx -o output.docx

# Kiểm tra tên style trong reference-doc có đúng không:
# Word phải có style tên chính xác "Heading 1", "Body Text", v.v.
```

---

### ❌ Lỗi: Tiếng Việt bị lỗi encoding

```bash
# Đảm bảo file .md lưu UTF-8
file -i input.md
# Nên thấy: text/plain; charset=utf-8

# Convert nếu cần
iconv -f windows-1252 -t utf-8 input.md > input-utf8.md

# Thêm lang vào YAML frontmatter
# lang: vi
```

---

### ❌ Lỗi: Mục lục không có trong output

```bash
# Phải dùng --toc flag
pandoc input.md --toc -o output.docx

# Hoặc trong YAML frontmatter:
# toc: true
```

---

### ❌ Lỗi: Footnote không hiển thị

```bash
# Bật footnote extension
pandoc input.md --from markdown+footnotes -o output.docx
```

---

## Automation: Script Tự Động Convert

### Bash (macOS/Linux)
```bash
#!/bin/bash
# convert.sh — Tự động convert tất cả .md trong thư mục

TEMPLATE="template.docx"
OUTPUT_DIR="./output"

mkdir -p "$OUTPUT_DIR"

for mdfile in *.md; do
    docxfile="${OUTPUT_DIR}/${mdfile%.md}.docx"
    echo "Converting: $mdfile → $docxfile"
    pandoc "$mdfile" \
        --reference-doc="$TEMPLATE" \
        --toc \
        --toc-depth=3 \
        -o "$docxfile"
done

echo "Done! Files saved to $OUTPUT_DIR"
```

### PowerShell (Windows)
```powershell
# convert.ps1
$template = "template.docx"
$outputDir = ".\output"
New-Item -ItemType Directory -Force -Path $outputDir

Get-ChildItem -Filter "*.md" | ForEach-Object {
    $output = Join-Path $outputDir ($_.BaseName + ".docx")
    Write-Host "Converting: $($_.Name) → $output"
    pandoc $_.FullName --reference-doc=$template --toc -o $output
}
Write-Host "Done!"
```

### Makefile
```makefile
TEMPLATE = template.docx
SOURCES  = $(wildcard *.md)
OUTPUTS  = $(patsubst %.md,output/%.docx,$(SOURCES))

.PHONY: all clean

all: $(OUTPUTS)

output/%.docx: %.md $(TEMPLATE)
	@mkdir -p output
	pandoc $< \
		--reference-doc=$(TEMPLATE) \
		--toc \
		--toc-depth=3 \
		-o $@
	@echo "✓ $@"

clean:
	rm -rf output/
```
