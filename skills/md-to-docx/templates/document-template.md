---
title: "Tên Tài Liệu"
subtitle: "Phụ đề (xóa dòng này nếu không cần)"
author:
  - Tên Tác Giả
date: "2024-01-15"
lang: vi
toc: true
toc-depth: 3
number-sections: false
---

## Giới Thiệu

Paragraph đầu tiên của tài liệu. Đây sẽ dùng Word style **First Paragraph**.

Paragraph tiếp theo dùng style **Body Text**. Nội dung bình thường viết ở đây.
Dòng này vẫn thuộc cùng paragraph — chỉ có blank line mới tạo paragraph mới.

## Nội Dung Chính

### Tiêu Đề Phần Con

Nội dung của phần con.

Ví dụ về **in đậm**, *in nghiêng*, và `inline code`.

### Bảng Ví Dụ

| Cột 1        | Cột 2        | Số     |
|:-------------|:------------:|-------:|
| Căn trái     | Căn giữa     | 100    |
| Nội dung     | Nội dung     | 99.5   |
| Dòng 3       | Dòng 3       | 1,234  |

Table: **Bảng 1.** Mô tả bảng ở đây.

### Code Ví Dụ

```python
def greet(name: str) -> str:
    """Hàm chào hỏi."""
    return f"Xin chào, {name}!"

print(greet("World"))
```

### Danh Sách

Unordered list:

- Item đầu tiên
- Item thứ hai
    - Sub-item
    - Sub-item khác
- Item thứ ba

Ordered list:

1. Bước một
2. Bước hai
3. Bước ba

### Hình Ảnh

![**Hình 1.** Mô tả hình ảnh.](path/to/image.png){width=80%}

### Trích Dẫn

> Đây là một block quote.
> Dùng để trích dẫn hoặc highlight thông tin quan trọng.
>
> — *Nguồn*

### Footnote

Câu văn có chú thích cuối trang.[^1]

[^1]: Nội dung của footnote.

## Kết Luận

Tóm tắt các điểm chính...

---

*Được tạo bằng Pandoc Markdown chuẩn MD→DOCX.*
