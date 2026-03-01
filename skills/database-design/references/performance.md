# Database Performance — Indexing & Optimization

## Table of Contents
1. [Index Strategy](#index-strategy)
2. [Index Types](#index-types)
3. [Query Optimization](#query-optimization)
4. [Partitioning](#partitioning)
5. [Connection Pooling](#connection-pooling)
6. [Common Performance Mistakes](#mistakes)

---

## 1. Index Strategy

### Nguyên tắc cơ bản

```
✅ LUÔN index:
  - Primary keys (tự động)
  - Foreign keys (KHÔNG tự động — phải tạo thủ công!)
  - Cột dùng trong WHERE thường xuyên với high cardinality
  - Cột dùng trong ORDER BY, GROUP BY
  - Cột dùng trong JOIN conditions

⚠️ CÂN NHẮC khi:
  - Bảng nhỏ (< 1000 rows) — full scan có thể nhanh hơn
  - Cột có cardinality thấp (boolean, status) — thường không hiệu quả
  - Write-heavy tables — index làm chậm INSERT/UPDATE/DELETE

❌ KHÔNG nên:
  - Index quá nhiều cột trên cùng 1 bảng (> 10 indexes)
  - Index cột thay đổi rất thường xuyên
  - Duplicate indexes
  - Index cột với nhiều NULL và query không lọc NULL
```

### Foreign Key Index — Lỗi thường gặp nhất
```sql
-- Khi tạo FK, PostgreSQL/MySQL KHÔNG tự tạo index!
-- Phải tạo thủ công:

CREATE TABLE orders (
  id BIGSERIAL PRIMARY KEY,
  user_id BIGINT NOT NULL REFERENCES users(id),  -- FK nhưng không có index!
  ...
);

-- ✅ Tạo index cho FK
CREATE INDEX idx_orders_user_id ON orders(user_id);

-- Tại sao quan trọng: Khi xóa 1 user, DB phải check xem có orders nào không.
-- Nếu không có index trên orders.user_id → full table scan → cực chậm với bảng lớn.
```

### Quy tắc Index Selectivity
```sql
-- High cardinality (nhiều unique values) = index hiệu quả
email VARCHAR(254)       -- Gần như unique → RẤT hiệu quả
user_id BIGINT           -- Nhiều unique values → Hiệu quả
created_at TIMESTAMPTZ   -- Nhiều unique values → Hiệu quả với range queries

-- Low cardinality = index kém hiệu quả
is_active BOOLEAN        -- Chỉ 2 giá trị → Không nên index đơn lẻ
gender VARCHAR(10)       -- Ít giá trị → Không hiệu quả
status VARCHAR(50)       -- Ít giá trị, phân bố lệch → Cân nhắc partial index
```

---

## 2. Index Types

### B-Tree Index (Default) — Dùng cho hầu hết
```sql
-- Standard index
CREATE INDEX idx_users_email ON users(email);

-- Composite index (thứ tự quan trọng!)
-- Rule: Đặt cột equality filter trước, range filter sau
-- Query: WHERE status = 'active' AND created_at > '2024-01-01'
CREATE INDEX idx_orders_status_created ON orders(status, created_at DESC);
-- ✅ status đứng trước (equality), created_at sau (range/sort)

-- Composite index: leading column rule
-- Index (A, B, C) có thể dùng cho:
--   WHERE A = ...
--   WHERE A = ... AND B = ...
--   WHERE A = ... AND B = ... AND C = ...
--   ORDER BY A, B, C
-- KHÔNG dùng được cho:
--   WHERE B = ...  (không có A)
--   WHERE C = ...  (không có A, B)
```

### Partial Index — Cực kỳ hiệu quả
```sql
-- Chỉ index active records (bỏ qua deleted)
CREATE INDEX idx_users_email_active ON users(email)
  WHERE deleted_at IS NULL;

-- Chỉ index unread notifications
CREATE INDEX idx_notifications_unread ON notifications(user_id, created_at DESC)
  WHERE is_read = FALSE;

-- Chỉ index pending orders
CREATE INDEX idx_orders_pending ON orders(created_at)
  WHERE status = 'pending';

-- Lợi ích: Index nhỏ hơn nhiều, nhanh hơn, ít tốn RAM
```

### Covering Index (Include columns)
```sql
-- Nếu query chỉ cần data từ các cột trong index → không cần đọc table
-- PostgreSQL: INCLUDE clause
CREATE INDEX idx_orders_user_covering ON orders(user_id)
  INCLUDE (status, total_amount, created_at);

-- Query này sẽ dùng index-only scan (không cần access heap):
SELECT status, total_amount, created_at FROM orders WHERE user_id = 123;
```

### GIN Index — Cho JSON, Arrays, Full-text (PostgreSQL)
```sql
-- Index JSONB field
CREATE INDEX idx_products_attributes ON products USING GIN(attributes);
-- Query: WHERE attributes @> '{"color": "red"}'

-- Full-text search index
CREATE INDEX idx_posts_search ON posts USING GIN(to_tsvector('english', title || ' ' || content));
-- Query: WHERE to_tsvector('english', title || ' ' || content) @@ to_tsquery('database')
```

### Hash Index (PostgreSQL) — Chỉ cho equality
```sql
-- Chỉ hỗ trợ = operator, nhanh hơn B-Tree cho equality
CREATE INDEX idx_users_token ON user_sessions USING HASH(token_hash);
-- Dùng khi: chỉ query = , không cần range, ORDER BY
```

---

## 3. Query Optimization

### EXPLAIN ANALYZE — Công cụ thiết yếu
```sql
-- PostgreSQL
EXPLAIN ANALYZE SELECT * FROM orders 
WHERE user_id = 123 AND status = 'pending'
ORDER BY created_at DESC;

-- Đọc output: Tìm kiếm những điều này:
-- Seq Scan → Full table scan, cần index
-- Index Scan → Dùng index, tốt
-- Index Only Scan → Dùng covering index, tốt nhất
-- Nested Loop → OK cho small datasets
-- Hash Join → OK cho larger datasets
-- Merge Join → OK
-- Rows=XXXX → Số rows estimate, nếu sai xa → chạy ANALYZE table_name
```

### Tránh N+1 Query
```sql
-- ❌ N+1: 1 query lấy orders, rồi N queries lấy user
SELECT * FROM orders LIMIT 100;
-- Rồi với mỗi order: SELECT * FROM users WHERE id = ?

-- ✅ JOIN một lần
SELECT o.*, u.display_name, u.email
FROM orders o
JOIN users u ON u.id = o.user_id
ORDER BY o.created_at DESC
LIMIT 100;
```

### Pagination — Offset vs Cursor
```sql
-- ❌ OFFSET pagination — chậm với large offsets
SELECT * FROM posts ORDER BY created_at DESC LIMIT 20 OFFSET 10000;
-- Vấn đề: DB phải scan 10020 rows, bỏ 10000, trả 20

-- ✅ Cursor-based pagination — hiệu quả hơn
SELECT * FROM posts 
WHERE created_at < '2024-01-15 10:30:00'  -- cursor từ last item
ORDER BY created_at DESC 
LIMIT 20;
-- Luôn nhanh vì dùng index scan

-- ✅ Keyset pagination với composite key
SELECT * FROM posts
WHERE (created_at, id) < ('2024-01-15 10:30:00', 12345)
ORDER BY created_at DESC, id DESC
LIMIT 20;
```

### Tránh SELECT *
```sql
-- ❌ SELECT * — lấy dữ liệu không cần thiết, không dùng covering index
SELECT * FROM users WHERE id = 123;

-- ✅ Chỉ lấy cột cần thiết
SELECT id, email, display_name, avatar_url FROM users WHERE id = 123;
```

### Sử dụng Connection Pool
```
Không bao giờ tạo DB connection trực tiếp trong production app
Luôn dùng connection pool: PgBouncer (PostgreSQL), ProxySQL (MySQL)

Pool settings cho PostgreSQL:
- max_connections trong postgresql.conf: 100-200 (tùy RAM)
- PgBouncer pool_size: 10-20 per app instance
- Pool mode: transaction (tốt nhất cho REST APIs)
```

---

## 4. Partitioning

### Khi nào cần Partitioning?
- Bảng > 100 triệu rows
- Cần DROP data cũ nhanh (partition pruning)
- Queries thường filter theo một cột (date, region)

### Range Partitioning (theo thời gian — phổ biến nhất)
```sql
-- PostgreSQL declarative partitioning
CREATE TABLE events (
  id BIGSERIAL,
  user_id BIGINT NOT NULL,
  event_type VARCHAR(100) NOT NULL,
  data JSONB,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
) PARTITION BY RANGE (created_at);

-- Tạo partitions
CREATE TABLE events_2024_q1 PARTITION OF events
  FOR VALUES FROM ('2024-01-01') TO ('2024-04-01');

CREATE TABLE events_2024_q2 PARTITION OF events
  FOR VALUES FROM ('2024-04-01') TO ('2024-07-01');

-- Drop old data instantly (instead of DELETE which is slow)
DROP TABLE events_2023_q1;  -- Instant! No vacuum needed

-- Automating partition creation với pg_partman extension
```

### List Partitioning (theo region, status)
```sql
CREATE TABLE orders (
  id BIGSERIAL,
  region VARCHAR(20) NOT NULL,
  ...
) PARTITION BY LIST (region);

CREATE TABLE orders_vn PARTITION OF orders FOR VALUES IN ('VN');
CREATE TABLE orders_us PARTITION OF orders FOR VALUES IN ('US');
CREATE TABLE orders_eu PARTITION OF orders FOR VALUES IN ('EU', 'UK', 'DE', 'FR');
```

---

## 5. Connection Pooling

### PgBouncer Config (PostgreSQL)
```ini
[pgbouncer]
listen_port = 6432
listen_addr = *
auth_type = md5
pool_mode = transaction  ; transaction / session / statement
max_client_conn = 1000
default_pool_size = 20
min_pool_size = 5
reserve_pool_size = 5
server_idle_timeout = 600
```

### Application-level Pooling
```python
# Python SQLAlchemy
engine = create_engine(
    DATABASE_URL,
    pool_size=10,          # Core pool connections
    max_overflow=20,       # Extra connections khi cần
    pool_pre_ping=True,    # Test connection trước khi dùng
    pool_recycle=3600      # Recycle connections sau 1h
)

# Node.js pg
const pool = new Pool({
    max: 20,
    idleTimeoutMillis: 30000,
    connectionTimeoutMillis: 2000,
})
```

---

## 6. Common Performance Mistakes

### 1. Implicit Type Conversion
```sql
-- ❌ user_id là BIGINT nhưng truyền STRING → không dùng index
WHERE user_id = '123'

-- ✅
WHERE user_id = 123
```

### 2. Function trên cột index
```sql
-- ❌ Function wrap cột → không dùng index
WHERE DATE(created_at) = '2024-01-15'
WHERE LOWER(email) = 'user@example.com'
WHERE YEAR(created_at) = 2024

-- ✅ Viết lại để index được dùng
WHERE created_at >= '2024-01-15 00:00:00' AND created_at < '2024-01-16 00:00:00'
WHERE email = LOWER('user@example.com')  -- Hoặc lưu email lowercase từ đầu
WHERE created_at >= '2024-01-01' AND created_at < '2025-01-01'
```

### 3. OR condition phá index
```sql
-- ❌ OR có thể không dùng index hiệu quả
WHERE status = 'pending' OR status = 'processing'

-- ✅ Dùng IN
WHERE status IN ('pending', 'processing')
```

### 4. Wildcard đầu chuỗi
```sql
-- ❌ LIKE '%keyword' không dùng B-Tree index
WHERE name LIKE '%shirt'

-- ✅ LIKE 'shirt%' dùng được B-Tree index
WHERE name LIKE 'shirt%'

-- ✅ Full-text search cho tìm kiếm trong chuỗi
WHERE to_tsvector('english', name) @@ to_tsquery('shirt')
```

### 5. Không analyze sau bulk insert
```sql
-- Sau khi import nhiều data:
ANALYZE table_name;
-- Hoặc
VACUUM ANALYZE table_name;

-- PostgreSQL planner dùng statistics để chọn execution plan
-- Statistics cũ → plan sai → query chậm
```

### 6. Counter Columns thay vì COUNT queries
```sql
-- ❌ COUNT(*) trên bảng lớn mỗi lần
SELECT COUNT(*) FROM posts WHERE author_id = 123;

-- ✅ Denormalized counter (cập nhật khi có write)
-- Trong bảng users:
post_count INT NOT NULL DEFAULT 0

-- Trigger hoặc application code:
UPDATE users SET post_count = post_count + 1 WHERE id = 123;

-- Trade-off: Có thể lệch nhau → cần periodic reconciliation job
```