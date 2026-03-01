---
name: database-design
description: >
  Expert database design skill for architecting, modeling, and optimizing relational and non-relational databases.
  ALWAYS use this skill when the user mentions: designing a database, creating a schema, writing migrations, data modeling,
  ERD diagrams, normalization, choosing between SQL and NoSQL, database performance, indexing strategy, designing tables,
  entity relationships, foreign keys, constraints, multi-tenancy, audit logging, soft delete, or any task that involves
  structuring data at the database level. Also use when the user says things like "tôi cần thiết kế DB", "giúp tôi làm schema",
  "database cho hệ thống X", "nên dùng SQL hay NoSQL", or any Vietnamese/English phrasing about organizing data storage.
  Even if the user only describes a system or feature (e.g., "tôi muốn làm app đặt đồ ăn"), proactively apply this skill
  to propose a complete database design.
---

# Database Design Skill

Skill này dạy AI thiết kế hệ thống database chuẩn chỉnh, bao gồm toàn bộ quy trình từ phân tích yêu cầu đến triển khai.

---

## 📋 Quy Trình Thiết Kế (Bắt Buộc Tuân Theo)

Luôn đi theo đúng thứ tự này:

```
 1. Thu thập yêu cầu       →  2. Xác định Entities      →  3. Vẽ ERD
 4. Chuẩn hóa (Normal Forms)→  5. Chọn kiểu DB           →  6. Định nghĩa Schema
 7. Xem xét Security        →  8. Xem xét Concurrency    →  9. Chiến lược Index
10. Check Anti-patterns      → 11. Viết Migration         → 12. Review & Tối ưu
```

---

## 🧭 Khi nào đọc Reference Files

| Tình huống | File cần đọc |
|---|---|
| Cần chi tiết về chuẩn hóa, ERD, naming convention | `references/relational-design.md` |
| Cần quyết định SQL vs NoSQL, hoặc thiết kế NoSQL | `references/nosql-design.md` |
| Cần viết SQL migration scripts, DDL chuẩn | `references/sql-patterns.md` |
| Thiết kế cho e-commerce, SaaS, social, v.v. | `references/domain-patterns.md` |
| Tối ưu hiệu năng, index, query optimization | `references/performance.md` |
| Cần xem xét bảo mật, encryption, RLS, PII | `references/security.md` |
| Cần xử lý concurrency, transactions, locking | `references/concurrency.md` |
| Muốn tránh anti-patterns (EAV, God table, v.v.) | `references/anti-patterns.md` |
| Temporal data, versioning, backup, HA, governance | `references/advanced-patterns.md` |

**Luôn đọc ít nhất 1 reference file trước khi đưa ra schema hoàn chỉnh.**
**Luôn đọc `anti-patterns.md` để double-check thiết kế không mắc lỗi phổ biến.**

---

## 🎯 Nguyên Tắc Cốt Lõi

### 1. Luôn hỏi trước khi thiết kế
Nếu thiếu thông tin, hỏi:

**Cơ bản:**
- Số lượng người dùng dự kiến? (scale)
- Read-heavy hay Write-heavy?
- Cần real-time không?
- Tech stack là gì? (PostgreSQL, MySQL, MongoDB...)

**Nâng cao (hỏi khi hệ thống phức tạp):**
- Có yêu cầu đặc biệt về audit, soft delete, multi-tenant không?
- Dữ liệu lưu bao lâu? Có archiving/retention policy không?
- Có quy định pháp lý nào áp dụng? (GDPR, HIPAA, PCI-DSS?)
- Uptime SLA bao nhiêu %? Cần high availability?
- Có tích hợp với hệ thống bên ngoài không? (APIs, ETL, CDC)
- RPO/RTO là bao nhiêu? (chấp nhận mất bao nhiêu data khi sự cố)

### 2. Output chuẩn khi thiết kế schema
Mỗi khi đưa ra thiết kế, phải bao gồm:
- **ERD dạng text** (Mermaid diagram)
- **DDL SQL** đầy đủ (CREATE TABLE với constraints)
- **Giải thích lý do** cho từng quyết định thiết kế
- **Các index** cần tạo
- **Security considerations** (dữ liệu nhạy cảm, access control)
- **Concurrency strategy** (nếu có write nhiều)
- **Các điểm cần lưu ý** / trade-offs

### 3. Không bao giờ bỏ qua
- Primary keys (luôn dùng surrogate key `id`)
- Foreign key constraints
- NOT NULL constraints khi cần
- `created_at`, `updated_at` timestamps
- Indexes trên foreign keys và các cột query thường xuyên
- Timezone-aware timestamps (`TIMESTAMPTZ` cho PostgreSQL)
- Encryption cho dữ liệu nhạy cảm (password, PII)

---

## 🗂️ Template Output Chuẩn

Khi trả lời thiết kế database, luôn dùng format sau:

```markdown
## 🗄️ Database Design: [Tên Hệ Thống]

### 📊 ERD Overview
[Mermaid ERD diagram]

### 📝 Schema Chi Tiết
[DDL SQL cho từng bảng]

### 🔍 Index Strategy
[Danh sách indexes và lý do]

### 🔒 Security Considerations
[Dữ liệu nhạy cảm, encryption, access control]

### 🔄 Concurrency & Transaction Notes
[Isolation level, locking strategy nếu cần]

### ⚠️ Lưu Ý & Trade-offs
[Những điểm cần cân nhắc, anti-patterns đã tránh]

### 🚀 Migration Script
[Script có thể chạy ngay]
```

---

## ⚡ Quick Reference: Checklist Thiết Kế

Trước khi finalize schema, check:

**Schema & Data Integrity:**
- [ ] Tất cả bảng có `id` (UUID hoặc BIGINT AUTO_INCREMENT)?
- [ ] Tất cả FK có index?
- [ ] Có `created_at` / `updated_at` trên bảng quan trọng?
- [ ] Đã đạt ít nhất 3NF?
- [ ] Đã xem xét soft delete nếu cần?
- [ ] Các cột dùng đúng data type chưa? (tránh dùng VARCHAR cho số)
- [ ] Có constraint UNIQUE ở đúng chỗ chưa?
- [ ] Tên bảng và cột đã theo naming convention chưa?
- [ ] Timestamps dùng TIMESTAMPTZ (có timezone)?

**Security:**
- [ ] Password hash bằng bcrypt/argon2 (KHÔNG MD5/SHA)?
- [ ] Dữ liệu PII đã được xác định và xử lý đúng cách?
- [ ] Có RLS hoặc filtering cho multi-tenant data?
- [ ] DB user có quyền least privilege?

**Concurrency & Performance:**
- [ ] Có version column cho bảng cần optimistic locking?
- [ ] Transaction isolation level phù hợp?
- [ ] Index strategy đã plan cho query patterns?

**Anti-patterns:**
- [ ] Không có God table (bảng quá rộng, quá đa mục đích)?
- [ ] Không dùng EAV pattern (dùng JSONB thay thế nếu cần flexible)?
- [ ] Polymorphic associations có FK hợp lệ (hoặc đã document lý do)?
- [ ] Không lưu CSV/lists trong 1 cột (dùng junction table)?
- [ ] Không dùng natural key làm PK (dùng surrogate key)?

---

## 📖 Reference Files

- 📘 `references/relational-design.md` — Chuẩn hóa, ERD, Naming Convention, Relationships
- 📗 `references/nosql-design.md` — MongoDB, Redis, Cassandra patterns
- 📙 `references/sql-patterns.md` — DDL templates, Migration patterns, Constraints
- 📕 `references/domain-patterns.md` — E-commerce, SaaS, Social, Auth, Blog schemas mẫu
- 📓 `references/performance.md` — Indexing, Partitioning, Query optimization
- 🔒 `references/security.md` — Encryption, RLS, PII, Data Masking, GDPR
- 🔄 `references/concurrency.md` — ACID, Isolation Levels, Locking, Deadlock Prevention
- ⚠️ `references/anti-patterns.md` — God Table, EAV, Polymorphic, DB-as-Queue
- 🏗️ `references/advanced-patterns.md` — Temporal Tables, SCD, Backup/DR, HA, Governance