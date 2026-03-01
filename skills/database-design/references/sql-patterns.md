# SQL Patterns & Migration Scripts

## Table of Contents
1. [DDL Templates Chuẩn](#ddl-templates)
2. [Common Column Patterns](#column-patterns)
3. [Soft Delete Pattern](#soft-delete)
4. [Audit Log Pattern](#audit-log)
5. [Migration Best Practices](#migration)
6. [PostgreSQL vs MySQL Differences](#pg-vs-mysql)

---

## 1. DDL Templates Chuẩn

### Base Table Template (PostgreSQL)
```sql
CREATE TABLE table_name (
  -- Primary Key
  id BIGSERIAL PRIMARY KEY,
  
  -- Business columns
  -- ...
  
  -- Audit timestamps (luôn có)
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Auto-update updated_at trigger
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_table_name_updated_at
  BEFORE UPDATE ON table_name
  FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
```

### Base Table Template (MySQL)
```sql
CREATE TABLE table_name (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  
  -- Business columns
  -- ...
  
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### Users Table — Chuẩn Nhất
```sql
-- PostgreSQL
CREATE TABLE users (
  id BIGSERIAL PRIMARY KEY,
  
  -- Authentication
  email VARCHAR(254) UNIQUE NOT NULL,
  email_verified_at TIMESTAMPTZ,
  password_hash VARCHAR(255),  -- NULL nếu OAuth only
  
  -- OAuth (optional)
  google_id VARCHAR(255) UNIQUE,
  github_id VARCHAR(255) UNIQUE,
  
  -- Profile
  username VARCHAR(50) UNIQUE,
  display_name VARCHAR(255),
  avatar_url TEXT,
  
  -- Status & Role
  role VARCHAR(50) NOT NULL DEFAULT 'user'
    CHECK (role IN ('user', 'admin', 'moderator')),
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  
  -- Security
  last_login_at TIMESTAMPTZ,
  failed_login_attempts INT NOT NULL DEFAULT 0,
  locked_until TIMESTAMPTZ,
  
  -- Soft delete
  deleted_at TIMESTAMPTZ,
  
  -- Timestamps
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_users_email ON users(email) WHERE deleted_at IS NULL;
CREATE INDEX idx_users_username ON users(username) WHERE deleted_at IS NULL;
CREATE INDEX idx_users_role ON users(role) WHERE is_active = TRUE;
```

---

## 2. Common Column Patterns

### Status Column Pattern
```sql
-- Option 1: VARCHAR với CHECK constraint (khuyến nghị cho PostgreSQL)
status VARCHAR(50) NOT NULL DEFAULT 'pending'
  CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'cancelled')),

-- Option 2: MySQL ENUM (không nên — khó alter)
-- status ENUM('pending', 'processing', 'completed') NOT NULL DEFAULT 'pending',

-- Option 3: Separate status table (khi status có thêm metadata)
CREATE TABLE order_statuses (
  code VARCHAR(50) PRIMARY KEY,
  label VARCHAR(255) NOT NULL,
  description TEXT,
  is_terminal BOOLEAN NOT NULL DEFAULT FALSE  -- 'completed', 'cancelled' là terminal
);
```

### Slug / SEO URL Pattern
```sql
slug VARCHAR(255) UNIQUE NOT NULL,
-- Luôn lowercase, chỉ a-z, 0-9, dấu gạch ngang
-- Ví dụ: 'my-great-blog-post-2024'

-- Index
CREATE INDEX idx_table_slug ON table_name(slug);
```

### Money / Price Pattern
```sql
-- Luôn dùng DECIMAL, KHÔNG dùng FLOAT
price DECIMAL(19, 4) NOT NULL DEFAULT 0.0000,
-- 19 digits total, 4 decimal places
-- Max value: 999,999,999,999,999.9999

-- Currency code nếu multi-currency
currency_code CHAR(3) NOT NULL DEFAULT 'VND',
  CHECK (currency_code ~ '^[A-Z]{3}$'),
-- ISO 4217: VND, USD, EUR, ...

-- Hoặc lưu theo cents/smallest unit (tránh decimal hoàn toàn)
price_cents BIGINT NOT NULL DEFAULT 0,
-- 100000 = 1000.00 VND
```

### Address Pattern
```sql
-- Embedded trong bảng (cho 1:1)
address_line1 VARCHAR(255),
address_line2 VARCHAR(255),
city VARCHAR(100),
state_province VARCHAR(100),
postal_code VARCHAR(20),
country_code CHAR(2),  -- ISO 3166-1 alpha-2: VN, US, GB

-- Hoặc bảng riêng (cho 1:many addresses)
CREATE TABLE addresses (
  id BIGSERIAL PRIMARY KEY,
  user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  type VARCHAR(20) NOT NULL DEFAULT 'shipping'
    CHECK (type IN ('shipping', 'billing', 'home', 'work')),
  is_default BOOLEAN NOT NULL DEFAULT FALSE,
  recipient_name VARCHAR(255) NOT NULL,
  phone VARCHAR(20),
  address_line1 VARCHAR(255) NOT NULL,
  address_line2 VARCHAR(255),
  city VARCHAR(100) NOT NULL,
  state_province VARCHAR(100),
  postal_code VARCHAR(20),
  country_code CHAR(2) NOT NULL DEFAULT 'VN',
  -- Geolocation (optional)
  latitude DECIMAL(10, 8),
  longitude DECIMAL(11, 8),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

## 3. Soft Delete Pattern

```sql
-- Thêm cột deleted_at
ALTER TABLE users ADD COLUMN deleted_at TIMESTAMPTZ;

-- "Xóa" record
UPDATE users SET deleted_at = NOW() WHERE id = 123;

-- Query chỉ lấy active records
SELECT * FROM users WHERE deleted_at IS NULL;

-- Partial index — chỉ index active records (tiết kiệm storage, nhanh hơn)
CREATE UNIQUE INDEX idx_users_email_active
  ON users(email) WHERE deleted_at IS NULL;

-- View để tiện query
CREATE VIEW active_users AS
  SELECT * FROM users WHERE deleted_at IS NULL;

-- Soft delete với paranoid check
-- Nếu cần lấy tất cả kể cả đã xóa:
SELECT * FROM users;
-- Nếu chỉ cần active:
SELECT * FROM users WHERE deleted_at IS NULL;
-- Nếu chỉ cần deleted:
SELECT * FROM users WHERE deleted_at IS NOT NULL;
```

---

## 4. Audit Log Pattern

### Option 1: Trigger-based Audit (PostgreSQL)
```sql
-- Bảng audit log
CREATE TABLE audit_logs (
  id BIGSERIAL PRIMARY KEY,
  table_name VARCHAR(255) NOT NULL,
  record_id BIGINT NOT NULL,
  action VARCHAR(10) NOT NULL CHECK (action IN ('INSERT', 'UPDATE', 'DELETE')),
  old_values JSONB,
  new_values JSONB,
  changed_by BIGINT REFERENCES users(id) ON DELETE SET NULL,
  changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  ip_address INET,
  user_agent TEXT
);

CREATE INDEX idx_audit_logs_table_record ON audit_logs(table_name, record_id);
CREATE INDEX idx_audit_logs_changed_at ON audit_logs(changed_at DESC);
CREATE INDEX idx_audit_logs_changed_by ON audit_logs(changed_by);

-- Trigger function
CREATE OR REPLACE FUNCTION audit_trigger_func()
RETURNS TRIGGER AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    INSERT INTO audit_logs(table_name, record_id, action, old_values)
    VALUES (TG_TABLE_NAME, OLD.id, 'DELETE', to_jsonb(OLD));
  ELSIF TG_OP = 'UPDATE' THEN
    INSERT INTO audit_logs(table_name, record_id, action, old_values, new_values)
    VALUES (TG_TABLE_NAME, NEW.id, 'UPDATE', to_jsonb(OLD), to_jsonb(NEW));
  ELSIF TG_OP = 'INSERT' THEN
    INSERT INTO audit_logs(table_name, record_id, action, new_values)
    VALUES (TG_TABLE_NAME, NEW.id, 'INSERT', to_jsonb(NEW));
  END IF;
  RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Apply trigger to a table
CREATE TRIGGER audit_users
  AFTER INSERT OR UPDATE OR DELETE ON users
  FOR EACH ROW EXECUTE FUNCTION audit_trigger_func();
```

### Option 2: Event Sourcing (Application-level)
```sql
-- Lưu toàn bộ lịch sử thay đổi như events
CREATE TABLE domain_events (
  id BIGSERIAL PRIMARY KEY,
  aggregate_type VARCHAR(100) NOT NULL,  -- 'User', 'Order'
  aggregate_id BIGINT NOT NULL,
  event_type VARCHAR(100) NOT NULL,      -- 'UserRegistered', 'OrderPlaced'
  event_data JSONB NOT NULL,
  metadata JSONB,                        -- IP, user agent, correlation_id
  occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  created_by BIGINT REFERENCES users(id)
);

CREATE INDEX idx_domain_events_aggregate ON domain_events(aggregate_type, aggregate_id);
CREATE INDEX idx_domain_events_type ON domain_events(event_type);
CREATE INDEX idx_domain_events_occurred_at ON domain_events(occurred_at DESC);
```

---

## 5. Migration Best Practices

### Quy tắc vàng khi viết Migration

```
✅ Migration phải idempotent (chạy nhiều lần không lỗi)
✅ Luôn có UP và DOWN migration (rollback được)
✅ Không bao giờ xóa cột/bảng trong production ngay lập tức
✅ Thêm cột mới với DEFAULT trước, sau đó backfill, rồi add NOT NULL
✅ Tạo index CONCURRENTLY (không lock table)
❌ Không rename cột/bảng mà không có deprecation period
❌ Không thay đổi column type trực tiếp trên production lớn
```

### Safe Migration Patterns

```sql
-- ✅ Thêm cột nullable trước (safe)
ALTER TABLE users ADD COLUMN phone VARCHAR(20);

-- ✅ Backfill data
UPDATE users SET phone = '' WHERE phone IS NULL;

-- ✅ Sau khi backfill xong, mới add constraint
ALTER TABLE users ALTER COLUMN phone SET NOT NULL;

-- ✅ Tạo index không lock table (PostgreSQL)
CREATE INDEX CONCURRENTLY idx_users_phone ON users(phone);

-- ✅ Drop index không lock table (PostgreSQL)
DROP INDEX CONCURRENTLY idx_old_index;

-- ✅ Rename column safely
-- Step 1: Thêm cột mới
ALTER TABLE users ADD COLUMN full_name VARCHAR(255);
-- Step 2: Copy data
UPDATE users SET full_name = name;
-- Step 3: Update application code để dùng full_name
-- Step 4: (Sau deploy) Drop cột cũ
ALTER TABLE users DROP COLUMN name;
```

### Migration File Naming Convention
```
V{timestamp}_{description}.sql
V20240115120000_create_users_table.sql
V20240116090000_add_phone_to_users.sql
V20240117150000_create_orders_table.sql

# Hoặc sequential versioning
V001_create_users_table.sql
V002_add_phone_to_users.sql
```

### Complete Migration Example
```sql
-- V20240115120000_create_initial_schema.sql

-- Enable UUID extension (PostgreSQL)
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Users
CREATE TABLE IF NOT EXISTS users (
  id BIGSERIAL PRIMARY KEY,
  email VARCHAR(254) UNIQUE NOT NULL,
  password_hash VARCHAR(255),
  display_name VARCHAR(255),
  role VARCHAR(50) NOT NULL DEFAULT 'user',
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  email_verified_at TIMESTAMPTZ,
  deleted_at TIMESTAMPTZ,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_users_email ON users(email) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_users_created_at ON users(created_at DESC);

-- Auto-update trigger
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN NEW.updated_at = NOW(); RETURN NEW; END;
$$ language 'plpgsql';

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_trigger WHERE tgname = 'update_users_updated_at'
  ) THEN
    CREATE TRIGGER update_users_updated_at
      BEFORE UPDATE ON users
      FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
  END IF;
END $$;
```

---

## 6. PostgreSQL vs MySQL — Key Differences

| Feature | PostgreSQL | MySQL |
|---|---|---|
| Auto increment | `BIGSERIAL` | `BIGINT AUTO_INCREMENT` |
| Current timestamp | `NOW()` hoặc `CURRENT_TIMESTAMP` | `CURRENT_TIMESTAMP` |
| Datetime with TZ | `TIMESTAMPTZ` | `DATETIME` (không có TZ native) |
| JSON support | `JSONB` (indexable!) | `JSON` (không index được) |
| String concatenation | `\|\|` hoặc `CONCAT()` | `CONCAT()` |
| Boolean | `BOOLEAN` (true/false) | `TINYINT(1)` (1/0) |
| ENUM | Tránh dùng, dùng CHECK | `ENUM(...)` |
| Partial index | Có (`WHERE` clause) | Không có |
| Concurrent index | `CREATE INDEX CONCURRENTLY` | Không có (lock table) |
| Full text search | Built-in, mạnh | Có nhưng yếu hơn |
| Default charset | UTF8 | Phải set `utf8mb4` |

### MySQL gotchas cần nhớ
```sql
-- LUÔN set charset cho MySQL
CREATE TABLE users (...) 
ENGINE=InnoDB 
DEFAULT CHARSET=utf8mb4      -- Hỗ trợ emoji, tiếng Việt
COLLATE=utf8mb4_unicode_ci;  -- Case-insensitive, accent-sensitive

-- MySQL KHÔNG support partial indexes
-- MySQL KHÔNG có TIMESTAMPTZ, lưu UTC thủ công

-- MySQL strict mode (nên bật)
SET GLOBAL sql_mode = 'STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
```