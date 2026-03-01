# Advanced Database Patterns

## Table of Contents
1. [Temporal Tables & Versioned Records](#temporal)
2. [Slowly Changing Dimensions (SCD)](#scd)
3. [Schema Evolution & Change Management](#schema-evolution)
4. [Backup & Disaster Recovery](#backup)
5. [High Availability & Replication](#ha)
6. [Data Governance](#governance)
7. [Database Testing](#testing)

---

## 1. Temporal Tables & Versioned Records

### 1.1 Tại sao cần?
```
Use Cases:
- Audit trail: "Ai thay đổi record này, khi nào, từ gì sang gì?"
- Point-in-time recovery: "Product price lúc 2pm hôm qua là bao nhiêu?"
- Legal/Compliance: Ngân hàng, bảo hiểm, y tế PHẢI giữ lịch sử
- Debug: "Data đúng trước khi bị sửa sai, giá trị cũ là gì?"
- Analytics: "Revenue trend theo thời gian, xét cả price changes"
- Undo/Revert: User muốn khôi phục version cũ
```

### 1.2 Pattern 1: History Table (Manual — phổ biến nhất)
```sql
-- ========================================
-- Bảng chính: Chỉ chứa data HIỆN TẠI
-- ========================================
CREATE TABLE products (
  id BIGSERIAL PRIMARY KEY,
  name VARCHAR(500) NOT NULL,
  price DECIMAL(19,4) NOT NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'active',
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by BIGINT REFERENCES users(id)
);

-- ========================================
-- Bảng History: Lưu MỌI phiên bản cũ
-- ========================================
CREATE TABLE products_history (
  history_id BIGSERIAL PRIMARY KEY,
  
  -- Data snapshot (copy toàn bộ columns từ products)
  product_id BIGINT NOT NULL,       -- Logical FK (không FK thật vì product có thể bị hard delete)
  name VARCHAR(500) NOT NULL,
  price DECIMAL(19,4) NOT NULL,
  status VARCHAR(50) NOT NULL,
  
  -- Temporal metadata
  valid_from TIMESTAMPTZ NOT NULL,   -- Bắt đầu có hiệu lực
  valid_to TIMESTAMPTZ NOT NULL,     -- Hết hiệu lực (= thời điểm bị thay đổi)
  
  -- Change metadata
  change_type VARCHAR(10) NOT NULL 
    CHECK (change_type IN ('INSERT', 'UPDATE', 'DELETE')),
  changed_by BIGINT,                 -- User thực hiện thay đổi
  change_reason TEXT                 -- Optional: lý do thay đổi
);

CREATE INDEX idx_products_history_product ON products_history(product_id, valid_to DESC);
CREATE INDEX idx_products_history_time ON products_history(valid_from, valid_to);

-- ========================================
-- Trigger: Tự động lưu history
-- ========================================
CREATE OR REPLACE FUNCTION products_history_trigger()
RETURNS TRIGGER AS $$
BEGIN
  IF TG_OP = 'INSERT' THEN
    INSERT INTO products_history 
      (product_id, name, price, status, valid_from, valid_to, change_type, changed_by)
    VALUES 
      (NEW.id, NEW.name, NEW.price, NEW.status, NOW(), '9999-12-31', 'INSERT', NEW.updated_by);
    RETURN NEW;
    
  ELSIF TG_OP = 'UPDATE' THEN
    -- Close current version
    UPDATE products_history SET valid_to = NOW()
    WHERE product_id = OLD.id AND valid_to = '9999-12-31';
    
    -- Insert new version
    INSERT INTO products_history
      (product_id, name, price, status, valid_from, valid_to, change_type, changed_by)
    VALUES
      (NEW.id, NEW.name, NEW.price, NEW.status, NOW(), '9999-12-31', 'UPDATE', NEW.updated_by);
    RETURN NEW;
    
  ELSIF TG_OP = 'DELETE' THEN
    -- Close current version
    UPDATE products_history SET valid_to = NOW(), change_type = 'DELETE'
    WHERE product_id = OLD.id AND valid_to = '9999-12-31';
    RETURN OLD;
  END IF;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_products_history
  AFTER INSERT OR UPDATE OR DELETE ON products
  FOR EACH ROW EXECUTE FUNCTION products_history_trigger();

-- ========================================
-- Queries
-- ========================================

-- 1. Lịch sử toàn bộ thay đổi của product
SELECT * FROM products_history
WHERE product_id = 42
ORDER BY valid_from;

-- 2. Product tại thời điểm cụ thể (time travel!)
SELECT * FROM products_history
WHERE product_id = 42
  AND valid_from <= '2024-06-15 12:00:00'
  AND valid_to > '2024-06-15 12:00:00';

-- 3. Tất cả products tại thời điểm cụ thể (snapshot)
SELECT ph.* FROM products_history ph
WHERE ph.valid_from <= '2024-06-15 12:00:00'
  AND ph.valid_to > '2024-06-15 12:00:00';

-- 4. Price changes of a product (diff)
SELECT
  valid_from,
  price AS new_price,
  LAG(price) OVER (ORDER BY valid_from) AS old_price,
  price - LAG(price) OVER (ORDER BY valid_from) AS price_change
FROM products_history
WHERE product_id = 42 AND change_type != 'DELETE'
ORDER BY valid_from;
```

### 1.3 Pattern 2: Version Column (Same Table)
```sql
-- ========================================
-- Tất cả versions trong CÙNG bảng
-- Tốt cho: Documents, content, configurations
-- ========================================

CREATE TABLE documents (
  id BIGINT NOT NULL,
  version INT NOT NULL,
  
  -- Content
  title VARCHAR(500) NOT NULL,
  content TEXT,
  
  -- Version metadata
  is_current BOOLEAN NOT NULL DEFAULT TRUE,
  created_by BIGINT NOT NULL REFERENCES users(id),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  change_summary TEXT,           -- "Updated pricing section"
  
  PRIMARY KEY (id, version)
);

-- Chỉ 1 current version per document
CREATE UNIQUE INDEX idx_documents_current ON documents(id) WHERE is_current = TRUE;

-- ========================================
-- Operations
-- ========================================

-- Tạo document mới
INSERT INTO documents (id, version, title, content, created_by)
VALUES (1, 1, 'Design Guide', 'Version 1 content...', 123);

-- Tạo version mới (atomic!)
BEGIN;
  -- Đánh dấu version cũ không còn current
  UPDATE documents SET is_current = FALSE
  WHERE id = 1 AND is_current = TRUE;
  
  -- Insert version mới
  INSERT INTO documents (id, version, title, content, is_current, created_by, change_summary)
  VALUES (1, 2, 'Design Guide v2', 'Updated content...', TRUE, 123, 'Major revision');
COMMIT;

-- Lấy current version
SELECT * FROM documents WHERE id = 1 AND is_current = TRUE;

-- Lấy toàn bộ version history
SELECT id, version, title, created_by, created_at, change_summary
FROM documents WHERE id = 1 ORDER BY version;

-- Revert về version cũ
BEGIN;
  UPDATE documents SET is_current = FALSE WHERE id = 1 AND is_current = TRUE;
  
  INSERT INTO documents (id, version, title, content, is_current, created_by, change_summary)
  SELECT id, (SELECT MAX(version) + 1 FROM documents WHERE id = 1),
         title, content, TRUE, 123, 'Reverted to version 3'
  FROM documents WHERE id = 1 AND version = 3;
COMMIT;
```

### 1.4 Pattern 3: System-Versioned Temporal (SQL Server / MariaDB)
```sql
-- ========================================
-- SQL Server: Built-in temporal tables (từ SQL Server 2016)
-- Database TỰ ĐỘNG quản lý history!
-- ========================================

CREATE TABLE employees (
  id INT PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  department VARCHAR(100),
  salary DECIMAL(19,4),
  -- System-versioned columns (DB quản lý tự động)
  valid_from DATETIME2 GENERATED ALWAYS AS ROW START NOT NULL,
  valid_to DATETIME2 GENERATED ALWAYS AS ROW END NOT NULL,
  PERIOD FOR SYSTEM_TIME (valid_from, valid_to)
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.employees_history));

-- ========================================
-- Query syntax đặc biệt — Time Travel!
-- ========================================

-- Employee info tại thời điểm cụ thể
SELECT * FROM employees
FOR SYSTEM_TIME AS OF '2024-01-15 10:00:00'
WHERE id = 123;

-- Toàn bộ lịch sử
SELECT * FROM employees
FOR SYSTEM_TIME ALL
WHERE id = 123
ORDER BY valid_from;

-- Trong khoảng thời gian
SELECT * FROM employees
FOR SYSTEM_TIME BETWEEN '2024-01-01' AND '2024-06-30'
WHERE department = 'Engineering';

-- ========================================
-- PostgreSQL: Chưa có built-in, dùng extension
-- ========================================
-- Option 1: temporal_tables extension
-- Option 2: Manual history tables (Pattern 1 ở trên)
-- Option 3: pg_audit extension cho audit trail
```

### 1.5 Khi nào dùng Pattern nào?
```
Pattern             │ Khi nào dùng                      │ Ưu/Nhược
════════════════════╪═══════════════════════════════════╪══════════════════════
History Table       │ Most common. Audit trail cho      │ ✅ Flexible, works everywhere
(separate table)    │ mọi loại entity                   │ ⚠️ Tự quản lý trigger
                    │                                    │
Version Column      │ Documents, content, configs       │ ✅ Dễ query versions
(same table)        │ cần revert, compare versions      │ ⚠️ Table grow nhanh
                    │                                    │
System-Versioned    │ SQL Server / MariaDB              │ ✅ Zero code, built-in
(built-in)          │ Strong compliance requirements     │ ❌ Vendor-specific
                    │                                    │
Event Sourcing      │ Complex domains (finance, CQRS)   │ ✅ Complete audit trail
(application-level) │ Cần rebuild state từ events       │ ⚠️ Complex architecture
```

---

## 2. Slowly Changing Dimensions (SCD)

### Dùng cho Data Warehouse / Analytics
```
Khi dimension data thay đổi (customer address, product category)
→ Cách lưu phụ thuộc vào cần đi lại trong thời gian không?

Type 0: RETAIN original   — Không bao giờ thay đổi (fixed attributes)
Type 1: OVERWRITE          — Ghi đè, mất lịch sử
Type 2: ADD ROW            — Thêm row mới (phổ biến nhất, giữ lịch sử)
Type 3: ADD COLUMN         — Thêm cột "previous_value" (ít dùng)
Type 4: HISTORY TABLE      — Giống temporal tables
Type 6: HYBRID 1+2+3       — Kết hợp
```

### SCD Type 2 (Khuyến nghị cho Data Warehouse)
```sql
CREATE TABLE dim_customers (
  -- Surrogate key (auto-increment) — NOT the customer_id
  sk BIGSERIAL PRIMARY KEY,
  
  -- Natural/business key (links to source system)
  customer_id BIGINT NOT NULL,
  
  -- Dimension attributes
  name VARCHAR(255) NOT NULL,
  email VARCHAR(254),
  city VARCHAR(100),
  membership_tier VARCHAR(50),
  
  -- SCD Type 2 tracking columns
  effective_date DATE NOT NULL,
  expiration_date DATE NOT NULL DEFAULT '9999-12-31',
  is_current BOOLEAN NOT NULL DEFAULT TRUE,
  
  -- ETL metadata
  source_system VARCHAR(50) NOT NULL DEFAULT 'app_db',
  etl_loaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes
CREATE INDEX idx_dim_customers_current ON dim_customers(customer_id) WHERE is_current = TRUE;
CREATE INDEX idx_dim_customers_range ON dim_customers(customer_id, effective_date, expiration_date);

-- ========================================
-- ETL Process: Khi customer data thay đổi
-- ========================================

-- Step 1: Expire current record
UPDATE dim_customers
SET expiration_date = CURRENT_DATE - 1, is_current = FALSE
WHERE customer_id = 100 AND is_current = TRUE;

-- Step 2: Insert new current record
INSERT INTO dim_customers 
  (customer_id, name, email, city, membership_tier, effective_date)
VALUES 
  (100, 'Nguyen Van A', 'a@example.com', 'HCMC', 'Gold', CURRENT_DATE);

-- ========================================
-- Query: Fact table joins
-- ========================================

-- Current customer info
SELECT f.*, d.*
FROM fact_orders f
JOIN dim_customers d ON d.customer_id = f.customer_id AND d.is_current = TRUE;

-- Customer info AT TIME OF ORDER (correct!)
SELECT f.*, d.*
FROM fact_orders f
JOIN dim_customers d ON d.customer_id = f.customer_id
  AND f.order_date BETWEEN d.effective_date AND d.expiration_date;
-- ✅ Nếu customer upgrade từ Silver → Gold vào 15/6
--    Order ngày 14/6 → join với Silver record
--    Order ngày 16/6 → join với Gold record
```

---

## 3. Schema Evolution & Change Management

### 3.1 Chiến lược tổng thể
```
✅ LUÔN:
  - Lưu migration files trong version control (Git)
  - Code review cho schema changes giống như application code
  - Test migrations trên staging/dev trước production
  - Có rollback plan cho MỖI migration
  - Backward-compatible changes khi possible
  - Document breaking changes

❌ KHÔNG BAO GIỜ:
  - Chạy ALTER TABLE trực tiếp trên production (ad-hoc)
  - Xóa column/table NGAY LẬP TỨC mà không deprecation period
  - Thay đổi column type trực tiếp (lock table!)
  - Deploy application code VÀ schema change cùng lúc
```

### 3.2 Expand-Contract Pattern (Zero-Downtime Migration)
```sql
-- ========================================
-- Khi cần: rename column, change type, split table
-- Mà KHÔNG downtime, KHÔNG break existing code
-- ========================================

-- Ví dụ: Rename column 'name' → 'full_name'

-- ════════════════════════════════════
-- PHASE 1: EXPAND (Deploy 1)
-- Thêm cột mới, viết vào CẢ HAI cột
-- ════════════════════════════════════

-- Migration 1: Add new column
ALTER TABLE users ADD COLUMN full_name VARCHAR(255);

-- Migration 2: Backfill existing data
UPDATE users SET full_name = name WHERE full_name IS NULL;

-- Deploy app version 1.1:
--   Write: SET name = ?, full_name = ?  (dual-write)
--   Read: SELECT full_name FROM users   (read new)

-- ════════════════════════════════════
-- PHASE 2: MIGRATE (Deploy 2)
-- Application chỉ đọc từ cột mới
-- ════════════════════════════════════

-- Verify: 100% rows have full_name populated
SELECT COUNT(*) FROM users WHERE full_name IS NULL;  -- Should be 0

-- Add NOT NULL constraint
ALTER TABLE users ALTER COLUMN full_name SET NOT NULL;

-- Deploy app version 1.2:
--   Write: SET full_name = ?  (single-write)
--   Read: SELECT full_name FROM users

-- ════════════════════════════════════
-- PHASE 3: CONTRACT (Deploy 3, vài ngày/tuần sau)
-- Xóa cột cũ, dọn dẹp
-- ════════════════════════════════════

-- Chắc chắn không còn code nào dùng 'name'
ALTER TABLE users DROP COLUMN name;

-- ✅ Zero downtime throughout entire process!
-- ✅ Rollback possible at any phase
```

### 3.3 Safe Migration Patterns
```sql
-- ════════════════════════════════════
-- Thêm cột mới (SAFE)
-- ════════════════════════════════════
-- ✅ Thêm nullable → OK, instant
ALTER TABLE users ADD COLUMN phone VARCHAR(20);

-- ✅ Thêm với DEFAULT (PostgreSQL 11+: instant, không rewrite table!)
ALTER TABLE users ADD COLUMN is_premium BOOLEAN NOT NULL DEFAULT FALSE;

-- ⚠️ MySQL < 8.0: ADD COLUMN locks table → cần pt-online-schema-change
-- ⚠️ PostgreSQL < 11: DEFAULT causes table rewrite → slow trên big tables

-- ════════════════════════════════════
-- Index creation (CRITICAL!)
-- ════════════════════════════════════
-- ❌ SAI: Lock table while creating index
CREATE INDEX idx_users_phone ON users(phone);
-- Trên bảng 10M rows → lock vài phút → downtime!

-- ✅ ĐÚNG: CONCURRENTLY (PostgreSQL)
CREATE INDEX CONCURRENTLY idx_users_phone ON users(phone);
-- Không lock table! Background process. Chậm hơn nhưng NO downtime.
-- ⚠️ CONCURRENTLY không thể chạy trong transaction
-- ⚠️ Nếu fail giữa chừng → index INVALID → phải DROP và tạo lại

-- ════════════════════════════════════
-- Thay đổi column type (DANGEROUS!)
-- ════════════════════════════════════
-- ❌ NGUY HIỂM: ALTER COLUMN TYPE
ALTER TABLE orders ALTER COLUMN total_amount TYPE DECIMAL(19,4);
-- → REWRITE TOÀN BỘ TABLE! Lock table! Trên 100M rows → hours!

-- ✅ SAFE: Expand-Contract pattern
-- 1. Add new column với type mới
ALTER TABLE orders ADD COLUMN total_amount_new DECIMAL(19,4);
-- 2. Dual-write + backfill
-- 3. Switch reads
-- 4. Drop old column

-- ════════════════════════════════════
-- Drop column (SAFE nhưng cần thận trọng)
-- ════════════════════════════════════
-- 1. Đảm bảo KHÔNG CÒN code nào dùng column này
-- 2. Monitor 1-2 tuần → no errors
-- 3. Drop
ALTER TABLE users DROP COLUMN old_column;
-- PostgreSQL: instant (chỉ mark column as dropped)
-- MySQL: có thể slow trên big tables
```

### 3.4 Migration Tools
```
Tool            │ Language     │ Format          │ Đặc điểm
════════════════╪══════════════╪═════════════════╪═══════════════════
Flyway          │ Java/any     │ SQL files       │ Simple, enterprise, versioned
Liquibase       │ Java/any     │ XML/YAML/SQL    │ Rollback tốt, diff support
Alembic         │ Python       │ Python scripts  │ SQLAlchemy integration, auto-gen
Prisma Migrate  │ TypeScript   │ SQL files       │ Type-safe schema, auto-gen
Drizzle Kit     │ TypeScript   │ TypeScript      │ Lightweight, drizzle-orm
golang-migrate  │ Go           │ SQL files       │ CLI-based, framework-agnostic
dbmate          │ Go           │ SQL files       │ Zero-dependency, simple
Rails Migrations│ Ruby         │ Ruby DSL        │ ActiveRecord, mature
Django Migrations│Python       │ Python          │ Auto-generated, battle-tested
```

### 3.5 Migration File Convention
```
# Naming: V{timestamp}_{description}.sql
V20240115120000_create_users_table.sql
V20240116090000_add_phone_to_users.sql
V20240117150000_create_orders_table.sql
V20240120100000_add_index_orders_user_id.sql
V20240125140000_rename_name_to_full_name_phase1.sql
V20240127100000_rename_name_to_full_name_phase2.sql

# Hoặc sequential
V001_create_users.sql
V002_create_products.sql
V003_add_orders.sql

# Mỗi file có UP và DOWN
-- UP
CREATE TABLE users (...);

-- DOWN (rollback)
DROP TABLE IF EXISTS users;
```

---

## 4. Backup & Disaster Recovery

### 4.1 Backup Types
```
Type             │ Mô tả                           │ RPO*         │ Recovery Speed
═════════════════╪══════════════════════════════════╪══════════════╪══════════════
Full Logical     │ pg_dump / mysqldump              │ At dump time │ Slow (import)
  (pg_dump)      │ Toàn bộ schema + data            │              │
─────────────────┼──────────────────────────────────┼──────────────┼──────────────
Full Physical    │ pg_basebackup / rsync            │ At backup    │ Fast (copy)
  (file-level)   │ Copy physical files              │ time         │
─────────────────┼──────────────────────────────────┼──────────────┼──────────────
Incremental      │ Chỉ thay đổi từ backup cuối     │ Near         │ Medium
  (WAL archive)  │ WAL (Write-Ahead Log) segments   │ real-time    │
─────────────────┼──────────────────────────────────┼──────────────┼──────────────
Continuous       │ WAL streaming to standby         │ Seconds      │ Very fast
  (replication)  │ Real-time replication            │              │ (failover)
─────────────────┼──────────────────────────────────┼──────────────┼──────────────
Cloud Snapshot   │ EBS snapshot, Azure backup       │ At snapshot  │ Fast
                 │ Provider-managed                 │ time         │

* RPO = Recovery Point Objective (bao nhiêu data TỐI ĐA có thể mất)
* RTO = Recovery Time Objective (bao lâu để recover)
```

### 4.2 Quy tắc 3-2-1 Backup
```
3 — Ít nhất 3 bản copy data
    (1 primary + 2 backups)
2 — Trên 2 loại storage khác nhau
    (local disk + cloud storage)
1 — Ít nhất 1 bản ở offsite
    (khác data center / khác region / cloud)

Ví dụ thực tế:
Copy 1: Production PostgreSQL server (primary)
Copy 2: Daily pg_dump → local NAS (same building)
Copy 3: WAL archiving → AWS S3 cross-region (offsite)
```

### 4.3 PostgreSQL Backup Commands
```bash
# ════════════════════════════════════
# Logical Backup (pg_dump)
# ════════════════════════════════════

# Full backup (custom format — recommended)
pg_dump -h localhost -U postgres -Fc mydb > backup_$(date +%Y%m%d_%H%M%S).dump

# Full backup (SQL format — human readable)
pg_dump -h localhost -U postgres --no-owner --no-acl mydb > backup.sql

# Backup specific tables
pg_dump -h localhost -U postgres -Fc -t users -t orders mydb > partial_backup.dump

# Schema only (no data)
pg_dump -h localhost -U postgres --schema-only mydb > schema.sql

# Data only (no schema)
pg_dump -h localhost -U postgres --data-only mydb > data.sql

# ════════════════════════════════════
# Restore
# ════════════════════════════════════

# From custom format
pg_restore -h localhost -U postgres -d mydb --no-owner backup.dump

# From SQL format
psql -h localhost -U postgres -d mydb < backup.sql

# ════════════════════════════════════
# Physical Backup (pg_basebackup)
# ════════════════════════════════════

# Full physical backup (for setting up replicas or PITR)
pg_basebackup -h primary_host -U replication -D /backup/base -Fp -Xs -P

# ════════════════════════════════════
# Continuous WAL Archiving (PITR capability)
# ════════════════════════════════════

# postgresql.conf
# wal_level = replica
# archive_mode = on
# archive_command = 'aws s3 cp %p s3://my-wal-bucket/%f'

# Recovery: PITR (point-in-time recovery)
# recovery.conf (hoặc postgresql.conf cho PG 12+)
# restore_command = 'aws s3 cp s3://my-wal-bucket/%f %p'
# recovery_target_time = '2024-01-15 14:30:00'
# → Database recover đến CHÍNH XÁC thời điểm đó!
```

### 4.4 Backup Automation & Monitoring
```bash
#!/bin/bash
# Daily backup script (cron: 0 2 * * * /scripts/backup.sh)

DB_NAME="myapp"
BACKUP_DIR="/backup"
S3_BUCKET="s3://myapp-backups"
RETENTION_DAYS=30

# Create backup
FILENAME="backup_${DB_NAME}_$(date +%Y%m%d_%H%M%S).dump"
pg_dump -Fc -h localhost -U backup_user "${DB_NAME}" > "${BACKUP_DIR}/${FILENAME}"

# Check backup success
if [ $? -eq 0 ]; then
  # Upload to S3
  aws s3 cp "${BACKUP_DIR}/${FILENAME}" "${S3_BUCKET}/${FILENAME}" --sse AES256
  
  # Cleanup old local backups
  find "${BACKUP_DIR}" -name "backup_*.dump" -mtime +${RETENTION_DAYS} -delete
  
  echo "SUCCESS: Backup ${FILENAME} completed"
else
  echo "FAILED: Backup ${DB_NAME} failed!" | mail -s "ALERT: DB Backup Failed" admin@example.com
  exit 1
fi
```

### 4.5 Backup Checklist
```
═══════════════════════════════════════════════════════════
  BACKUP & DR CHECKLIST
═══════════════════════════════════════════════════════════

Backup:
  - [ ] Backup tự động chạy hàng ngày (hoặc thường xuyên hơn)?
  - [ ] Backup files đã encrypt (at rest)?
  - [ ] Đã tuân thủ quy tắc 3-2-1?
  - [ ] Có retention policy? (giữ 30/60/90 ngày?)
  - [ ] Monitor backup failures? (alert khi backup fail?)
  - [ ] WAL archiving enabled cho PITR?

Testing:
  - [ ] Đã test restore procedure? (ít nhất mỗi quý!)
  - [ ] Thời gian restore đã đo và acceptable (RTO)?
  - [ ] Restored data verified integrity?

DR Planning:
  - [ ] RPO đã define? (chấp nhận mất max bao nhiêu data?)
  - [ ] RTO đã define? (max bao lâu để recover?)
  - [ ] Có documented DR plan?
  - [ ] DR plan đã test ít nhất 1 lần/năm?
  - [ ] Team biết process khi xảy ra incident?
```

---

## 5. High Availability & Replication

### 5.1 Kiến trúc HA
```
                    ┌──────────────────────┐
                    │    Load Balancer     │
                    │ (HAProxy/PgBouncer)  │
                    │  RW → Primary        │
                    │  RO → Replicas       │
                    └────┬──────────┬──────┘
                         │          │
                    ┌────▼───┐ ┌───▼──────┐
                    │Primary │ │ Replica 1 │ (read-only, same AZ)
                    │ (R+W)  │ │ (RO)      │
                    └────┬───┘ └──────────┘
                         │
              ┌──────────┼──────────┐
              │                     │
         ┌────▼────┐          ┌────▼────┐
         │Replica 2│          │Replica 3│
         │(RO, AZ2)│          │(RO, DR) │ ← Different region!
         └─────────┘          └─────────┘

    Primary: Handles ALL writes + reads
    Replicas: Handle read-only queries (SELECT)
    AZ = Availability Zone
    DR = Disaster Recovery
```

### 5.2 Replication Types chi tiết
```
════════════════════════════════════════════════════════════════════
Type                │ Mô tả                    │ Data Loss │ Latency
════════════════════════════════════════════════════════════════════
Synchronous         │ Primary CHỜI replica     │ ❌ Zero   │ ⚠️ Higher
Replication         │ confirm TRƯỚC khi commit │ data loss │ (network RTT)
────────────────────┼──────────────────────────┼───────────┼─────────
Asynchronous        │ Primary commit ngay,     │ ⚠️ May    │ ✅ Lower
Replication         │ replica bắt kịp SAU      │ lose secs │
────────────────────┼──────────────────────────┼───────────┼─────────
Logical             │ Replicate specific       │ Varies    │ Varies
Replication         │ tables/databases         │           │
════════════════════════════════════════════════════════════════════

Chọn sync khi: Zero data loss critical (finance, healthcare)
Chọn async khi: Performance > consistency (analytics, content)
Chọn logical khi: Selective replication, cross-version, cross-platform
```

### 5.3 PostgreSQL Streaming Replication
```bash
# ════════════════════════════════════
# Primary Server Configuration
# ════════════════════════════════════

# postgresql.conf
wal_level = replica                    # Required for replication
max_wal_senders = 5                    # Max number of replicas
wal_keep_size = 1GB                    # WAL retention
synchronous_standby_names = ''         # '' = async, 'replica1' = sync

# pg_hba.conf (allow replication connection)
host replication replication_user 10.0.0.0/24 md5

# Create replication user
CREATE ROLE replication_user WITH REPLICATION LOGIN PASSWORD 'strong_pass';

# ════════════════════════════════════
# Replica Server Setup
# ════════════════════════════════════

# Take base backup from primary
pg_basebackup -h primary_host -U replication_user \
  -D /var/lib/postgresql/16/data -Fp -Xs -P -R
# -R flag auto-creates standby.signal and recovery config

# postgresql.conf on replica
hot_standby = on                       # Allow read queries on replica
primary_conninfo = 'host=primary_host port=5432 user=replication_user password=xxx'
```

### 5.4 Application-Level Read/Write Splitting
```python
# Route queries to appropriate server
# WRITE queries → Primary
# READ queries → Replica(s)

# Python SQLAlchemy example
from sqlalchemy import create_engine

primary = create_engine("postgresql://user:pass@primary:5432/db")
replica = create_engine("postgresql://user:pass@replica:5432/db")

def get_engine(readonly=False):
    return replica if readonly else primary

# Usage:
# Order creation → primary
with get_engine(readonly=False).connect() as conn:
    conn.execute("INSERT INTO orders ...")

# Dashboard queries → replica
with get_engine(readonly=True).connect() as conn:
    result = conn.execute("SELECT * FROM orders WHERE ...")
```

### 5.5 Uptime SLA Guide
```
SLA      │ Downtime/năm │ Architecture cần thiết
═════════╪══════════════╪══════════════════════════════════
99%      │ 3.65 ngày    │ Single server + monitoring + backup
99.9%    │ 8.76 giờ     │ + Auto failover + 1 replica
99.95%   │ 4.38 giờ     │ + Multi-AZ replicas
99.99%   │ 52.6 phút    │ + Sync replication + auto failover
99.999%  │ 5.26 phút    │ + Multi-region + complex orchestration
```

### 5.6 CAP Theorem
```
Trong DISTRIBUTED system, chỉ chọn được 2 trong 3:

    ┌─────────────────────┐
    │    Consistency (C)   │
    │  Mọi node trả cùng  │
    │  data tại mọi thời  │
    │  điểm                │
    └─────────┬───────────┘
              │
    ┌─────────┴───────────┐
    │                     │
    ▼                     ▼
┌──────────┐      ┌──────────────┐
│Avail.(A) │      │Partition (P) │
│Mọi req   │      │Hệ thống work│
│đều nhận  │      │dù network    │
│response  │      │partition     │
└──────────┘      └──────────────┘

   CP: Consistency + Partition tolerance
       → PostgreSQL sync replica, MongoDB (w:majority)
       → Sacrifice: availability (reject writes if partition)

   AP: Availability + Partition tolerance
       → Cassandra, DynamoDB, PostgreSQL async replica
       → Sacrifice: consistency (eventual consistency)

   CA: Consistency + Availability
       → Single-node PostgreSQL (no distribution)
       → Sacrifice: partition tolerance (single point of failure)

🎯 Trong thực tế: P là bắt buộc (network LUÔN có thể fail)
   → Thực chất chọn giữa CP và AP
   → Financial: chọn CP (consistency > availability)
   → Social/Content: chọn AP (availability > consistency)
```

---

## 6. Data Governance

### 6.1 Data Classification trong Schema
```sql
-- Dùng COMMENT để classify data NGAY TRONG schema
-- → Documentation sống cùng database, không bị outdated

COMMENT ON TABLE users IS 
  'Core user accounts. Contains CONFIDENTIAL PII data.
   Owner: Identity Team. Retention: Account lifetime + 30 days.';

COMMENT ON COLUMN users.email IS 
  'Classification: CONFIDENTIAL. PII. Encrypted lookup via email_hash.';

COMMENT ON COLUMN users.role IS 
  'Classification: INTERNAL. Access control field.';

COMMENT ON TABLE audit_logs IS 
  'Immutable audit trail. Classification: INTERNAL.
   Owner: Security Team. Retention: 7 years (compliance).
   NOTE: UPDATE and DELETE are PROHIBITED on this table.';

COMMENT ON TABLE posts IS 
  'User-generated content. Classification: PUBLIC.
   Owner: Content Team. Retention: Indefinite.';
```

### 6.2 Retention Policy Management
```sql
CREATE TABLE data_retention_policies (
  id SERIAL PRIMARY KEY,
  schema_name VARCHAR(100) NOT NULL DEFAULT 'public',
  table_name VARCHAR(255) NOT NULL,
  retention_days INT NOT NULL,
  action VARCHAR(50) NOT NULL DEFAULT 'delete'
    CHECK (action IN ('delete', 'anonymize', 'archive', 'truncate')),
  condition_column VARCHAR(100) NOT NULL DEFAULT 'created_at',
  extra_where TEXT,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  last_run_at TIMESTAMPTZ,
  last_run_rows_affected BIGINT,
  description TEXT,
  
  UNIQUE (schema_name, table_name)
);

-- Standard policies
INSERT INTO data_retention_policies 
  (table_name, retention_days, action, description) VALUES
('user_sessions', 90, 'delete', 'Remove expired sessions after 90 days'),
('password_resets', 7, 'delete', 'Remove consumed/expired reset tokens'),
('email_verifications', 30, 'delete', 'Remove consumed/expired verification tokens'),
('audit_logs', 2555, 'archive', 'Archive audit logs after 7 years (compliance)'),
('analytics_events', 730, 'anonymize', 'Anonymize analytics after 2 years'),
('notifications', 180, 'delete', 'Remove read notifications after 6 months'),
('job_queue', 30, 'delete', 'Remove completed/dead jobs after 30 days');

-- Cleanup job (run nightly)
-- For each active policy:
--   IF action = 'delete':
--     DELETE FROM {table} WHERE {condition_column} < NOW() - INTERVAL '{retention_days} days'
--     AND {extra_where if any}
--   Log rows affected
```

### 6.3 Data Lifecycle Questions (Khi thu thập yêu cầu)
```
Hỏi user/stakeholder TRƯỚC KHI thiết kế:

1. DATA RETENTION
   - Dữ liệu này lưu bao lâu? (30 ngày? 1 năm? 7 năm? forever?)
   - Sau khi hết hạn: xóa hẳn, anonymize, hay archive?
   - Có compliance requirement nào? (GDPR 30-day erasure, financial 7-year retention)

2. DATA CLASSIFICATION
   - Có chứa PII không? (email, phone, address, SSN)
   - Data sensitivity level? (Public/Internal/Confidential/Restricted)
   - Cần encryption không? (at rest, column-level)

3. DATA OWNERSHIP
   - Ai own data này? (team nào responsible?)
   - Ai có quyền đọc? Ai có quyền sửa/xóa?
   - Có cần audit trail cho mọi thay đổi không?

4. AVAILABILITY
   - Uptime SLA bao nhiêu? (99%? 99.9%? 99.99%?)
   - RPO: Chấp nhận mất tối đa bao nhiêu data? (5 phút? 1 giờ?)
   - RTO: Chấp nhận downtime tối đa bao lâu? (10 phút? 1 giờ?)

5. GROWTH & SCALE
   - Data growth rate? (bao nhiêu rows/ngày? tháng? năm?)
   - Peak concurrent users? (100? 1000? 100,000?)
   - Lượng data dự kiến sau 1 năm? 3 năm?

6. INTEGRATION
   - Hệ thống nào cần đọc data này? (reporting, analytics, ML)
   - Format nào? (real-time API, batch ETL, CDC stream)
   - Cross-system consistency requirements?
```

---

## 7. Database Testing

### 7.1 Test Categories
```
Category           │ Test cái gì                    │ Khi nào chạy
═══════════════════╪════════════════════════════════╪═══════════════
Migration Tests    │ Up/Down migrations work        │ Before deploy
Constraint Tests   │ CHECK, FK, UNIQUE work         │ CI/CD pipeline
Data Integrity     │ Business rules enforced by DB  │ CI/CD pipeline
Performance Tests  │ Query performance acceptable   │ Before release
Backup/Restore     │ Backups can be restored        │ Monthly
Security Tests     │ Permissions, RLS work correctly │ Before deploy
```

### 7.2 Migration Testing
```bash
# ════════════════════════════════════
# Test: Migration UP succeeds
# ════════════════════════════════════
# 1. Apply all migrations to empty database
migrate -path ./migrations -database "postgres://..." up
# 2. Verify schema matches expected
pg_dump --schema-only mydb > actual_schema.sql
diff expected_schema.sql actual_schema.sql

# ════════════════════════════════════
# Test: Migration DOWN (rollback) works
# ════════════════════════════════════
# 1. Apply UP
migrate up
# 2. Apply DOWN
migrate down 1
# 3. Apply UP again (should work without errors)
migrate up
# → If fail: migration is NOT idempotent → fix!
```

### 7.3 Constraint Testing
```sql
-- ========================================
-- Test: CHECK constraints reject bad data
-- ========================================

-- Should FAIL: negative price
INSERT INTO products (name, price) VALUES ('Test', -10);
-- Expected: ERROR: violates check constraint "products_price_check"

-- Should FAIL: invalid status
INSERT INTO orders (user_id, status) VALUES (1, 'invalid_status');
-- Expected: ERROR: violates check constraint

-- Should FAIL: NULL required field
INSERT INTO users (email) VALUES (NULL);
-- Expected: ERROR: null value in column "email" violates not-null constraint

-- ========================================
-- Test: FK constraints prevent orphans
-- ========================================

-- Should FAIL: FK to non-existent user
INSERT INTO orders (user_id, total_amount) VALUES (999999, 100);
-- Expected: ERROR: violates foreign key constraint

-- Should CASCADE: Delete user → delete orders
DELETE FROM users WHERE id = 1;
-- Verify: SELECT COUNT(*) FROM orders WHERE user_id = 1;  → 0

-- ========================================
-- Test: UNIQUE constraints prevent duplicates
-- ========================================

INSERT INTO users (email) VALUES ('test@example.com');
INSERT INTO users (email) VALUES ('test@example.com');
-- Expected: ERROR: duplicate key value violates unique constraint
```

### 7.4 Performance Testing
```sql
-- ========================================
-- Benchmark queries on realistic data volume
-- ========================================

-- 1. Seed test data (realistic volume)
-- Use: faker, pg_generate_series, or production-like seed scripts

-- 2. Run EXPLAIN ANALYZE on critical queries
EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
SELECT o.id, o.total_amount, u.email
FROM orders o
JOIN users u ON u.id = o.user_id
WHERE o.status = 'pending'
  AND o.created_at > NOW() - INTERVAL '7 days'
ORDER BY o.created_at DESC
LIMIT 50;

-- Check:
-- ✅ Seq Scan → Cần Index?
-- ✅ Execution time < threshold (e.g., < 100ms)?
-- ✅ Rows estimate accurate? (nếu sai → ANALYZE table)
-- ✅ Buffers hit ratio > 95%? (good cache usage)
```
