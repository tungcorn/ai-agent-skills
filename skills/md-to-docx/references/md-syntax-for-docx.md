# Cú Pháp Markdown Chuẩn Cho DOCX Output

## 1. Headings

```markdown
# Heading 1   ← Thường lấy từ YAML title, ít dùng trực tiếp
## Heading 2  ← Section chính (Heading 1 trong Word)
### Heading 3 ← Subsection (Heading 2 trong Word)
#### Heading 4
```

**Quy tắc bắt buộc:**

- PHẢI có blank line trước mỗi heading
- KHÔNG được skip level (H2 → H4 là sai)
- Heading 1 trong markdown = "Heading 1" trong Word, heading 2 = "Heading 2", v.v.

```markdown
Đây là paragraph.

## Heading Mới        ← blank line bắt buộc!

Nội dung tiếp theo.
```

---

## 2. Paragraphs

```markdown
Paragraph thứ nhất. Các dòng liên tiếp
vẫn là cùng một paragraph trong DOCX.

Blank line tạo paragraph mới.

Dùng backslash để xuống dòng cứng:\
Dòng này trong cùng paragraph nhưng xuống dòng.
```

**Tránh dùng 2 spaces để xuống dòng** — vô hình, dễ nhầm, dùng `\` thay thế.

---

## 3. Lists

### Unordered List

```markdown
- Item thứ nhất
- Item thứ hai
    - Sub-item (4 spaces indent)
    - Sub-item khác
- Item thứ ba
```

### Ordered List

```markdown
1. Bước một
2. Bước hai
3. Bước ba

<!-- Pandoc tự đánh số, có thể viết tất cả là 1. -->
1. Bước một
1. Bước hai
1. Bước ba
```

### Lưu ý quan trọng về List trong DOCX

```markdown
<!-- Tight list (không có blank line) — spacing nhỏ, compact -->
- Item A
- Item B
- Item C

<!-- Loose list (có blank line) — mỗi item như một paragraph, spacing lớn -->
- Item A

- Item B

- Item C
```

Pandoc convert loose list → "Body Text" style thay vì "Compact".
Hầu hết tài liệu hành chính nên dùng **tight list**.

---

## 4. Tables (Pipe Table)

```markdown
| Tiêu đề 1 | Tiêu đề 2  | Tiêu đề 3 |
|:----------|:----------:|----------:|
| Trái      | Giữa       | Phải      |
| Nội dung  | Nội dung   | 100       |
| Dài hơn   | Trung bình | 99.99     |

Table: **Bảng 1.** Mô tả bảng ở đây.
```

**Alignment:**

- `:---` = căn trái (mặc định cho text)
- `:---:` = căn giữa
- `---:` = căn phải (dùng cho số)

**Caption:** Dòng `Table: ...` ngay sau bảng → Word style "Table Caption".

**Lưu ý:**

- Phải có blank line trước và sau bảng
- Header row bắt buộc (không thể có bảng không có header trong pandoc)
- Nội dung ô không nên quá dài — sẽ bị wrap trong DOCX

---

## 5. Code Blocks

````markdown
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
pandoc input.md --reference-doc=ref.docx -o output.docx
```
````

**Inline code:**

```markdown
Chạy lệnh `pandoc --version` để kiểm tra.
```

**Word output:** Code block → style "Source Code" (tạo động bởi pandoc), dùng font monospace.

---

## 6. Bold, Italic, Strikethrough

```markdown
**In đậm** hoặc __in đậm__
*In nghiêng* hoặc _in nghiêng_
***Đậm và nghiêng***
~~Gạch ngang~~
```

---

## 7. Blockquote

```markdown
> Đây là một blockquote.
> Dòng này cùng blockquote.
>
> Paragraph thứ hai trong blockquote.
>
> — *Nguồn trích dẫn*
```

Word style: "Block Text".

---

## 8. Images & Figures

```markdown
<!-- Inline image đơn giản -->
![Alt text](images/diagram.png)

<!-- Image với caption → tạo Figure trong DOCX -->
![**Hình 1.** Sơ đồ kiến trúc hệ thống.](images/diagram.png)

<!-- Kiểm soát kích thước -->
![Caption](images/chart.png){width=80%}
![Caption](images/logo.png){width=3cm}
```

**Lưu ý:**

- Dùng đường dẫn **tương đối** từ vị trí file .md
- Pandoc hỗ trợ: PNG, JPG, SVG, TIFF
- Image có caption → Word style "Caption" / "Image Caption"
- Image không có caption → inline image

---

## 9. Footnotes

```markdown
Đây là câu có chú thích.[^note1]

Đây là chú thích inline.^[Nội dung ngay tại đây — tiện hơn.]

[^note1]: Nội dung của footnote.
    Có thể nhiều dòng (indent 4 spaces).
```

Word output: Footnote được render đúng chuẩn Word footnote.

---

## 10. Links

```markdown
[Text hiển thị](https://example.com)
<https://example.com>    ← Auto-link

<!-- Link đến section trong cùng document -->
Xem [phần kết luận](#kết-luận).

## Kết Luận     ← anchor tự động: #kết-luận
```

---

## 11. Horizontal Rule (Page Break)

Để tạo page break trong DOCX từ markdown, dùng raw OOXML:

```markdown
Nội dung trang 1.

```{=openxml}
<w:p><w:r><w:br w:type="page"/></w:r></w:p>
```

Nội dung trang 2.
```

Hoặc dùng `---` nhưng đây chỉ tạo horizontal line, không phải page break.

---

## 12. Definition Lists (Pandoc Extension)

```markdown
Thuật ngữ 1
:   Định nghĩa của thuật ngữ 1.

Thuật ngữ 2
:   Định nghĩa đầu tiên.
:   Định nghĩa thứ hai.
```

Word output: "Definition Term" và "Definition" styles.

---

## 13. Những Thứ KHÔNG Hoạt Động Với DOCX

```markdown
<!-- RAW HTML bị bỏ qua hoàn toàn -->
<div class="highlight">text</div>    ← KHÔNG hiển thị trong DOCX
<br>                                 ← KHÔNG hoạt động, dùng \ thay thế
<span style="color:red">text</span>  ← KHÔNG hoạt động

<!-- Math cần engine riêng -->
$E = mc^2$    ← Cần --mathml hoặc plugin; không tự nhiên như PDF

<!-- Emoji -->
:smile:       ← Không render, phải paste emoji trực tiếp: 😊
```

