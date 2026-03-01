# Database Anti-Patterns & Common Mistakes

## Table of Contents
1. [God Table (Blob Table)](#god-table)
2. [EAV (Entity-Attribute-Value)](#eav)
3. [Polymorphic Associations](#polymorphic)
4. [Database as Queue](#db-queue)
5. [Soft Delete Pitfalls](#soft-delete-pitfalls)
6. [Over-normalization](#over-normalization)
7. [Natural Key as PK](#natural-key)
8. [Storing Calculated Values Without Sync](#calculated)
9. [CSV/JSON Strings Instead of Relationships](#csv)
10. [Files/Blobs in Database](#blobs)
11. [Missing Timezone Awareness](#timezone)
12. [Other Common Mistakes](#other)

---

## 1. God Table (Blob Table)

### Anti-pattern
```sql
-- ❌ Một bảng cố chứa TOÀN BỘ data, MỌI thứ
CREATE TABLE entities (
  id BIGSERIAL PRIMARY KEY,
  type VARCHAR(50),            -- 'user', 'order', 'product', 'comment', 'notification'
  name VARCHAR(500),
  email VARCHAR(254),          -- Chỉ dùng cho users
  price DECIMAL(19,4),         -- Chỉ dùng cho products
  total_amount DECIMAL(19,4),  -- Chỉ dùng cho orders
  content TEXT,                -- Chỉ dùng cho comments
  parent_id BIGINT,            -- Quan hệ unclear
  status VARCHAR(50),
  -- 80-90% columns sẽ NULL trên hầu hết rows
  field1 TEXT, field2 TEXT, field3 TEXT, field4 TEXT,
  field5 TEXT, field6 TEXT, field7 TEXT, field8 TEXT,
  -- Columns cứ thêm dần theo thời gian...
  metadata JSONB,
  created_at TIMESTAMPTZ
);
```

### Tại sao sai — Phân tích chi tiết
```
1. CONSTRAINTS VÔ DỤNG
   - Không thể set NOT NULL cho email (vì products không có email)
   - Không thể CHECK price >= 0 (vì users không có price)
   - Business rules phải nằm ở application → dễ bypass

2. INDEXES KÉM HIỆU QUẢ
   - Index trên email chứa 90% NULL (products, orders, comments)
   - Storage lãng phí, query planner confused

3. KHÔNG SELF-DOCUMENTING
   - Developer mới nhìn vào không hiểu bảng này chứa gì
   - "field1", "field7" → ý nghĩa gì?

4. PERFORMANCE
   - Row size lớn (nhiều cột) → ít rows per page → nhiều I/O
   - Full table scan chậm hơn
   - Index bloat

5. MIGRATION NIGHTMARE
   - Thêm feature mới → thêm 5 cột vào god table
   - ALTER TABLE trên bảng triệu rows → lock table lâu

6. TESTING KHÓ
   - Test data phức tạp: phải seed với đúng "type" + đúng columns
   - Constraint at application level → easy to miss edge cases
```

### ✅ Giải pháp: Tách thành bảng riêng theo Entity
```sql
-- Mỗi entity một bảng → rõ ràng, type-safe, constrainable
CREATE TABLE users (
  id BIGSERIAL PRIMARY KEY,
  email VARCHAR(254) UNIQUE NOT NULL,  -- ✅ NOT NULL enforced
  name VARCHAR(255) NOT NULL,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE products (
  id BIGSERIAL PRIMARY KEY,
  name VARCHAR(500) NOT NULL,
  price DECIMAL(19,4) NOT NULL CHECK (price >= 0),  -- ✅ CHECK enforced
  stock_quantity INT NOT NULL DEFAULT 0 CHECK (stock_quantity >= 0),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE orders (
  id BIGSERIAL PRIMARY KEY,
  user_id BIGINT NOT NULL REFERENCES users(id),  -- ✅ FK enforced
  total_amount DECIMAL(19,4) NOT NULL CHECK (total_amount >= 0),
  status VARCHAR(50) NOT NULL DEFAULT 'pending'
    CHECK (status IN ('pending', 'confirmed', 'shipped', 'delivered', 'cancelled')),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_orders_user ON orders(user_id);
CREATE INDEX idx_orders_status ON orders(status);

-- Mỗi bảng:
-- ✅ Có constraints phù hợp
-- ✅ Có indexes tối ưu
-- ✅ Self-documenting
-- ✅ Dễ migrate
```

### Khi nào "wide table" chấp nhận được?
```
✅ Data warehouse / OLAP tables (denormalized star schema)
✅ Analytics fact tables (rất nhiều dimensions → flat table)
✅ Audit log tables (JSONB cho varied event types)

❌ KHÔNG chấp nhận cho OLTP / transactional data
```

---

## 2. EAV (Entity-Attribute-Value)

### Anti-pattern
```sql
-- ❌ Mọi thuộc tính biến thành key-value pairs
CREATE TABLE entity_attributes (
  entity_id BIGINT NOT NULL,
  attribute_name VARCHAR(255) NOT NULL,  -- 'color', 'size', 'weight', 'price'
  attribute_value TEXT,                   -- EVERYTHING stored as TEXT!
  PRIMARY KEY (entity_id, attribute_name)
);

-- Data trông như thế này:
-- | entity_id | attribute_name | attribute_value |
-- |-----------|----------------|-----------------|
-- | 1         | name           | iPhone 15       |
-- | 1         | price          | 999.99          |  ← TEXT, không phải DECIMAL!
-- | 1         | color          | Blue            |
-- | 1         | weight         | 171             |  ← TEXT, không phải INT!
-- | 1         | released       | 2023-09-22      |  ← TEXT, không phải DATE!
-- | 1         | colour         | Blue            |  ← Typo = attribute mới!
```

### Tại sao sai — Phân tích chi tiết
```
1. KHÔNG CÓ DATA TYPE VALIDATION
   - price = 'abc' → database chấp nhận! (vì TEXT)
   - weight = -100 → database chấp nhận!
   - Validation chỉ ở application → bypass dễ dàng

2. QUERY CỰC KỲ PHỨC TẠP
   - Lấy 1 product → cần pivot 10+ rows thành 1 object
   - WHERE price > 100 → phải cast TEXT to DECIMAL trong query
   - JOIN giữa entities → nightmare

3. PERFORMANCE TỆ
   - 1 product = 10 rows (thay vì 1 row)
   - Lấy 100 products = 1000 rows + pivot
   - Index trên attribute_value vô dụng (mixed types)

4. TYPOS TẠO ATTRIBUTE MỚI
   - 'color' vs 'colour' vs 'Color' → 3 attributes khác nhau!
   - Không có schema enforce → data inconsistency

5. KHÔNG CÓ REFERENTIAL INTEGRITY
   - Không thể FK từ attribute_value
   - Không thể UNIQUE trên business logic combinations

6. REPORTING NIGHTMARE
   - Pivot query rất phức tạp
   - BI tools không hiểu EAV schema
   - Report chậm trên bảng lớn
```

```sql
-- ❌ Query EAV: muốn lấy products có color = 'Blue' và price > 500
SELECT e.entity_id
FROM entity_attributes e
WHERE e.attribute_name = 'color' AND e.attribute_value = 'Blue'
INTERSECT
SELECT e.entity_id
FROM entity_attributes e
WHERE e.attribute_name = 'price' AND CAST(e.attribute_value AS DECIMAL) > 500;
-- 😱 So sánh với: SELECT * FROM products WHERE color = 'Blue' AND price > 500;

-- ❌ Query EAV: muốn lấy product name + price + color (pivot)
SELECT
  p_name.attribute_value AS name,
  p_price.attribute_value AS price,
  p_color.attribute_value AS color,
  p_weight.attribute_value AS weight,
  p_status.attribute_value AS status
FROM entity_attributes p_name
LEFT JOIN entity_attributes p_price
  ON p_name.entity_id = p_price.entity_id AND p_price.attribute_name = 'price'
LEFT JOIN entity_attributes p_color
  ON p_name.entity_id = p_color.entity_id AND p_color.attribute_name = 'color'
LEFT JOIN entity_attributes p_weight
  ON p_name.entity_id = p_weight.entity_id AND p_weight.attribute_name = 'weight'
LEFT JOIN entity_attributes p_status
  ON p_name.entity_id = p_status.entity_id AND p_status.attribute_name = 'status'
WHERE p_name.attribute_name = 'name';
-- 5 cột = 4 JOINs! 20 cột = 19 JOINs! → 💀💀💀
```

### ✅ Giải pháp 1: Dedicated Columns (khi biết trước attributes)
```sql
-- Biết trước product có: name, price, color, weight, status
CREATE TABLE products (
  id BIGSERIAL PRIMARY KEY,
  name VARCHAR(500) NOT NULL,
  price DECIMAL(19,4) NOT NULL CHECK (price >= 0),
  color VARCHAR(50),
  weight_grams INT CHECK (weight_grams > 0),
  status VARCHAR(50) NOT NULL DEFAULT 'active'
    CHECK (status IN ('active', 'inactive', 'archived')),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ✅ Simple query: SELECT * FROM products WHERE color = 'Blue' AND price > 500;
-- ✅ Type-safe: price là DECIMAL, weight là INT
-- ✅ Constraints: CHECK, NOT NULL, FK đều hoạt động
```

### ✅ Giải pháp 2: JSONB Column (khi attributes linh hoạt)
```sql
-- ========================================
-- Khi: Products có attributes khác nhau tùy category
-- Áo: size, color, material
-- Điện thoại: screen_size, RAM, storage
-- → Cột cố định cho common attrs + JSONB cho dynamic attrs
-- ========================================

CREATE TABLE products (
  id BIGSERIAL PRIMARY KEY,
  -- Cột cố định cho COMMON attributes (mọi product đều có)
  name VARCHAR(500) NOT NULL,
  price DECIMAL(19,4) NOT NULL CHECK (price >= 0),
  category_id INT REFERENCES categories(id),
  status VARCHAR(50) NOT NULL DEFAULT 'active',
  
  -- JSONB cho DYNAMIC attributes (khác nhau theo category)
  attributes JSONB NOT NULL DEFAULT '{}',
  -- Ví dụ:
  -- Áo:        {"size": "L", "color": "Blue", "material": "Cotton"}
  -- Điện thoại: {"screen_size": 6.1, "ram_gb": 8, "storage_gb": 256}
  -- Laptop:    {"screen_size": 15.6, "cpu": "i7-13700H", "ram_gb": 32}
  
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- GIN index cho JSONB queries
CREATE INDEX idx_products_attributes ON products USING GIN(attributes);

-- ✅ Query JSONB: Đơn giản hơn EAV rất nhiều
SELECT * FROM products
WHERE attributes @> '{"color": "Blue"}'         -- Contains key-value
  AND (attributes->>'ram_gb')::INT >= 8;         -- Compare value

-- ✅ Query JSONB: Filter + sort
SELECT * FROM products
WHERE category_id = 5
  AND attributes @> '{"material": "Cotton"}'
ORDER BY price;

-- ✅ JSONB giữ types tốt hơn EAV (number, string, boolean, null, array)
-- {"price_original": 999.99, "on_sale": true, "tags": ["new", "featured"]}
```

### ✅ Giải pháp 3: JSONB có Validation
```sql
-- Validate JSONB structure bằng CHECK constraint
ALTER TABLE products ADD CONSTRAINT valid_attributes CHECK (
  -- Nếu category = clothing, phải có size và color
  (category_id != 1 OR (
    attributes ? 'size'
    AND attributes ? 'color'
  ))
  -- Price attribute nếu có, phải > 0
  AND (
    NOT attributes ? 'weight_kg'
    OR (attributes->>'weight_kg')::DECIMAL > 0
  )
);

-- PostgreSQL 12+: Dùng IS JSON cho validation cơ bản
ALTER TABLE products ADD CONSTRAINT valid_json CHECK (
  attributes IS NOT NULL AND jsonb_typeof(attributes) = 'object'
);
```

### Khi nào EAV chấp nhận được?
```
✅ EAV OK khi:
  - Prototype / MVP nhanh (sẽ refactor sau)
  - Extensible fields user tự define (custom form builder)
  - Attribute count THẬT SỰ unbounded (hàng nghìn attributes)

❌ EAV KHÔNG OK khi:
  - Bạn biết trước attributes (dùng columns!)
  - Cần query/filter/sort theo attributes
  - Cần referential integrity
  - Performance matters

🎯 Hầu hết trường hợp → JSONB column là giải pháp tốt hơn EAV
```

---

## 3. Polymorphic Associations

### Anti-pattern
```sql
-- ❌ Một FK tham chiếu NHIỀU bảng khác nhau — phân biệt bằng type column
CREATE TABLE comments (
  id BIGSERIAL PRIMARY KEY,
  -- "commentable" có thể là post, product, hoặc video
  commentable_type VARCHAR(50) NOT NULL,  -- 'post', 'product', 'video'
  commentable_id BIGINT NOT NULL,         -- FK đến... bảng nào?
  content TEXT NOT NULL,
  author_id BIGINT REFERENCES users(id),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ⚠️ KHÔNG THỂ tạo FOREIGN KEY!
-- FOREIGN KEY (commentable_id) REFERENCES ???
-- commentable_id = 42 có thể là:
--   post #42, product #42, hoặc video #42 → DB KHÔNG BIẾT!
```

### Tại sao có vấn đề — Phân tích chi tiết
```
1. KHÔNG CÓ REFERENTIAL INTEGRITY
   - Xóa post #42 → comment vẫn reference commentable_id = 42
   - → Orphaned records, data không consistent
   - Application PHẢI tự enforce → dễ miss edge cases

2. QUERY PHỨC TẠP
   - "Lấy tất cả comments kèm target info":
   SELECT c.*, 
     CASE c.commentable_type
       WHEN 'post' THEN p.title
       WHEN 'product' THEN pr.name
       WHEN 'video' THEN v.title
     END AS target_name
   FROM comments c
   LEFT JOIN posts p ON c.commentable_type = 'post' AND c.commentable_id = p.id
   LEFT JOIN products pr ON c.commentable_type = 'product' AND c.commentable_id = pr.id
   LEFT JOIN videos v ON c.commentable_type = 'video' AND c.commentable_id = v.id;
   -- Thêm 1 type mới → thêm 1 LEFT JOIN + CASE WHEN!

3. INDEX KÉM HIỆU QUẢ
   - Composite index (commentable_type, commentable_id):
     Index chứa MỌI types, dù query chỉ cần 1 type
   - Partial index tốt hơn nhưng phải tạo cho MỖI type

4. MIGRATION RISK
   - Rename bảng (posts → articles) → phải update commentable_type data!
   - Dễ quên, dễ sai
```

### ✅ Giải pháp 1: Separate FK Columns (Khi ít types, <5)
```sql
CREATE TABLE comments (
  id BIGSERIAL PRIMARY KEY,
  -- FK riêng cho mỗi loại — database enforce integrity!
  post_id BIGINT REFERENCES posts(id) ON DELETE CASCADE,
  product_id BIGINT REFERENCES products(id) ON DELETE CASCADE,
  video_id BIGINT REFERENCES videos(id) ON DELETE CASCADE,
  
  content TEXT NOT NULL,
  author_id BIGINT REFERENCES users(id) ON DELETE SET NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  
  -- ✅ Exactly ONE FK must be set
  CHECK (
    (post_id IS NOT NULL)::INT +
    (product_id IS NOT NULL)::INT +
    (video_id IS NOT NULL)::INT = 1
  )
);

-- Indexes cho mỗi FK
CREATE INDEX idx_comments_post ON comments(post_id) WHERE post_id IS NOT NULL;
CREATE INDEX idx_comments_product ON comments(product_id) WHERE product_id IS NOT NULL;
CREATE INDEX idx_comments_video ON comments(video_id) WHERE video_id IS NOT NULL;

-- ✅ Query: Comments cho 1 post
SELECT * FROM comments WHERE post_id = 42;
-- Simple! Dùng partial index → nhanh!
```

### ✅ Giải pháp 2: Separate Tables (Khi mỗi type có schema khác)
```sql
-- Mỗi type có bảng comments riêng → mỗi bảng có schema phù hợp
CREATE TABLE post_comments (
  id BIGSERIAL PRIMARY KEY,
  post_id BIGINT NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
  author_id BIGINT REFERENCES users(id) ON DELETE SET NULL,
  parent_id BIGINT REFERENCES post_comments(id) ON DELETE CASCADE,  -- Nested!
  content TEXT NOT NULL,
  is_approved BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE product_reviews (
  id BIGSERIAL PRIMARY KEY,
  product_id BIGINT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  author_id BIGINT REFERENCES users(id) ON DELETE SET NULL,
  -- Reviews có rating → schema khác comments!
  rating INT NOT NULL CHECK (rating BETWEEN 1 AND 5),
  title VARCHAR(255),
  content TEXT NOT NULL,
  is_verified_purchase BOOLEAN NOT NULL DEFAULT FALSE,
  helpful_count INT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ✅ Mỗi bảng:
--   - Có FK enforced
--   - Có schema phù hợp (reviews có rating, comments có nesting)
--   - Indexes tối ưu cho từng use case
```

### ✅ Giải pháp 3: Common Base Table (Shared Supertype)
```sql
-- ========================================
-- Khi cần polymorphic nhưng VẪN muốn FK enforcement
-- Pattern: "Class Table Inheritance" / "Shared Primary Key"
-- ========================================

-- Bảng base chung — mọi commentable entity tham chiếu
CREATE TABLE commentable_entities (
  id BIGSERIAL PRIMARY KEY,
  entity_type VARCHAR(50) NOT NULL  -- discriminator column
    CHECK (entity_type IN ('post', 'product', 'video')),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Bảng con: ID = FK đến base table
CREATE TABLE posts (
  id BIGINT PRIMARY KEY REFERENCES commentable_entities(id) ON DELETE CASCADE,
  -- ID PHẢI trùng với commentable_entities.id
  title VARCHAR(500) NOT NULL,
  content TEXT,
  author_id BIGINT REFERENCES users(id)
);

CREATE TABLE products (
  id BIGINT PRIMARY KEY REFERENCES commentable_entities(id) ON DELETE CASCADE,
  name VARCHAR(500) NOT NULL,
  price DECIMAL(19,4) NOT NULL
);

-- Comments: FK đến base table → VALID FK!
CREATE TABLE comments (
  id BIGSERIAL PRIMARY KEY,
  commentable_id BIGINT NOT NULL REFERENCES commentable_entities(id) ON DELETE CASCADE,
  -- ✅ FK enforced! Xóa entity → cascade xóa comments
  content TEXT NOT NULL,
  author_id BIGINT REFERENCES users(id),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ✅ Query: lấy comments kèm entity type
SELECT c.*, ce.entity_type
FROM comments c
JOIN commentable_entities ce ON ce.id = c.commentable_id;

-- ⚠️ Nhược điểm: Insert cần 2 bảng
-- INSERT INTO commentable_entities (entity_type) VALUES ('post') RETURNING id;
-- INSERT INTO posts (id, title) VALUES (returned_id, 'My Post');
```

### Khi nào Polymorphic (type + id) chấp nhận được?
```
✅ Chấp nhận KHI:
  - ORM hỗ trợ tốt (Rails, Laravel, Django Polymorphic)
  - Application layer đảm bảo integrity (careful!!)
  - Types ổn định, ít thay đổi  
  - Data consistency KHÔNG critical (blog comments, likes)
  - Development speed > data integrity

❌ KHÔNG chấp nhận KHI:
  - Financial data, medical records, legal documents
  - Data consistency CRITICAL
  - Nhiều types (>5) và thường xuyên thêm mới
  - Cần complex queries across types
  - No ORM hoặc ORM không hỗ trợ polymorphic
```

---

## 4. Database as Queue

### Anti-pattern
```sql
-- ❌ Dùng table làm job queue, poll liên tục
CREATE TABLE email_queue (
  id BIGSERIAL PRIMARY KEY,
  to_email VARCHAR(254) NOT NULL,
  subject TEXT NOT NULL,
  body TEXT NOT NULL,
  status VARCHAR(50) DEFAULT 'pending',
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Workers poll mỗi 1 giây:
-- WHILE TRUE:
--   SELECT * FROM email_queue WHERE status = 'pending' LIMIT 10;
--   -- nếu có → UPDATE status = 'processing' → process → UPDATE status = 'done'
--   -- nếu không → sleep(1) → poll lại
```

### Tại sao có vấn đề
```
1. POLLING OVERHEAD
   - Query mỗi 1 giây × 10 workers = 600 queries/phút khi queue EMPTY
   - Tốn CPU, I/O, connection pool

2. TABLE BLOAT
   - UPDATE creates dead tuple (MVCC) → table phình lên
   - VACUUM phải chạy thường xuyên
   - Dead tuples ảnh hưởng query performance

3. LOCK CONTENTION
   - Nhiều workers cùng SELECT + UPDATE → race condition
   - Nếu không dùng SKIP LOCKED → duplicate processing

4. THIẾU FEATURES
   - Không có: retry with backoff, dead letter queue
   - Không có: priority queue, delayed jobs
   - Không có: rate limiting, consumer groups
   - Phải tự implement → complex, bug-prone

5. SCALE KÉM
   - >10,000 jobs/phút → DB trở thành bottleneck
   - Table grows → queries chậm dần
```

### ✅ Giải pháp phân theo scale
```
Scale               │ Giải pháp
════════════════════╪═══════════════════════════════════════════
< 100 jobs/phút    │ ✅ DB queue với SKIP LOCKED (xem concurrency.md)
100-10,000/phút    │ ✅ Redis + Bull/Celery
> 10,000/phút      │ ✅ RabbitMQ, Apache Kafka, AWS SQS
Real-time events   │ ✅ Redis Streams, Kafka
```

```sql
-- ========================================
-- Nếu PHẢI dùng DB queue → dùng SKIP LOCKED + NOTIFY
-- (xem chi tiết trong concurrency.md → Queue Table Pattern)
-- ========================================

-- Kết hợp LISTEN/NOTIFY để TRÁNH POLLING
-- Khi insert job mới:
CREATE OR REPLACE FUNCTION notify_new_job()
RETURNS TRIGGER AS $$
BEGIN
  PERFORM pg_notify('new_job', json_build_object(
    'job_id', NEW.id,
    'job_type', NEW.job_type
  )::TEXT);
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_notify_new_job
  AFTER INSERT ON job_queue
  FOR EACH ROW EXECUTE FUNCTION notify_new_job();

-- Worker: LISTEN thay vì poll
-- Application: LISTEN new_job;
-- Khi nhận notification → query table với SKIP LOCKED
-- → Không cần poll! Zero overhead khi queue empty!
```

---

## 5. Soft Delete Pitfalls

### Vấn đề thường gặp
```sql
-- ========================================
-- Pitfall 1: Quên filter ở MỌI NƠI
-- ========================================
-- Có 100+ queries trong codebase
-- TẤT CẢ phải thêm: AND deleted_at IS NULL
-- Nếu quên 1 chỗ → bugs ngầm, data leak

-- ❌ Quên filter:
SELECT * FROM users WHERE email = 'test@example.com';
-- → Trả về user đã bị "xóa"!

-- ❌ Join quên filter:
SELECT o.* FROM orders o
JOIN users u ON u.id = o.user_id;
-- → Join với deleted users!

-- ========================================
-- Pitfall 2: UNIQUE constraint fail
-- ========================================
-- User A xóa (soft) account → email 'test@example.com' có deleted_at
-- User B muốn đăng ký cùng email → UNIQUE constraint VIOLATION!
-- Vì UNIQUE(email) vẫn count cả soft-deleted rows

-- ========================================
-- Pitfall 3: Cascading issues
-- ========================================
-- Soft delete parent → children vẫn active
-- Soft delete user → orders, comments, posts vẫn visible
-- → Phải cascade soft delete thủ công (complex!)

-- ========================================
-- Pitfall 4: Table bloat
-- ========================================
-- Soft deleted rows vẫn chiếm disk space
-- Index size lớn hơn cần thiết
-- Queries scan nhiều rows hơn
```

### ✅ Giải pháp
```sql
-- ========================================
-- Solution 1: View để đảm bảo luôn filter
-- ========================================
CREATE VIEW active_users AS
SELECT * FROM users WHERE deleted_at IS NULL;

-- Application queries active_users thay vì users
-- → Không bao giờ quên filter!

-- ========================================
-- Solution 2: Partial UNIQUE index
-- ========================================
-- Chỉ enforce unique cho ACTIVE records
CREATE UNIQUE INDEX idx_users_email_active
ON users(email) WHERE deleted_at IS NULL;
-- ✅ User A soft delete email 'test@example.com'
-- ✅ User B đăng ký 'test@example.com' → OK! (vì User A đã deleted)

-- ========================================
-- Solution 3: Xem xét alternatives
-- ========================================

-- Alternative 1: Archive table
-- Move deleted records sang bảng riêng
CREATE TABLE users_archive (LIKE users INCLUDING ALL);

-- "Delete" = move to archive
WITH deleted AS (
  DELETE FROM users WHERE id = 123 RETURNING *
)
INSERT INTO users_archive SELECT * FROM deleted;

-- Lợi ích:
-- ✅ Main table nhỏ, nhanh
-- ✅ UNIQUE constraint hoạt động bình thường
-- ✅ Không cần filter everywhere

-- Alternative 2: Status column thay vì deleted_at
-- is_active = FALSE thay vì deleted_at IS NOT NULL
-- Simpler, nhưng không lưu THỜI ĐIỂM xóa

-- ========================================
-- Decision guide: Khi nào dùng Soft Delete?
-- ========================================
-- ✅ Soft Delete khi:
--   - Compliance yêu cầu audit trail
--   - User cần "undo" delete
--   - Data cần giữ cho reporting/analytics
--   - FK references KHÔNG cascade delete tốt
--
-- ❌ Tránh Soft Delete khi:
--   - Data không quan trọng (logs, temp records)
--   - No audit requirement
--   - Table grow rate cao → bloat
--   - UNIQUE constraints phức tạp
```

---

## 6. Over-normalization

### Anti-pattern
```sql
-- ❌ Tách QUÁT MỨC khi không cần thiết
CREATE TABLE first_names (id SERIAL PRIMARY KEY, value VARCHAR(100));
CREATE TABLE last_names (id SERIAL PRIMARY KEY, value VARCHAR(100));
CREATE TABLE cities (id SERIAL PRIMARY KEY, name VARCHAR(100));
CREATE TABLE streets (id SERIAL PRIMARY KEY, name VARCHAR(255));

CREATE TABLE users (
  id BIGSERIAL PRIMARY KEY,
  first_name_id INT REFERENCES first_names(id),  -- WHY?
  last_name_id INT REFERENCES last_names(id),     -- WHY?
  city_id INT REFERENCES cities(id),              -- WHY?
  street_id INT REFERENCES streets(id)            -- WHY?
);

-- Lấy user name + address = 4 JOINs!
SELECT u.id, fn.value, ln.value, c.name, s.name
FROM users u
JOIN first_names fn ON fn.id = u.first_name_id
JOIN last_names ln ON ln.id = u.last_name_id
JOIN cities c ON c.id = u.city_id
JOIN streets s ON s.id = u.street_id;
-- Performance tệ, code phức tạp, maintenance khó
```

### ✅ Guideline: Normalize đúng mức
```
❌ Không tách thành lookup table KHI:
  - Giá trị là free-text (tên người, địa chỉ)
  - Không cần query/filter riêng theo giá trị đó
  - Giá trị unique per entity (email cá nhân)
  - JOIN cost > storage savings
  - Không ai cần maintain danh sách giá trị

✅ NÊN tách KHI:
  - Giá trị thuộc danh sách có giới hạn (countries, categories)
  - Giá trị cần metadata riêng (icon, description, sort_order)
  - Giá trị dùng ở NHIỀU bảng (categories, tags, roles)
  - Cần thay đổi tên/label tập trung (rename category → update 1 chỗ)
  - Cần enforce danh sách hợp lệ (status codes, types)

🎯 Rule of thumb:
  Normalize đến 3NF, sau đó DENORMALIZE CÓ CHỦ ĐÍCH
  Mỗi lần denormalize → DOCUMENT LÝ DO
```

---

## 7. Natural Key as PK

### Anti-pattern
```sql
-- ❌ Business field làm Primary Key
CREATE TABLE students (
  student_code VARCHAR(20) PRIMARY KEY,  -- 'SV2024001' → có thể thay đổi!
  name VARCHAR(255) NOT NULL
);

CREATE TABLE enrollments (
  student_code VARCHAR(20) REFERENCES students(student_code),
  course_code VARCHAR(20) REFERENCES courses(course_code),
  PRIMARY KEY (student_code, course_code)
);

-- Vấn đề: Student code format thay đổi từ 'SV2024001' → '2024-CT-001'
-- → Phải UPDATE students SET student_code = '2024-CT-001' WHERE student_code = 'SV2024001'
-- → Phải CASCADE UPDATE enrollments, transcripts, payments, ...
-- → Trên bảng triệu rows → LOCK + SLOW + RISKY
```

### ✅ Giải pháp
```sql
CREATE TABLE students (
  id BIGSERIAL PRIMARY KEY,                        -- Surrogate key
  student_code VARCHAR(20) UNIQUE NOT NULL,        -- Business key → UNIQUE
  name VARCHAR(255) NOT NULL
);

CREATE TABLE enrollments (
  id BIGSERIAL PRIMARY KEY,
  student_id BIGINT NOT NULL REFERENCES students(id),  -- FK dùng surrogate key
  course_id BIGINT NOT NULL REFERENCES courses(id),
  UNIQUE (student_id, course_id)
);

-- Student code thay đổi? UPDATE 1 row trong students!
-- enrollments, transcripts, payments → KHÔNG CẦN THAY ĐỔI GÌ!
```

---

## 8-12. Other Anti-Patterns (Quick Reference)

### 8. Calculated Values Without Sync
```sql
-- ❌ total_amount lưu nhưng không ai update khi order_items thay đổi
-- ✅ Dùng trigger hoặc VIEW hoặc application logic + document rõ

-- Trigger pattern:
CREATE OR REPLACE FUNCTION update_order_total()
RETURNS TRIGGER AS $$
BEGIN
  UPDATE orders SET total_amount = (
    SELECT COALESCE(SUM(quantity * unit_price), 0)
    FROM order_items WHERE order_id = COALESCE(NEW.order_id, OLD.order_id)
  ) WHERE id = COALESCE(NEW.order_id, OLD.order_id);
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_update_order_total
  AFTER INSERT OR UPDATE OR DELETE ON order_items
  FOR EACH ROW EXECUTE FUNCTION update_order_total();
```

### 9. CSV Strings Instead of Relationships
```sql
-- ❌ 'electronics,fashion,books' trong 1 cột
-- ✅ Junction table (xem relational-design.md)
```

### 10. Files/Blobs in Database
```sql
-- ❌ BYTEA 10MB PDF → DB bloat, backup siêu chậm
-- ✅ Object storage (S3/GCS) + metadata table (xem domain-patterns.md → Media)
```

### 11. Missing Timezone
```sql
-- ❌ TIMESTAMP (without timezone) → ambiguous
-- ✅ TIMESTAMPTZ + luôn lưu UTC + convert ở app layer
```

### 12. Over-indexing
```sql
-- ❌ Index MỌI cột "để safe"
-- → Mỗi INSERT/UPDATE/DELETE phải update TẤT CẢ indexes
-- → Write performance giảm 50-80%
-- ✅ Chỉ index cột trong WHERE, JOIN, ORDER BY thường xuyên
-- ✅ Monitor: pg_stat_user_indexes → unused indexes → DROP!
```

---

## Tổng Kết: Anti-Pattern Quick Reference

| # | Anti-Pattern | Dấu hiệu nhận biết | Giải pháp |
|---|---|---|---|
| 1 | God Table | 50+ cột, 80% NULL, type column | Tách bảng theo entity |
| 2 | EAV | {entity_id, attr_name, attr_value} | JSONB hoặc dedicated columns |
| 3 | Polymorphic FK | {target_type, target_id} không có FK | Separate FKs / base table |
| 4 | DB as Queue | Poll-based, status column | Message broker / SKIP LOCKED |
| 5 | Soft Delete bugs | deleted_at + quên filter | View + partial UNIQUE index |
| 6 | Over-normalize | Lookup tables cho free-text | Inline simple values |
| 7 | Natural Key PK | Business field là PK | Surrogate key + UNIQUE |
| 8 | Unsync'ed calc | Denorm column, no trigger | Trigger / VIEW / document |
| 9 | CSV in column | Comma-separated in TEXT | Junction table |
| 10 | Blob in DB | BYTEA for large files | Object storage + metadata |
| 11 | No timezone | TIMESTAMP (no TZ) | TIMESTAMPTZ + UTC |
| 12 | Over-indexing | Index mọi cột | Monitor + DROP unused |
