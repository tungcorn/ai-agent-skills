# Concurrency & Transaction Management

## Table of Contents
1. [ACID Properties](#acid)
2. [Transaction Isolation Levels](#isolation)
3. [Locking Strategies](#locking)
4. [Optimistic vs Pessimistic Concurrency](#opt-vs-pess)
5. [Deadlock Prevention](#deadlock)
6. [Concurrency Patterns trong Schema Design](#patterns)
7. [Concurrency theo Domain](#domain-concurrency)

---

## 1. ACID Properties

### 1.1 Giải thích chi tiết
```
A — ATOMICITY (Tính nguyên tử)
    "All or nothing" — Transaction là một khối duy nhất.
    Nếu bất kỳ operation nào fail → TOÀN BỘ rollback.
    
    Ví dụ thực tế — Chuyển tiền:
    BEGIN;
      UPDATE accounts SET balance = balance - 500000 WHERE id = 1;  -- Trừ A
      UPDATE accounts SET balance = balance + 500000 WHERE id = 2;  -- Cộng B
    COMMIT;
    → Nếu "Cộng B" fail → "Trừ A" cũng rollback → không mất tiền

C — CONSISTENCY (Tính nhất quán)
    Database luôn ở trạng thái hợp lệ trước VÀ sau transaction.
    Mọi constraints, triggers, cascades đều được enforce.
    
    Ví dụ: CHECK (balance >= 0) → nếu trừ quá số dư → transaction fail, rollback.
    Balance trước: 500,000. Trừ 600,000 → CHECK fail → rollback.

I — ISOLATION (Tính cô lập)
    Transactions chạy đồng thời KHÔNG ảnh hưởng lẫn nhau.
    Mỗi transaction "thấy" database như thể chỉ có mình nó.
    Mức độ cô lập có thể điều chỉnh (xem section 2).
    
    Ví dụ: T1 đang đọc balance, T2 đang update balance.
    Tùy isolation level, T1 có thể thấy giá trị CŨ hoặc phải ĐỢI T2 commit.

D — DURABILITY (Tính bền vững)
    Sau khi COMMIT thành công → dữ liệu đã lưu VĨNH VIỄN.
    Server crash, mất điện → data vẫn còn (WAL - Write-Ahead Log).
    
    Cách hoạt động: PostgreSQL ghi WAL trước, rồi mới ghi data files.
    Nếu crash giữa chừng → recover từ WAL khi restart.
```

### 1.2 ACID vs BASE — Khi nào dùng gì?
```
ACID (SQL — Strong Consistency):              BASE (NoSQL — Eventual Consistency):
──────────────────────────────────             ──────────────────────────────────
✅ Tài chính (banking, payments)              ✅ Social feeds, likes, views
✅ Inventory management                       ✅ Caching, sessions
✅ Order processing                           ✅ Analytics, logging
✅ Booking/reservation systems                ✅ Content delivery
✅ Medical records                            ✅ IoT sensor data
✅ Mọi thứ cần "exactly once"                ✅ "Eventually consistent" OK

BASE = Basically Available, Soft-state, Eventually consistent:
- BA: Hệ thống luôn available (dù không đảm bảo consistency ngay)
- S:  State có thể thay đổi theo thời gian (không cần stable)
- E:  Dữ liệu sẽ consistent... eventually (sau vài ms hoặc seconds)
```

---

## 2. Transaction Isolation Levels

### 2.1 Concurrency Problems (Tại sao cần Isolation)
```sql
-- ════════════════════════════════════════════
-- DIRTY READ: Đọc data chưa commit
-- ════════════════════════════════════════════
-- Time  | Transaction T1              | Transaction T2
-- ──────┼─────────────────────────────┼─────────────────────
-- t1    | BEGIN;                      |
-- t2    | UPDATE accounts             |
--       | SET balance = 500           |
--       | WHERE id = 1;              |
--       | (balance was 1000)          |
-- t3    |                             | BEGIN;
-- t4    |                             | SELECT balance FROM accounts
--       |                             | WHERE id = 1;
--       |                             | → Đọc 500 (chưa commit!)
-- t5    | ROLLBACK;                   |
--       | (balance quay lại 1000)     |
-- t6    |                             | -- T2 đã dùng giá trị 500 → SAI!
--       |                             | COMMIT;

-- ════════════════════════════════════════════
-- NON-REPEATABLE READ: Đọc cùng row, kết quả khác
-- ════════════════════════════════════════════
-- Time  | Transaction T1              | Transaction T2
-- ──────┼─────────────────────────────┼─────────────────────
-- t1    | BEGIN;                      |
-- t2    | SELECT balance FROM accounts|
--       | WHERE id = 1;              |
--       | → balance = 1000           |
-- t3    |                             | BEGIN;
-- t4    |                             | UPDATE accounts SET balance = 500
--       |                             | WHERE id = 1;
-- t5    |                             | COMMIT;
-- t6    | SELECT balance FROM accounts|
--       | WHERE id = 1;              |
--       | → balance = 500 (khác!)    |
-- t7    | -- Quyết định dựa trên 2   |
--       | -- số khác nhau → logic SAI|

-- ════════════════════════════════════════════
-- PHANTOM READ: Số rows thay đổi giữa 2 lần query
-- ════════════════════════════════════════════
-- Time  | Transaction T1              | Transaction T2
-- ──────┼─────────────────────────────┼─────────────────────
-- t1    | BEGIN;                      |
-- t2    | SELECT COUNT(*) FROM orders |
--       | WHERE status = 'pending';   |
--       | → 10 orders                |
-- t3    |                             | BEGIN;
-- t4    |                             | INSERT INTO orders (status)
--       |                             | VALUES ('pending');
-- t5    |                             | COMMIT;
-- t6    | SELECT COUNT(*) FROM orders |
--       | WHERE status = 'pending';   |
--       | → 11 orders (phantom!)     |
```

### 2.2 Bốn Isolation Levels (ANSI SQL)
```
═══════════════════════════════════════════════════════════════════════
 Level               │ Dirty Read  │ Non-Repeat  │ Phantom   │ Speed
═══════════════════════════════════════════════════════════════════════
 READ UNCOMMITTED    │ ❌ Có thể   │ ❌ Có thể   │ ❌ Có thể │ ⚡ Fastest
 READ COMMITTED      │ ✅ Ngăn     │ ❌ Có thể   │ ❌ Có thể │ ⚡ Fast
 REPEATABLE READ     │ ✅ Ngăn     │ ✅ Ngăn     │ ❌ Có thể │ ⚡ Good
 SERIALIZABLE        │ ✅ Ngăn     │ ✅ Ngăn     │ ✅ Ngăn   │ 🐢 Slowest
═══════════════════════════════════════════════════════════════════════

Defaults:
- PostgreSQL: READ COMMITTED
- MySQL InnoDB: REPEATABLE READ
- SQL Server: READ COMMITTED
- Oracle: READ COMMITTED
```

### 2.3 SQL Syntax & Ví Dụ
```sql
-- ========================================
-- Set isolation level
-- ========================================

-- PostgreSQL: Set cho 1 transaction cụ thể
BEGIN ISOLATION LEVEL SERIALIZABLE;
  -- critical operations here
COMMIT;

-- PostgreSQL: Set cho session
SET DEFAULT_TRANSACTION_ISOLATION TO 'repeatable read';

-- MySQL
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
START TRANSACTION;
  -- critical operations here
COMMIT;

-- ========================================
-- Ví dụ thực tế: Chọn isolation level đúng
-- ========================================

-- 1. CRUD API thông thường → READ COMMITTED (default, đủ dùng)
BEGIN;
  UPDATE users SET display_name = 'New Name' WHERE id = 123;
COMMIT;

-- 2. Dashboard / Báo cáo → READ COMMITTED hoặc REPEATABLE READ
-- (đọc consistent snapshot trong 1 report)
BEGIN ISOLATION LEVEL REPEATABLE READ;
  SELECT SUM(total_amount) FROM orders WHERE created_at > '2024-01-01';
  SELECT COUNT(*) FROM orders WHERE status = 'completed';
  -- Cả 2 query đọc từ CÙNG snapshot → số liệu consistent
COMMIT;

-- 3. Financial: Chuyển tiền → SERIALIZABLE
BEGIN ISOLATION LEVEL SERIALIZABLE;
  -- Check balance
  SELECT balance FROM accounts WHERE id = 1;  -- 1000
  -- Nếu >= amount, chuyển
  UPDATE accounts SET balance = balance - 500 WHERE id = 1;
  UPDATE accounts SET balance = balance + 500 WHERE id = 2;
COMMIT;
-- Nếu có conflict → PostgreSQL serialization error → app retry

-- 4. Inventory: Trừ stock → SERIALIZABLE hoặc FOR UPDATE
BEGIN;
  SELECT stock_quantity FROM products WHERE id = 42 FOR UPDATE;
  -- Lock row → transaction khác phải đợi
  UPDATE products SET stock_quantity = stock_quantity - 1 WHERE id = 42;
COMMIT;
```

### 2.4 Khuyến nghị theo Use Case
```
Use Case                          │ Level khuyến nghị        │ Lý do
──────────────────────────────────┼──────────────────────────┼──────────────────
CRUD thông thường (REST APIs)     │ READ COMMITTED (default) │ Đủ dùng, performance tốt
Dashboard / Reporting             │ REPEATABLE READ          │ Consistent snapshot
Financial (transfer, payment)     │ SERIALIZABLE             │ Strong consistency
Inventory (stock deduction)       │ SERIALIZABLE + FOR UPDATE│ Prevent overselling
Booking (seats, rooms)            │ SERIALIZABLE             │ Prevent double booking
Counter/aggregation (likes, views)│ READ COMMITTED + atomic  │ Performance > precision
Analytics (read-heavy)            │ READ COMMITTED           │ Performance first
Batch processing                  │ READ COMMITTED           │ Từng batch nhỏ
```

---

## 3. Locking Strategies

### 3.1 Row-Level Locking
```sql
-- ========================================
-- FOR UPDATE: Exclusive lock — block cả read FOR UPDATE và write
-- ========================================
BEGIN;
  SELECT * FROM accounts WHERE id = 123 FOR UPDATE;
  -- Row bị lock! Transaction khác:
  -- - SELECT (bình thường) → OK, đọc được (old value)
  -- - SELECT ... FOR UPDATE → PHẢI ĐỢI
  -- - UPDATE/DELETE → PHẢI ĐỢI
  
  UPDATE accounts SET balance = balance - 100 WHERE id = 123;
COMMIT;
-- Lock release sau COMMIT hoặc ROLLBACK

-- ========================================
-- FOR UPDATE NOWAIT: Fail ngay nếu row bị lock
-- ========================================
BEGIN;
  SELECT * FROM accounts WHERE id = 123 FOR UPDATE NOWAIT;
  -- Nếu row đang bị lock → NGAY LẬP TỨC throw error:
  -- ERROR: could not obtain lock on row in relation "accounts"
  -- Application catch error → thông báo user "Thử lại sau"
COMMIT;

-- ========================================
-- FOR UPDATE SKIP LOCKED: Bỏ qua rows đang bị lock
-- ========================================
-- Perfect cho job queue pattern — nhiều workers xử lý song song
BEGIN;
  SELECT id, payload FROM job_queue
  WHERE status = 'pending'
  ORDER BY created_at
  LIMIT 5
  FOR UPDATE SKIP LOCKED;
  -- Chỉ trả về rows CHƯA bị lock
  -- Worker 1 lấy job 1-5, Worker 2 lấy job 6-10 (không trùng!)
COMMIT;

-- ========================================
-- FOR SHARE: Shared lock — cho phép đọc đồng thời, block write
-- ========================================
BEGIN;
  SELECT * FROM products WHERE id = 42 FOR SHARE;
  -- Nhiều transactions có thể FOR SHARE cùng lúc (shared read lock)
  -- Nhưng KHÔNG AI được UPDATE/DELETE row này (cho đến khi tất cả RELEASE)
  
  -- Useful khi cần đọc consistent data mà không cần update
  -- VD: Check product exists trước khi insert order_item
  INSERT INTO order_items (order_id, product_id, quantity, unit_price)
  VALUES (1, 42, 3, (SELECT price FROM products WHERE id = 42));
COMMIT;
```

### 3.2 Table-Level Locking (PostgreSQL)
```sql
-- ========================================
-- Ít dùng trong application, thường cho maintenance
-- ========================================

-- LOCK TABLE — Full table lock
LOCK TABLE products IN ACCESS EXCLUSIVE MODE;
-- Chặn MỌI thứ (đọc, ghi) → dùng cho schema changes

LOCK TABLE products IN SHARE MODE;
-- Cho phép đọc, chặn ghi → dùng khi dump table

-- ⚠️ TRÁNH table-level lock trong application code!
-- Dùng row-level locks (FOR UPDATE) thay thế
```

### 3.3 Advisory Locks (Application-level Logic Locks)
```sql
-- ========================================
-- Lock một "resource" bằng key — không lock row thật sự
-- Useful cho business logic locks
-- ========================================

-- Ví dụ 1: Chỉ cho 1 worker process payment tại 1 thời điểm
-- Session-level lock (phải manually unlock hoặc disconnect)
SELECT pg_advisory_lock(hashtext('process_payment_order_' || '123'));
  -- ... process payment cho order 123 ...
  -- ... gọi API gateway, ghi DB ...
SELECT pg_advisory_unlock(hashtext('process_payment_order_' || '123'));

-- Ví dụ 2: Chỉ cho 1 cron job chạy report monthly tại 1 thời điểm
-- Transaction-level lock (tự release khi COMMIT/ROLLBACK)
BEGIN;
  SELECT pg_advisory_xact_lock(hashtext('monthly_report_generation'));
  -- ... generate report (10 minutes) ...
  -- ... nếu cron khác chạy → pg_advisory_xact_lock sẽ ĐỢI ...
COMMIT;  -- lock release

-- Ví dụ 3: Try lock (không đợi, return false nếu locked)
SELECT pg_try_advisory_lock(hashtext('daily_cleanup'));
-- Returns: true (acquired) or false (already locked by another)
-- → Nếu false → skip job này, để instance khác chạy
```

---

## 4. Optimistic vs Pessimistic Concurrency

### 4.1 Optimistic Concurrency (Version-based)
```sql
-- ========================================
-- Concept: "Giả sử không có conflict, check khi commit"
-- Schema: Thêm version column (hoặc updated_at)
-- ========================================

CREATE TABLE products (
  id BIGSERIAL PRIMARY KEY,
  name VARCHAR(500) NOT NULL,
  price DECIMAL(19,4) NOT NULL,
  stock_quantity INT NOT NULL DEFAULT 0,
  description TEXT,
  -- ✅ Version column cho optimistic locking
  version INT NOT NULL DEFAULT 1,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ========================================
-- Flow: Optimistic Update
-- ========================================

-- Step 1: Application đọc product (kèm version)
SELECT id, name, price, stock_quantity, version
FROM products WHERE id = 42;
-- → {id: 42, name: "Widget", price: 29.99, stock: 100, version: 5}

-- Step 2: User chỉnh sửa trên UI (mất vài giây/phút)
-- ... UI form ...

-- Step 3: Application update — CHECK version!
UPDATE products
SET
  name = 'Updated Widget',
  price = 39.99,
  version = version + 1,          -- ← Tăng version
  updated_at = NOW()
WHERE id = 42 AND version = 5;    -- ← Check version cũ!

-- Step 4: Check kết quả
-- Nếu affected_rows = 1 → ✅ Update thành công
-- Nếu affected_rows = 0 → ❌ Conflict!
--   → Ai đó đã update trước (version đã > 5)
--   → Application: reload data, hiện conflict message

-- ========================================
-- Alternative: Dùng updated_at thay version
-- (kém chính xác hơn — 2 updates trong cùng millisecond)
-- ========================================
UPDATE products
SET name = 'Updated Widget', updated_at = NOW()
WHERE id = 42 AND updated_at = '2024-01-15 10:30:00.123456';
```

```
Khi nào dùng Optimistic:
✅ Read-heavy, write ít conflict (web apps, CMS, blogs)
✅ HTTP/REST stateless (read → UI edit → submit)
✅ Short edit operations (user form, quick edits)
✅ Conflict hiếm xảy ra (<5% requests)
✅ Conflict resolution đơn giản (retry hoặc merge)

Schema Requirements:
  - Thêm cột: version INT NOT NULL DEFAULT 1
  - Application: đọc version → update WHERE version = old_version
  - Application: check affected_rows → handle conflict
```

### 4.2 Pessimistic Concurrency (Lock-based)
```sql
-- ========================================
-- Concept: "Lock row TRƯỚC, rồi mới đọc/update"
-- Không cần version column
-- ========================================

-- Ví dụ 1: Bank Transfer
BEGIN;
  -- Lock CÙNG LÚC cả 2 accounts (order by id để tránh deadlock!)
  SELECT balance FROM accounts
  WHERE id IN (1, 2) ORDER BY id
  FOR UPDATE;
  -- → Account 1: balance = 1,000,000
  -- → Account 2: balance = 500,000
  
  -- Check business rule
  -- IF account_1.balance >= transfer_amount THEN
  UPDATE accounts SET balance = balance - 500000 WHERE id = 1;
  UPDATE accounts SET balance = balance + 500000 WHERE id = 2;
COMMIT;
-- Lock release → transaction khác có thể tiếp tục

-- Ví dụ 2: Inventory Stock Deduction
BEGIN;
  -- Lock product row
  SELECT stock_quantity FROM products WHERE id = 42 FOR UPDATE;
  -- → stock = 5
  
  -- Check: đủ hàng không?
  -- IF stock >= requested_quantity THEN
  UPDATE products SET stock_quantity = stock_quantity - 3 WHERE id = 42;
  INSERT INTO order_items (order_id, product_id, quantity)
  VALUES (100, 42, 3);
COMMIT;

-- Ví dụ 3: Booking (đặt chỗ)
BEGIN;
  -- Lock seat row
  SELECT id FROM seats WHERE show_id = 1 AND seat_number = 'A12' FOR UPDATE;
  -- Check: chưa ai đặt?
  SELECT is_booked FROM seats WHERE show_id = 1 AND seat_number = 'A12';
  -- IF NOT is_booked THEN
  UPDATE seats SET is_booked = TRUE, booked_by = 123 
  WHERE show_id = 1 AND seat_number = 'A12';
COMMIT;
```

```
Khi nào dùng Pessimistic:
✅ Write-heavy, conflict THƯỜNG XUYÊN (>10% requests)
✅ Financial/banking operations (chuyển tiền, thanh toán)
✅ Inventory management (trừ kho, đặt hàng)
✅ Booking systems (đặt vé, đặt phòng, đặt bàn)
✅ Conflict COSTLY (rollback có side effects)
✅ Short transactions (lock nhanh, release nhanh)

⚠️ Nhược điểm:
  - Có thể gây DEADLOCK (xem Section 5)
  - Giảm throughput (threads phải đợi lock)
  - Connection bị giữ lâu hơn (tốn pool)
  - KHÔNG dùng cho long-running operations (user form, API calls)
```

### 4.3 So sánh chi tiết
```
                        │ Optimistic                  │ Pessimistic
════════════════════════╪═════════════════════════════╪═══════════════════════
Lock timing             │ Khi commit (check version)  │ Khi đọc (SELECT FOR UPDATE)
Conflict detection      │ Application (affected_rows) │ Database (lock mechanism)
Performance             │ Tốt khi ít conflict         │ Tốt khi nhiều conflict
Deadlock risk           │ ❌ Không có                 │ ⚠️ Có (xem section 5)
Schema change           │ Thêm version column         │ Không cần thay đổi
Long operations OK?     │ ✅ Có (lock lúc commit)     │ ❌ Không (lock quá lâu)
Code complexity         │ Medium (handle conflict)    │ Low (lock guarantees)
Scalability             │ ✅ Tốt hơn                  │ ⚠️ Kém hơn (contention)
Best for                │ Web CRUD, REST APIs, CMS    │ Financial, inventory, booking
```

---

## 5. Deadlock Prevention

### 5.1 Deadlock là gì?
```
Transaction A: Lock row 1, cần lock row 2
Transaction B: Lock row 2, cần lock row 1
→ Cả hai chờ nhau mãi mãi → DEADLOCK!

Database (PG/MySQL) tự detect deadlock sau timeout (~1s default)
→ Kill 1 transaction (thường transaction mới hơn)
→ Transaction đó nhận ERROR: deadlock detected

Dù DB tự xử lý, PHÒNG TRÁNH vẫn tốt hơn!
```

### 5.2 Strategies phòng tránh
```sql
-- ════════════════════════════════════════
-- Strategy 1: Lock theo thứ tự cố định (QUAN TRỌNG NHẤT)
-- ════════════════════════════════════════

-- ❌ Transaction A: lock user 100, rồi user 200
-- ❌ Transaction B: lock user 200, rồi user 100
-- → DEADLOCK!

-- ✅ LUÔN lock theo ID nhỏ trước
-- Transfer money: sort account IDs trước
BEGIN;
  -- Lock cả hai accounts, ORDER BY id!
  SELECT * FROM accounts WHERE id IN (100, 200) ORDER BY id FOR UPDATE;
  -- id=100 locked trước, rồi id=200
  -- Transaction khác cũng lock 100 trước → KHÔNG deadlock
  
  UPDATE accounts SET balance = balance - 500 WHERE id = 100;
  UPDATE accounts SET balance = balance + 500 WHERE id = 200;
COMMIT;

-- Application example (Python):
def transfer(from_id, to_id, amount):
    # Sort IDs to ensure consistent lock order
    lock_ids = sorted([from_id, to_id])
    with db.transaction():
        accounts = db.execute(
            "SELECT * FROM accounts WHERE id = ANY(%s) ORDER BY id FOR UPDATE",
            [lock_ids]
        )
        # ... validate and update ...

-- ════════════════════════════════════════
-- Strategy 2: Giữ transaction NGẮN nhất có thể
-- ════════════════════════════════════════

-- ❌ SAI: Transaction dài, hold lock lâu
BEGIN;
  SELECT * FROM orders WHERE id = 1 FOR UPDATE;   -- Lock order
  -- ... gọi external API (2-5 seconds) ...        -- ← CHỜ NETWORK
  -- ... complex business logic (1 second) ...     -- ← TÍNH TOÁN
  -- ... generate PDF (3 seconds) ...              -- ← I/O
  UPDATE orders SET status = 'processed' WHERE id = 1;
COMMIT;
-- Lock bị giữ 6-9 seconds → các request khác blocked!

-- ✅ ĐÚNG: Xử lý NGOÀI transaction, chỉ lock khi ghi
-- Step 1: Đọc data (KHÔNG lock)
order = db.query("SELECT * FROM orders WHERE id = 1");
-- Step 2: Xử lý business logic, gọi API, generate PDF (NGOÀI transaction)
api_result = call_external_api(order);
pdf = generate_pdf(order);
-- Step 3: Chỉ lock khi update (< 10ms)
BEGIN;
  UPDATE orders SET status = 'processed', pdf_url = pdf.url
  WHERE id = 1 AND status = 'pending';  -- Optimistic check
COMMIT;

-- ════════════════════════════════════════
-- Strategy 3: Set lock timeout
-- ════════════════════════════════════════

-- PostgreSQL
SET LOCAL lock_timeout = '3s';
-- → Nếu không lấy được lock trong 3 giây → error thay vì chờ mãi

-- MySQL
SET innodb_lock_wait_timeout = 3;

-- ════════════════════════════════════════
-- Strategy 4: SKIP LOCKED cho queue patterns
-- ════════════════════════════════════════

-- Worker lấy task → bỏ qua tasks đang bị lock
SELECT * FROM tasks
WHERE status = 'pending'
LIMIT 1
FOR UPDATE SKIP LOCKED;
-- → Deadlock IMPOSSIBLE (mỗi worker lấy task khác nhau)

-- ════════════════════════════════════════
-- Strategy 5: Retry logic trong application
-- ════════════════════════════════════════

-- Python example:
-- MAX_RETRIES = 3
-- for attempt in range(MAX_RETRIES):
--     try:
--         with db.transaction():
--             # ... critical section ...
--             break  # success
--     except DeadlockDetected:
--         if attempt == MAX_RETRIES - 1:
--             raise  # give up
--         time.sleep(random.uniform(0.01, 0.1))  # wait random time
```

---

## 6. Concurrency Patterns trong Schema Design

### 6.1 Idempotent Operations
```sql
-- ========================================
-- Đảm bảo operation chạy nhiều lần cho kết quả giống nhau
-- QUAN TRỌNG cho: payment processing, API endpoints, message consumers
-- ========================================

-- Idempotency key table
CREATE TABLE idempotency_keys (
  key VARCHAR(255) PRIMARY KEY,       -- UUID do client gửi
  request_path VARCHAR(500) NOT NULL, -- '/api/payments'
  request_body_hash VARCHAR(64),      -- SHA-256 hash of request body
  response_status INT NOT NULL,       -- HTTP status code
  response_body JSONB NOT NULL,       -- Cached response
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at TIMESTAMPTZ NOT NULL DEFAULT NOW() + INTERVAL '24 hours'
);

CREATE INDEX idx_idempotency_expires ON idempotency_keys(expires_at);

-- ========================================
-- Flow:
-- 1. Client gửi request kèm Idempotency-Key header
-- 2. Server check idempotency_keys:
--    a. Nếu key đã tồn tại → trả cached response (KHÔNG xử lý lại)
--    b. Nếu chưa → xử lý → lưu key + response
-- 3. Dùng UNIQUE key để tránh race condition (2 requests cùng key)
-- ========================================

-- Cleanup expired keys periodically
DELETE FROM idempotency_keys WHERE expires_at < NOW();
```

### 6.2 Safe Counter Updates (Atomic Operations)
```sql
-- ========================================
-- ❌ Race condition khi update counter
-- ========================================
-- Thread 1: SELECT views FROM posts WHERE id = 1;  → 100
-- Thread 2: SELECT views FROM posts WHERE id = 1;  → 100
-- Thread 1: UPDATE posts SET views = 101 WHERE id = 1;
-- Thread 2: UPDATE posts SET views = 101 WHERE id = 1;
-- → Kết quả: 101 (mất 1 increment!)

-- ========================================
-- ✅ Atomic increment (không cần explicit lock)
-- ========================================

-- Simple counter
UPDATE posts SET view_count = view_count + 1 WHERE id = 1;
-- PostgreSQL xử lý atomically → thread-safe

-- Counter với condition
UPDATE products
SET stock_quantity = stock_quantity - 3
WHERE id = 42 AND stock_quantity >= 3;
-- Nếu affected_rows = 0 → hết hàng hoặc không đủ

-- Counter với RETURNING
UPDATE products
SET stock_quantity = stock_quantity - 3
WHERE id = 42 AND stock_quantity >= 3
RETURNING stock_quantity;
-- Trả về giá trị mới → app biết stock còn bao nhiêu

-- ========================================
-- Batch counter (cho high-frequency events: likes, views)
-- ========================================

-- Thay vì update trực tiếp trên bảng chính (hot row contention)
-- Gom vào buffer table, rồi flush periodically

CREATE TABLE counter_buffer (
  id BIGSERIAL PRIMARY KEY,
  target_table VARCHAR(100) NOT NULL,
  target_id BIGINT NOT NULL,
  counter_name VARCHAR(100) NOT NULL,
  increment INT NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Application: INSERT vào buffer (nhanh, không contention)
INSERT INTO counter_buffer (target_table, target_id, counter_name, increment)
VALUES ('posts', 42, 'view_count', 1);

-- Cron job: Flush buffer mỗi 10 giây
WITH aggregated AS (
  DELETE FROM counter_buffer
  WHERE target_table = 'posts'
  RETURNING target_id, counter_name, increment
)
UPDATE posts
SET view_count = view_count + agg.total
FROM (
  SELECT target_id, SUM(increment) AS total
  FROM aggregated
  GROUP BY target_id
) agg
WHERE posts.id = agg.target_id;
```

### 6.3 Queue Table Pattern (với SKIP LOCKED)
```sql
-- ========================================
-- Pattern: Database-backed Job Queue (concurrent-safe)
-- Dùng khi: không muốn setup RabbitMQ/Redis nhưng cần job queue
-- ========================================

CREATE TABLE job_queue (
  id BIGSERIAL PRIMARY KEY,
  
  -- Job definition
  job_type VARCHAR(100) NOT NULL,     -- 'send_email', 'generate_report'
  priority INT NOT NULL DEFAULT 0,    -- Higher = more important
  payload JSONB NOT NULL,
  
  -- Execution tracking
  status VARCHAR(50) NOT NULL DEFAULT 'pending'
    CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'dead')),
  attempts INT NOT NULL DEFAULT 0,
  max_attempts INT NOT NULL DEFAULT 5,
  
  -- Worker tracking
  locked_by VARCHAR(255),             -- Worker hostname/ID
  locked_at TIMESTAMPTZ,
  
  -- Results
  result JSONB,
  error_message TEXT,
  last_error_at TIMESTAMPTZ,
  completed_at TIMESTAMPTZ,
  
  -- Scheduling
  scheduled_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes
CREATE INDEX idx_job_queue_pending ON job_queue(priority DESC, scheduled_at)
  WHERE status = 'pending';
CREATE INDEX idx_job_queue_locked ON job_queue(locked_by)
  WHERE status = 'processing';

-- ========================================
-- Worker: Pick next job (concurrent-safe, no deadlock)
-- ========================================
WITH next_job AS (
  SELECT id FROM job_queue
  WHERE status = 'pending'
    AND scheduled_at <= NOW()
    AND attempts < max_attempts
  ORDER BY priority DESC, scheduled_at
  LIMIT 1
  FOR UPDATE SKIP LOCKED    -- ← CRITICAL: skip locked rows
)
UPDATE job_queue SET
  status = 'processing',
  locked_by = 'worker-01-abc',
  locked_at = NOW(),
  attempts = attempts + 1
FROM next_job
WHERE job_queue.id = next_job.id
RETURNING job_queue.*;

-- ========================================
-- Mark job completed / failed
-- ========================================
-- Success:
UPDATE job_queue SET
  status = 'completed', result = '{"sent": true}', completed_at = NOW(),
  locked_by = NULL, locked_at = NULL
WHERE id = 123;

-- Failure:
UPDATE job_queue SET
  status = CASE WHEN attempts >= max_attempts THEN 'dead' ELSE 'pending' END,
  error_message = 'Connection timeout',
  last_error_at = NOW(),
  locked_by = NULL, locked_at = NULL,
  -- Exponential backoff: retry sau 2^attempts seconds
  scheduled_at = NOW() + (POWER(2, attempts) || ' seconds')::INTERVAL
WHERE id = 123;

-- ========================================
-- Cleanup stale locks (worker crashed without unlocking)
-- Run every 5 minutes
-- ========================================
UPDATE job_queue SET
  status = 'pending',
  locked_by = NULL,
  locked_at = NULL
WHERE status = 'processing'
  AND locked_at < NOW() - INTERVAL '10 minutes';
```

### 6.4 Distributed Lock với Database
```sql
-- ========================================
-- Khi cần lock resource across multiple app instances
-- Simpler alternative to Redis SETNX
-- ========================================

CREATE TABLE distributed_locks (
  lock_name VARCHAR(255) PRIMARY KEY,
  locked_by VARCHAR(255) NOT NULL,      -- Instance identifier
  locked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at TIMESTAMPTZ NOT NULL,      -- Auto-expire
  metadata JSONB
);

-- Acquire lock
INSERT INTO distributed_locks (lock_name, locked_by, expires_at)
VALUES ('cron:daily-cleanup', 'worker-01', NOW() + INTERVAL '30 minutes')
ON CONFLICT (lock_name) DO NOTHING;
-- Nếu INSERT thành công → acquired
-- Nếu ON CONFLICT → lock đã tồn tại → NOT acquired

-- Release lock
DELETE FROM distributed_locks
WHERE lock_name = 'cron:daily-cleanup' AND locked_by = 'worker-01';

-- Cleanup expired locks
DELETE FROM distributed_locks WHERE expires_at < NOW();
```

---

## 7. Concurrency theo Domain

### 7.1 E-commerce: Stock Management
```sql
-- ========================================
-- Prevent overselling: PESSIMISTIC lock
-- ========================================
BEGIN;
  -- Lock product variant
  SELECT stock_quantity FROM product_variants
  WHERE id = 42 FOR UPDATE;
  -- stock = 5

  -- Check & deduct
  UPDATE product_variants
  SET stock_quantity = stock_quantity - 3
  WHERE id = 42 AND stock_quantity >= 3;
  -- affected_rows = 1 → OK
  -- affected_rows = 0 → Not enough stock → ROLLBACK

  -- Create order item
  INSERT INTO order_items (...) VALUES (...);
COMMIT;

-- ========================================
-- Alternative: OPTIMISTIC with stock reservation
-- ========================================
-- Step 1: Reserve stock (soft lock, expire after 15 mins)
INSERT INTO stock_reservations (variant_id, quantity, user_id, expires_at)
VALUES (42, 3, 123, NOW() + INTERVAL '15 minutes');

-- Step 2: On checkout, convert reservation to order
-- Step 3: Cron: release expired reservations
DELETE FROM stock_reservations WHERE expires_at < NOW();
```

### 7.2 Financial: Account Balance
```sql
-- ALWAYS use SERIALIZABLE + FOR UPDATE
BEGIN ISOLATION LEVEL SERIALIZABLE;
  SELECT balance FROM accounts WHERE id = 1 FOR UPDATE;
  -- balance = 1,000,000
  
  -- Business rule: balance >= withdrawal
  UPDATE accounts SET balance = balance - 500000
  WHERE id = 1 AND balance >= 500000;
  
  -- Record transaction
  INSERT INTO transaction_ledger (account_id, type, amount, balance_after)
  VALUES (1, 'withdrawal', 500000, 500000);
COMMIT;
```

### 7.3 Booking: Seat Reservation
```sql
-- Double booking prevention
BEGIN ISOLATION LEVEL SERIALIZABLE;
  -- Check seat availability
  SELECT is_booked FROM seats
  WHERE show_id = 1 AND seat_number = 'A12'
  FOR UPDATE;
  
  -- If not booked → reserve
  UPDATE seats SET
    is_booked = TRUE,
    booked_by = 123,
    booked_at = NOW()
  WHERE show_id = 1 AND seat_number = 'A12' AND is_booked = FALSE;
  
  -- affected_rows check in application
COMMIT;
```
