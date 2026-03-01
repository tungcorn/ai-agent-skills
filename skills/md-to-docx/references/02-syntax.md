# 02 — Cú Pháp Markdown Chuẩn Cho DOCX

Các cú pháp dưới đây được Pandoc convert chính xác sang Word elements.
Mỗi element map sang một Word style cụ thể trong `reference-doc.docx`.

---

## Mapping: Markdown → Word Style

| Markdown element | Word Style (trong reference-doc) |
|---|---|
| `# H1` | Heading 1 |
| `## H2` | Heading 2 |
| `### H3` | Heading 3 |
| `#### H4` | Heading 4 |
| Paragraph thường | Body Text / Normal |
| Paragraph đầu tiên sau heading | First Paragraph |
| List item | List Paragraph |
| Code block | Source Code |
| Inline code | Verbatim Char (character style) |
| Block quote | Block Text |
| Bảng | Normal Table |
| Caption bảng | Table Caption |
| Caption hình | Image Caption |
| Footnote | Footnote Text |
| `**bold**` | Bold (character) |
| `*italic*` | Italic (character) |
| Link | Hyperlink (character style) |

---

## Headings

```markdown
## Chương 1: Giới Thiệu

### 1.1 Bối Cảnh

#### 1.1.1 Lịch Sử

Nội dung...
```

**Quy tắc bắt buộc:**
- Phải có **blank line** trước mỗi heading
- Không skip level: H2 → H3 → H4, **không** H2 → H4
- Dùng `##` (ATX style) thay vì underline style để pandoc xử lý chính xác
- H1 (`#`) thường không dùng trong body vì pandoc lấy title từ YAML

---

## Paragraphs

```markdown
Đây là paragraph thứ nhất.
Dòng này vẫn là cùng paragraph (pandoc ghép lại).

Blank line tạo paragraph mới — đây là paragraph thứ hai.

Kết thúc dòng bằng backslash\
tạo line break trong cùng paragraph.
```

**Lưu ý DOCX:**
- Paragraph đầu tiên ngay sau heading → Word style `First Paragraph`
- Các paragraph tiếp theo → Word style `Body Text`
- Chỉnh indent, spacing trong `reference-doc.docx` ở các styles này

---

## Tables (Pipe Table — Phổ Biến Nhất)

```markdown
| Cột 1       | Cột 2          | Số    |
|:------------|:--------------:|------:|
| Căn trái    | Căn giữa       | 100   |
| Nội dung    | Nội dung       | 99.5  |
| Dòng 3      | Dòng 3         | 1,234 |

Table: **Bảng 1.** Mô tả bảng (caption tùy chọn).
```

**Ký hiệu alignment:**
- `:---` = căn trái (mặc định)
- `:---:` = căn giữa
- `---:` = căn phải

**Lưu ý DOCX:**
- Caption `Table: ...` → Word style `Table Caption`
- Toàn bộ bảng → Word style `Normal Table`
- Để format header row khác body row → chỉnh trong `reference-doc.docx`
- **Bắt buộc có blank line** trước và sau table

**Lỗi thường gặp:**
```markdown
# ❌ Thiếu separator row
| Col 1 | Col 2 |
| Data  | Data  |

# ✅ Đúng
| Col 1 | Col 2 |
|-------|-------|
| Data  | Data  |
```

---

## Lists

### Unordered List
```markdown
- Item đầu tiên
- Item thứ hai
    - Sub-item (dùng 4 spaces để indent)
    - Sub-item khác
- Item thứ ba
```

### Ordered List
```markdown
1. Bước đầu tiên
2. Bước thứ hai
3. Bước thứ ba
```

### Tight vs Loose (ảnh hưởng spacing trong DOCX)
```markdown
# Tight list — không có blank line giữa items → ít spacing hơn
- Item 1
- Item 2
- Item 3

# Loose list — có blank line → mỗi item như paragraph, nhiều spacing hơn
- Item 1

- Item 2

- Item 3
```

**Lỗi thường gặp với list:**
```markdown
# ❌ Code block bị ghép vào list item
1. Item đầu tiên

    ```python        ← 4 spaces indent → pandoc nghĩ đây là tiếp tục của list!
    code...
    ```

# ✅ Cách đúng: kết thúc list trước
1. Item đầu tiên

<!-- blank comment để kết thúc list -->

```python
code...
```
```

---

## Code Blocks

````markdown
# ✅ Luôn có language tag
```python
def hello(name: str) -> str:
    return f"Xin chào, {name}!"
```

```sql
SELECT u.name, COUNT(o.id) as order_count
FROM users u
LEFT JOIN orders o ON o.user_id = u.id
GROUP BY u.id;
```

```bash
pandoc input.md --reference-doc=template.docx -o output.docx
```

```json
{
  "name": "project",
  "version": "1.0.0"
}
```
````

**Lưu ý DOCX:**
- Code block → Word style `Source Code`
- Inline code → Word character style `Verbatim Char`
- Không có syntax color trong DOCX mặc định (chỉ có trong HTML/PDF)
- Muốn có syntax highlighting → cần dùng `--highlight-style` + thêm filter

---

## Images

```markdown
# Inline image (không tạo figure)
![Alt text](path/to/image.png)

# Figure với caption → Word style "Image Caption"
![**Hình 1.** Mô tả hình ảnh.](path/to/image.png)

# Kiểm soát kích thước
![Caption](image.png){width=80%}
![Caption](image.png){width=10cm}
```

**Lưu ý DOCX:**
- Đường dẫn ảnh phải là **relative** từ nơi chạy pandoc
- Format hỗ trợ: PNG, JPG, SVG, EMF (EMF tốt nhất cho DOCX)
- SVG sẽ được convert tự động nếu có `librsvg`

---

## Block Quotes

```markdown
> Đây là một block quote.
> Có thể nhiều dòng.
>
> — *Tác giả*
```

→ Word style `Block Text`

---

## Footnotes

```markdown
Đây là câu có chú thích.[^1]

Inline footnote tiện hơn.^[Nội dung chú thích ngay tại đây.]

[^1]: Nội dung của footnote 1.
      Dòng tiếp theo indent 6 spaces.
```

→ Word style `Footnote Text`

---

## Bold, Italic, Strikethrough

```markdown
**in đậm**
*in nghiêng*
***đậm và nghiêng***
~~gạch ngang~~
`inline code`
```

---

## Page Break

```markdown
Nội dung trước.

---

Nội dung sau.
```

Hoặc dùng raw OpenXML (chỉ cho DOCX):

```markdown
```{=openxml}
<w:p><w:r><w:br w:type="page"/></w:r></w:p>
```
```

---

## Custom Word Style (Fenced Div)

Áp dụng Word style tùy chỉnh lên paragraph:

```markdown
::: {custom-style="My Custom Style"}
Đoạn văn này sẽ dùng Word style "My Custom Style".
:::

Văn bản [màu đặc biệt]{custom-style="Highlight Char"} trong câu.
```

Điều kiện: style phải tồn tại trong `reference-doc.docx`.

---

## Những Thứ KHÔNG Hoạt Động Trong DOCX

```markdown
# ❌ Raw HTML bị bỏ qua hoàn toàn
<div class="warning">Text</div>
<br>
<span style="color:red">Red text</span>

# ❌ CSS không áp dụng được
# ❌ JavaScript không chạy được
# ✅ Dùng custom-style thay thế
```
