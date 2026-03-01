# Database Security & Data Protection

## Table of Contents
1. [Encryption](#encryption)
2. [Access Control tầng Database](#access-control)
3. [Row-Level Security (RLS)](#rls)
4. [PII & Sensitive Data Handling](#pii)
5. [Data Masking & Anonymization](#masking)
6. [SQL Injection Prevention tầng Schema](#injection)
7. [Security Patterns cho từng Domain](#domain-security)
8. [Security Checklist](#checklist)

---

## 1. Encryption

### 1.1 Encryption at Rest
```
Mục đích: Bảo vệ dữ liệu khi lưu trên disk (database files, backups, logs)
```

#### OS-Level Encryption (Khuyến nghị cho toàn bộ disk)
```bash
# Linux: LUKS (Linux Unified Key Setup)
# Encrypt partition chứa PostgreSQL data
cryptsetup luksFormat /dev/sdb1
cryptsetup luksOpen /dev/sdb1 pg_data
mkfs.ext4 /dev/mapper/pg_data
mount /dev/mapper/pg_data /var/lib/postgresql/16/data

# Windows: BitLocker
# Bật BitLocker cho ổ đĩa chứa database files

# Cloud: Tự động bật ở hầu hết managed services
# AWS RDS: Mặc định encrypt at rest bằng AES-256
# Azure SQL: Transparent Data Encryption (TDE) mặc định
# GCP Cloud SQL: Mặc định encrypt
```

#### Column-Level Encryption (Cho dữ liệu nhạy cảm cụ thể)
```sql
-- PostgreSQL: Dùng pgcrypto extension
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ========================================
-- Schema cho bảng có dữ liệu encrypt
-- ========================================
CREATE TABLE user_payment_methods (
  id BIGSERIAL PRIMARY KEY,
  user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  payment_type VARCHAR(50) NOT NULL
    CHECK (payment_type IN ('credit_card', 'debit_card', 'bank_account')),
  
  -- ❌ KHÔNG lưu thế này
  -- card_number VARCHAR(20),        -- Plain text!
  -- cvv VARCHAR(4),                 -- Plain text!
  
  -- ✅ Encrypt dữ liệu nhạy cảm
  card_last_four CHAR(4) NOT NULL,                 -- Hiển thị cho user (***1234)
  card_number_encrypted BYTEA NOT NULL,            -- AES-256 encrypted
  card_holder_name_encrypted BYTEA NOT NULL,
  expiry_month_encrypted BYTEA NOT NULL,
  expiry_year_encrypted BYTEA NOT NULL,
  -- CVV: KHÔNG BAO GIỜ lưu! (vi phạm PCI-DSS)
  
  -- Billing address
  billing_address_encrypted BYTEA,
  
  -- Metadata (không cần encrypt)
  is_default BOOLEAN NOT NULL DEFAULT FALSE,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index cho lookup (user_id) — KHÔNG index encrypted columns!
CREATE INDEX idx_payment_methods_user ON user_payment_methods(user_id);

-- ========================================
-- Encrypt / Decrypt operations
-- ========================================

-- Symmetric encryption (AES-256, dùng khi app cần decrypt)
-- Key PHẢI lưu trong secrets manager (Vault, AWS Secrets Manager), KHÔNG hardcode!

-- INSERT encrypted data
INSERT INTO user_payment_methods 
  (user_id, payment_type, card_last_four, card_number_encrypted,
   card_holder_name_encrypted, expiry_month_encrypted, expiry_year_encrypted)
VALUES (
  123,
  'credit_card',
  '1234',
  pgp_sym_encrypt('4111111111111234', current_setting('app.encryption_key')),
  pgp_sym_encrypt('NGUYEN VAN A', current_setting('app.encryption_key')),
  pgp_sym_encrypt('12', current_setting('app.encryption_key')),
  pgp_sym_encrypt('2026', current_setting('app.encryption_key'))
);

-- SELECT decrypted data (chỉ khi thật sự cần)
SELECT
  id,
  card_last_four,
  pgp_sym_decrypt(card_number_encrypted, current_setting('app.encryption_key')) AS card_number,
  pgp_sym_decrypt(card_holder_name_encrypted, current_setting('app.encryption_key')) AS card_holder
FROM user_payment_methods
WHERE user_id = 123;

-- ========================================
-- Khi nào encrypt column-level vs dùng OS-level?
-- ========================================
-- OS-level: Encrypt toàn bộ → bảo vệ khi disk bị đánh cắp
-- Column-level: Encrypt cột nhạy cảm → bảo vệ ngay cả khi DB user bị leak
-- 
-- Best practice: DÙNG CẢ HAI
-- OS-level cho baseline protection
-- Column-level cho high-value data (credit cards, SSN, medical records)
```

### 1.2 Encryption in Transit
```
Mục đích: Bảo vệ dữ liệu khi truyền giữa application ↔ database
```

```sql
-- ========================================
-- PostgreSQL SSL Configuration
-- ========================================

-- postgresql.conf
-- ssl = on
-- ssl_cert_file = '/etc/ssl/certs/server.crt'
-- ssl_key_file = '/etc/ssl/private/server.key'
-- ssl_ca_file = '/etc/ssl/certs/ca.crt'

-- pg_hba.conf (Force SSL cho remote connections)
-- hostssl all all 0.0.0.0/0 md5
-- ↑ "hostssl" thay vì "host" → BẮT BUỘC SSL
```

```python
# Connection strings với SSL — từ application

# PostgreSQL
# sslmode options:
# ┌──────────────┬────────────────────────────────────────────────────────┐
# │ Mode         │ SSL?  │ Verify Cert? │ Verify Host? │ Cho môi trường │
# ├──────────────┼───────┼──────────────┼──────────────┼────────────────┤
# │ disable      │ ❌    │ ❌           │ ❌           │ ❌ KHÔNG DÙNG  │
# │ allow        │ ⚠️    │ ❌           │ ❌           │ ❌ KHÔNG DÙNG  │
# │ prefer       │ ✅    │ ❌           │ ❌           │ Development    │
# │ require      │ ✅    │ ❌           │ ❌           │ ✅ Minimum prod│
# │ verify-ca    │ ✅    │ ✅           │ ❌           │ ✅ Recommended │
# │ verify-full  │ ✅    │ ✅           │ ✅           │ ✅ Most secure │
# └──────────────┴───────┴──────────────┴──────────────┴────────────────┘

DATABASE_URL = "postgresql://user:pass@host:5432/db?sslmode=verify-full&sslrootcert=/path/to/ca.crt"

# MySQL
DATABASE_URL = "mysql://user:pass@host:3306/db?ssl-mode=VERIFY_IDENTITY&ssl-ca=/path/to/ca.pem"
```

### 1.3 Password Hashing
```sql
-- ========================================
-- ⚠️ CRITICAL: Password PHẢI hash ở APPLICATION LAYER
-- Database chỉ lưu hash đã được tạo bởi application
-- ========================================

-- ❌ SAI: Plain text
password VARCHAR(255)  -- 'MyPassword123'

-- ❌ SAI: MD5 (broken, rainbow tables)
password_hash CHAR(32)  -- MD5('MyPassword123')

-- ❌ SAI: SHA-256 (quá nhanh → brute force dễ, không có salt)
password_hash CHAR(64)  -- SHA-256('MyPassword123')

-- ✅ ĐÚNG: bcrypt hash (từ application)
password_hash VARCHAR(255) NOT NULL
-- Lưu: '$2b$12$LJ3m4yz...' (bcrypt output, ~60 chars)

-- ✅ ĐÚNG: Argon2 hash (newer, recommended)
password_hash VARCHAR(255) NOT NULL
-- Lưu: '$argon2id$v=19$m=65536...' (argon2 output)
```

```python
# Application-level hashing examples:

# Python (bcrypt)
import bcrypt
hashed = bcrypt.hashpw(password.encode(), bcrypt.gensalt(rounds=12))
# rounds=12 → ~250ms per hash (good balance)

# Python (argon2 — recommended for new projects)
from argon2 import PasswordHasher
ph = PasswordHasher(
    time_cost=3,       # iterations
    memory_cost=65536,  # 64 MB
    parallelism=4       # threads
)
hashed = ph.hash(password)

# Node.js (bcrypt)
const bcrypt = require('bcrypt');
const hash = await bcrypt.hash(password, 12);

# C# (BCrypt.Net)
string hash = BCrypt.Net.BCrypt.HashPassword(password, 12);
```

### 1.4 Encryption Key Management
```
⚠️ CRITICAL: Quản lý encryption keys
```

```
❌ KHÔNG BAO GIỜ:
- Hardcode key trong source code
- Lưu key trong database (cùng nơi với data!)
- Dùng chung key cho mọi môi trường
- Dùng weak keys (ngắn, predictable)

✅ LUÔN:
- Lưu keys trong secrets manager:
  → HashiCorp Vault
  → AWS Secrets Manager / KMS
  → Azure Key Vault
  → Google Cloud KMS
- Rotate keys định kỳ (mỗi 90 ngày)
- Khác key cho dev/staging/production
- Key length ≥ 256 bits cho AES
- Log key access cho audit

Key Rotation Strategy:
1. Tạo key mới (v2)
2. Encrypt data mới bằng key v2
3. Batch re-encrypt data cũ từ key v1 → key v2
4. Sau khi 100% migrate → retire key v1
```

---

## 2. Access Control tầng Database

### 2.1 Principle of Least Privilege
```sql
-- ========================================
-- PostgreSQL Role Hierarchy
-- ========================================

-- 1. KHÔNG bao giờ cho app dùng superuser (postgres)!
-- 2. Tạo roles theo chức năng

-- Role: Application (CRUD operations)
CREATE ROLE app_readwrite NOLOGIN;  -- NOLOGIN = role, không phải user
GRANT CONNECT ON DATABASE myapp TO app_readwrite;
GRANT USAGE ON SCHEMA public TO app_readwrite;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_readwrite;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO app_readwrite;
-- Auto apply cho bảng tương lai
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_readwrite;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO app_readwrite;

-- Role: Reporting (Read-only)
CREATE ROLE app_readonly NOLOGIN;
GRANT CONNECT ON DATABASE myapp TO app_readonly;
GRANT USAGE ON SCHEMA public TO app_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO app_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT ON TABLES TO app_readonly;

-- Role: Migration / Admin (DDL)
CREATE ROLE app_admin NOLOGIN;
GRANT CONNECT ON DATABASE myapp TO app_admin;
GRANT ALL PRIVILEGES ON SCHEMA public TO app_admin;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO app_admin;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO app_admin;

-- Role: Audit (Insert-only vào audit tables)
CREATE ROLE app_audit NOLOGIN;
GRANT CONNECT ON DATABASE myapp TO app_audit;
GRANT USAGE ON SCHEMA audit TO app_audit;
GRANT INSERT ON ALL TABLES IN SCHEMA audit TO app_audit;
-- ❌ KHÔNG GRANT UPDATE, DELETE trên audit tables!

-- ========================================
-- Tạo Users và gán Roles
-- ========================================

CREATE USER web_api WITH PASSWORD 'strong_random_password_here';
GRANT app_readwrite TO web_api;

CREATE USER reporting_svc WITH PASSWORD 'strong_random_password_here';
GRANT app_readonly TO reporting_svc;

CREATE USER migration_runner WITH PASSWORD 'strong_random_password_here';
GRANT app_admin TO migration_runner;

CREATE USER background_worker WITH PASSWORD 'strong_random_password_here';
GRANT app_readwrite TO background_worker;
GRANT app_audit TO background_worker;
```

### 2.2 Schema-level Isolation
```sql
-- Dùng PostgreSQL schemas để tách biệt data theo chức năng
-- Giúp quản lý permissions granular hơn

CREATE SCHEMA app_data;      -- Business data chính
CREATE SCHEMA auth;          -- Authentication & authorization
CREATE SCHEMA audit;         -- Audit logs (immutable)
CREATE SCHEMA analytics;     -- Analytics & reporting data
CREATE SCHEMA staging;       -- Staging/temp data

-- Permissions
-- App chỉ thấy app_data + auth
GRANT USAGE ON SCHEMA app_data TO app_readwrite;
GRANT USAGE ON SCHEMA auth TO app_readwrite;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA app_data TO app_readwrite;
GRANT SELECT ON ALL TABLES IN SCHEMA auth TO app_readwrite;
-- App KHÔNG thấy audit, analytics, staging

-- Reporting chỉ thấy analytics
GRANT USAGE ON SCHEMA analytics TO app_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA analytics TO app_readonly;

-- Audit: chỉ INSERT, KHÔNG thể sửa/xóa logs
GRANT USAGE ON SCHEMA audit TO app_readwrite;
GRANT INSERT ON ALL TABLES IN SCHEMA audit TO app_readwrite;
-- KHÔNG GRANT UPDATE, DELETE!
```

### 2.3 MySQL Specific Permissions
```sql
-- MySQL: Permissions tương tự nhưng syntax khác

-- Read-write user
CREATE USER 'web_api'@'10.0.%' IDENTIFIED BY 'strong_password';
-- Giới hạn IP range: chỉ từ 10.0.x.x
GRANT SELECT, INSERT, UPDATE, DELETE ON myapp.* TO 'web_api'@'10.0.%';

-- Read-only user
CREATE USER 'reporting'@'10.0.%' IDENTIFIED BY 'strong_password';
GRANT SELECT ON myapp.* TO 'reporting'@'10.0.%';

-- Revoke nguy hiểm
REVOKE FILE, PROCESS, SUPER, SHUTDOWN ON *.* FROM 'web_api'@'10.0.%';
REVOKE CREATE, DROP, ALTER ON *.* FROM 'web_api'@'10.0.%';
```

---

## 3. Row-Level Security (RLS) — PostgreSQL

### 3.1 Multi-tenant Isolation
```sql
-- ========================================
-- Scenario: SaaS app, mỗi org chỉ thấy data của mình
-- ========================================

-- Bước 1: Enable RLS
ALTER TABLE projects ENABLE ROW LEVEL SECURITY;
-- FORCE cho cả table owner (mặc định owner bypass RLS)
ALTER TABLE projects FORCE ROW LEVEL SECURITY;

-- Bước 2: Tạo policy
-- USING clause: filter rows khi SELECT
-- WITH CHECK clause: validate rows khi INSERT/UPDATE
CREATE POLICY tenant_isolation_select ON projects
  FOR SELECT
  USING (organization_id = current_setting('app.current_org_id')::BIGINT);

CREATE POLICY tenant_isolation_insert ON projects
  FOR INSERT
  WITH CHECK (organization_id = current_setting('app.current_org_id')::BIGINT);

CREATE POLICY tenant_isolation_update ON projects
  FOR UPDATE
  USING (organization_id = current_setting('app.current_org_id')::BIGINT)
  WITH CHECK (organization_id = current_setting('app.current_org_id')::BIGINT);

CREATE POLICY tenant_isolation_delete ON projects
  FOR DELETE
  USING (organization_id = current_setting('app.current_org_id')::BIGINT);

-- Bước 3: Application set context mỗi request
-- (Trong middleware/interceptor)
SET LOCAL app.current_org_id = '42';
SET LOCAL app.current_user_id = '123';
SET LOCAL app.current_role = 'admin';
-- "LOCAL" = chỉ trong transaction hiện tại

-- Bước 4: Mọi query tự động filter!
SELECT * FROM projects;
-- DB tự thêm: WHERE organization_id = 42
-- User KHÔNG THỂ thấy data của org khác, dù cố gắng
```

### 3.2 Role-based Access trong RLS
```sql
-- Admin: thấy tất cả trong org
-- Member: chỉ thấy projects được assign
-- Viewer: chỉ thấy public projects

CREATE POLICY admin_all ON projects
  FOR ALL
  USING (
    organization_id = current_setting('app.current_org_id')::BIGINT
    AND current_setting('app.current_role') = 'admin'
  );

CREATE POLICY member_assigned ON projects
  FOR SELECT
  USING (
    organization_id = current_setting('app.current_org_id')::BIGINT
    AND current_setting('app.current_role') = 'member'
    AND id IN (
      SELECT project_id FROM project_members
      WHERE user_id = current_setting('app.current_user_id')::BIGINT
    )
  );

CREATE POLICY viewer_public ON projects
  FOR SELECT
  USING (
    organization_id = current_setting('app.current_org_id')::BIGINT
    AND current_setting('app.current_role') = 'viewer'
    AND is_public = TRUE
  );
```

### 3.3 RLS Performance Tips
```sql
-- ⚠️ RLS có thể ảnh hưởng performance nếu không cẩn thận

-- 1. Index cột dùng trong policy
CREATE INDEX idx_projects_org ON projects(organization_id);
CREATE INDEX idx_project_members_user ON project_members(user_id, project_id);

-- 2. Tránh subquery phức tạp trong policy
-- ❌ Chậm: subquery mỗi row
USING (id IN (SELECT project_id FROM complex_view WHERE ...))

-- ✅ Nhanh: simple equality check
USING (organization_id = current_setting('app.current_org_id')::BIGINT)

-- 3. Monitor: Check xem RLS policies có được dùng Index không
EXPLAIN ANALYZE SELECT * FROM projects;
```

---

## 4. PII & Sensitive Data Handling

### 4.1 Phân loại dữ liệu chi tiết
```
Level           | Ví dụ dữ liệu                    | Yêu cầu xử lý
────────────────────────────────────────────────────────────────────────────────
PUBLIC          | Product name, blog title,         | Không cần đặc biệt
                | pricing plans, FAQ                |
────────────────────────────────────────────────────────────────────────────────
INTERNAL        | Order ID, user ID, internal       | Access control, không
                | metrics, settings                 | expose ra ngoài
────────────────────────────────────────────────────────────────────────────────
CONFIDENTIAL    | Email, phone, address,            | Encrypt at rest, mask
                | date of birth, IP address         | trong non-prod, audit log
────────────────────────────────────────────────────────────────────────────────
RESTRICTED      | SSN/CCCD, credit card number,     | Encrypt column-level,
                | health records, biometric data,   | strict access, audit mọi
                | financial account numbers         | truy cập, compliance required
```

### 4.2 Schema Pattern: Tách PII ra bảng riêng
```sql
-- ========================================
-- Pattern: Separate PII Table
-- Lý do: Dễ quản lý encryption, access control, GDPR compliance
-- ========================================

-- Bảng chính: KHÔNG chứa PII
CREATE TABLE users (
  id BIGSERIAL PRIMARY KEY,
  username VARCHAR(50) UNIQUE NOT NULL,
  role VARCHAR(50) NOT NULL DEFAULT 'user'
    CHECK (role IN ('user', 'admin', 'moderator')),
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  last_login_at TIMESTAMPTZ,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deleted_at TIMESTAMPTZ
);

-- Bảng PII: Cần encryption, RLS, audit
CREATE TABLE user_pii (
  id BIGSERIAL PRIMARY KEY,
  user_id BIGINT UNIQUE NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  
  -- PII fields (encrypt at rest via pgcrypto hoặc application-level)
  email_encrypted BYTEA NOT NULL,
  email_hash VARCHAR(64) NOT NULL,         -- SHA-256 hash để lookup (không reversible)
  phone_encrypted BYTEA,
  full_name_encrypted BYTEA NOT NULL,
  date_of_birth_encrypted BYTEA,
  address_encrypted BYTEA,
  national_id_encrypted BYTEA,             -- CCCD/SSN (RESTRICTED)
  
  -- GDPR Consent & Retention
  consent_given_at TIMESTAMPTZ NOT NULL,    -- Khi nào user đồng ý thu thập data
  consent_version VARCHAR(20) NOT NULL,     -- Version của privacy policy
  consent_purposes TEXT[] NOT NULL,         -- {'marketing', 'analytics', 'essential'}
  data_retention_until TIMESTAMPTZ,         -- Deadline xóa data
  
  -- Metadata
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index hash để tìm kiếm bằng email (không index encrypted data!)
CREATE UNIQUE INDEX idx_user_pii_email_hash ON user_pii(email_hash);

-- Enable RLS cho PII table
ALTER TABLE user_pii ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_pii FORCE ROW LEVEL SECURITY;

-- ========================================
-- Lookup bằng email:
-- Application: hash email → search by email_hash
-- ========================================
-- SELECT u.id, u.username FROM users u
-- JOIN user_pii p ON p.user_id = u.id
-- WHERE p.email_hash = encode(sha256('user@example.com'), 'hex');
```

### 4.3 GDPR / Data Protection Compliance Patterns
```sql
-- ========================================
-- Right to Access (GDPR Article 15)
-- User yêu cầu xem tất cả data về mình
-- ========================================

-- Tạo function export all user data
CREATE OR REPLACE FUNCTION export_user_data(p_user_id BIGINT)
RETURNS JSONB AS $$
DECLARE
  result JSONB;
BEGIN
  SELECT jsonb_build_object(
    'user', (SELECT to_jsonb(u) FROM users u WHERE u.id = p_user_id),
    'pii', (SELECT to_jsonb(p) FROM user_pii p WHERE p.user_id = p_user_id),
    'orders', (SELECT jsonb_agg(to_jsonb(o)) FROM orders o WHERE o.user_id = p_user_id),
    'comments', (SELECT jsonb_agg(to_jsonb(c)) FROM comments c WHERE c.author_id = p_user_id),
    'exported_at', NOW()
  ) INTO result;
  
  -- Audit log
  INSERT INTO audit_logs (table_name, record_id, action, changed_by, changed_at)
  VALUES ('users', p_user_id, 'DATA_EXPORT', p_user_id, NOW());
  
  RETURN result;
END;
$$ LANGUAGE plpgsql;

-- ========================================
-- Right to Erasure / Right to be Forgotten (GDPR Article 17)
-- ========================================

CREATE TABLE data_deletion_requests (
  id BIGSERIAL PRIMARY KEY,
  user_id BIGINT NOT NULL REFERENCES users(id),
  requested_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  deadline_at TIMESTAMPTZ NOT NULL DEFAULT NOW() + INTERVAL '30 days', -- GDPR: 30 ngày
  processed_at TIMESTAMPTZ,
  status VARCHAR(50) NOT NULL DEFAULT 'pending'
    CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'rejected')),
  rejection_reason TEXT,       -- Nếu reject: lý do hợp pháp
  deleted_tables TEXT[],       -- Danh sách bảng đã xóa data
  notes TEXT,
  processed_by BIGINT REFERENCES users(id)
);

-- ========================================
-- Data Retention: Tự động xóa/anonymize data hết hạn
-- ========================================

-- Bảng quản lý policy
CREATE TABLE data_retention_policies (
  id SERIAL PRIMARY KEY,
  table_name VARCHAR(255) NOT NULL,
  retention_days INT NOT NULL,
  action VARCHAR(50) NOT NULL DEFAULT 'delete'
    CHECK (action IN ('delete', 'anonymize', 'archive')),
  where_clause TEXT,  -- Extra conditions
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  last_run_at TIMESTAMPTZ,
  description TEXT
);

-- Policies mẫu
INSERT INTO data_retention_policies (table_name, retention_days, action, description)
VALUES
  ('user_sessions', 90, 'delete', 'Xóa sessions hết hạn sau 90 ngày'),
  ('password_resets', 7, 'delete', 'Xóa reset tokens sau 7 ngày'),
  ('audit_logs', 365, 'archive', 'Archive audit logs sau 1 năm'),
  ('analytics_events', 730, 'anonymize', 'Anonymize analytics sau 2 năm'),
  ('notifications', 180, 'delete', 'Xóa notifications đã đọc sau 6 tháng'),
  ('email_verifications', 30, 'delete', 'Xóa email verification tokens sau 30 ngày');
```

---

## 5. Data Masking & Anonymization

### 5.1 Static Masking (cho non-production)
```sql
-- ========================================
-- Khi copy production data sang staging/dev
-- PHẢI mask PII trước khi dùng
-- ========================================

-- Tạo materialized view với masked data
CREATE MATERIALIZED VIEW dev_users AS
SELECT
  id,
  -- Mask email: j***@example.com → giữ domain để test email flows
  CONCAT(
    LEFT(email, 1),
    REPEAT('*', GREATEST(LENGTH(SPLIT_PART(email, '@', 1)) - 1, 3)),
    '@',
    SPLIT_PART(email, '@', 2)
  ) AS email,
  
  -- Mask phone: *** *** 1234 → giữ 4 số cuối
  CONCAT('***-***-', RIGHT(phone, 4)) AS phone,
  
  -- Mask name: thay bằng fake name
  CONCAT('User_', id) AS display_name,
  
  -- Mask address
  'XX Fake Street' AS address_line1,
  NULL AS address_line2,
  
  -- Giữ nguyên non-PII
  role,
  is_active,
  created_at,
  updated_at
FROM users;

-- ========================================
-- Script mask toàn bộ data trước khi export
-- ========================================
-- pg_dump production_db | sed 's/email_pattern/masked/g' > staging.sql
-- Hoặc dùng tool chuyên dụng: pg_anonymize, Jailer, DataMasker
```

### 5.2 Dynamic Masking (Runtime)
```sql
-- ========================================
-- PostgreSQL: Dùng VIEW + current_setting để mask theo role
-- ========================================

-- Function mask email theo role
CREATE OR REPLACE FUNCTION mask_email(raw_email TEXT)
RETURNS TEXT AS $$
BEGIN
  -- Admin và support thấy full email
  IF current_setting('app.current_role', true) IN ('admin', 'support') THEN
    RETURN raw_email;
  END IF;
  -- Các role khác thấy masked
  RETURN CONCAT(
    LEFT(raw_email, 1), '***@', SPLIT_PART(raw_email, '@', 2)
  );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER STABLE;

-- Function mask phone
CREATE OR REPLACE FUNCTION mask_phone(raw_phone TEXT)
RETURNS TEXT AS $$
BEGIN
  IF raw_phone IS NULL THEN RETURN NULL; END IF;
  IF current_setting('app.current_role', true) IN ('admin', 'support') THEN
    RETURN raw_phone;
  END IF;
  RETURN CONCAT('***-', RIGHT(raw_phone, 4));
END;
$$ LANGUAGE plpgsql SECURITY DEFINER STABLE;

-- View dùng masking functions
CREATE VIEW users_masked AS
SELECT
  id,
  mask_email(email) AS email,
  mask_phone(phone) AS phone,
  display_name,
  role,
  is_active,
  created_at
FROM users;

-- Application queries users_masked thay vì users trực tiếp
```

### 5.3 Anonymization cho Analytics/Data Warehouse
```sql
-- ========================================
-- Khi export data cho analytics/ML, anonymize triệt để
-- ========================================

CREATE TABLE analytics_events (
  id BIGSERIAL PRIMARY KEY,
  -- Dùng hashed user ID (không thể reverse)
  user_hash VARCHAR(64) NOT NULL,  -- SHA-256(user_id + secret_salt)
  
  -- Event data
  event_type VARCHAR(100) NOT NULL,
  event_data JSONB,
  
  -- Generalize time (bỏ giây/phút → privacy)
  event_hour TIMESTAMPTZ NOT NULL,  -- DATE_TRUNC('hour', event_time)
  
  -- Generalize location
  city VARCHAR(100),       -- Giữ city, bỏ address
  country_code CHAR(2),
  -- ❌ KHÔNG lưu: IP address, exact coordinates
  
  -- Device info (generalize)
  device_category VARCHAR(20),  -- 'mobile', 'desktop', 'tablet'
  os_family VARCHAR(50),        -- 'iOS', 'Android', 'Windows'
  -- ❌ KHÔNG lưu: exact user agent, device fingerprint
  
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- K-anonymity: đảm bảo mỗi combination of quasi-identifiers
-- xuất hiện ít nhất K lần (thường K >= 5)
-- Quasi-identifiers: age_range + gender + city
```

---

## 6. SQL Injection Prevention tầng Schema

### 6.1 Database-Level Protections
```sql
-- ========================================
-- Chủ yếu xử lý ở APPLICATION LAYER (parameterized queries)
-- Database tạo thêm lớp bảo vệ (defense in depth)
-- ========================================

-- 1. Giới hạn quyền DDL
REVOKE CREATE ON SCHEMA public FROM app_readwrite;
REVOKE ALL ON DATABASE mydb FROM app_readwrite;
GRANT CONNECT ON DATABASE mydb TO app_readwrite;
-- App user KHÔNG THỂ CREATE TABLE, DROP TABLE, ALTER TABLE

-- 2. Giới hạn EXECUTE quyền
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
-- Chỉ grant execute cho specific functions cần thiết
GRANT EXECUTE ON FUNCTION get_user_by_email(VARCHAR) TO app_readwrite;

-- 3. Validate input trong CHECK constraints
ALTER TABLE users ADD CONSTRAINT valid_email
  CHECK (email ~* '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$');

ALTER TABLE users ADD CONSTRAINT valid_phone
  CHECK (phone IS NULL OR phone ~ '^\+?[0-9\s\-()]{7,20}$');

ALTER TABLE users ADD CONSTRAINT valid_username
  CHECK (username ~ '^[a-zA-Z0-9_]{3,50}$');

-- 4. Giới hạn chiều dài input (defense against buffer overflow)
username VARCHAR(50),        -- Giới hạn hợp lý
bio VARCHAR(500),
email VARCHAR(254),          -- RFC 5321 max
url TEXT CHECK (LENGTH(url) <= 2048),

-- 5. Dùng Prepared Statements trong stored procedures
CREATE OR REPLACE FUNCTION search_products(
  p_query TEXT,
  p_category_id INT DEFAULT NULL,
  p_min_price DECIMAL DEFAULT NULL,
  p_max_price DECIMAL DEFAULT NULL,
  p_limit INT DEFAULT 20,
  p_offset INT DEFAULT 0
)
RETURNS TABLE (
  id BIGINT, name VARCHAR, price DECIMAL, category_id INT
) AS $$
BEGIN
  -- ✅ Parameterized — safe from injection
  RETURN QUERY
  SELECT p.id, p.name, p.price, p.category_id
  FROM products p
  WHERE p.deleted_at IS NULL
    AND (p_query IS NULL OR p.name ILIKE '%' || p_query || '%')
    AND (p_category_id IS NULL OR p.category_id = p_category_id)
    AND (p_min_price IS NULL OR p.price >= p_min_price)
    AND (p_max_price IS NULL OR p.price <= p_max_price)
  ORDER BY p.created_at DESC
  LIMIT LEAST(p_limit, 100)  -- Max 100 để tránh abuse
  OFFSET p_offset;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;
```

### 6.2 Application-Level Reminders (cho AI khi viết code)
```
✅ LUÔN dùng parameterized queries / prepared statements:

# Python (psycopg2)
cursor.execute("SELECT * FROM users WHERE email = %s", (email,))

# Node.js (pg)
pool.query('SELECT * FROM users WHERE email = $1', [email])

# Java (JDBC)
PreparedStatement ps = conn.prepareStatement("SELECT * FROM users WHERE email = ?");
ps.setString(1, email);

# C# (Dapper)
var user = connection.QueryFirstOrDefault<User>(
  "SELECT * FROM users WHERE email = @Email", new { Email = email });

❌ KHÔNG BAO GIỜ string concatenation:
# cursor.execute(f"SELECT * FROM users WHERE email = '{email}'")  ← INJECTION!
```

---

## 7. Security Patterns cho từng Domain

### 7.1 E-commerce
```sql
-- Payment data: PCI-DSS compliance
-- KHÔNG lưu CVV, lưu card_last_four để hiển thị
-- Tokenize card numbers qua payment gateway (Stripe, VNPay)

-- Order access: user chỉ thấy order của mình
CREATE POLICY own_orders ON orders
  FOR SELECT
  USING (user_id = current_setting('app.current_user_id')::BIGINT
         OR current_setting('app.current_role') = 'admin');
```

### 7.2 Healthcare (HIPAA-like)
```sql
-- Tách medical records riêng schema
CREATE SCHEMA medical;

-- Audit log BẮT BUỘC cho mọi access
CREATE TRIGGER audit_medical_records
  AFTER SELECT OR INSERT OR UPDATE OR DELETE ON medical.patient_records
  FOR EACH ROW EXECUTE FUNCTION audit_trigger_func();

-- Break-glass: Emergency access override (logged + alerted)
```

### 7.3 Multi-tenant SaaS
```sql
-- RLS trên MỌI bảng tenant-scoped
-- Đảm bảo organization_id có mặt ở mọi bảng
-- Index organization_id trên mọi bảng

-- Cross-tenant query prevention
CREATE POLICY strict_tenant ON ALL TABLES IN SCHEMA app_data
  FOR ALL
  USING (organization_id = current_setting('app.current_org_id')::BIGINT);
```

---

## 8. Security Checklist

```
═══════════════════════════════════════════════════════════
  SECURITY CHECKLIST — Check TRƯỚC KHI deploy production
═══════════════════════════════════════════════════════════

Authentication & Access:
  - [ ] Application KHÔNG dùng superuser/root account?
  - [ ] Mỗi service/app có DB user riêng với least privilege?
  - [ ] Default accounts (postgres, root) đã đổi password hoặc disable?
  - [ ] Connection strings KHÔNG hardcode password?
        → Dùng env vars, secrets manager, hoặc IAM auth
  - [ ] Mỗi môi trường (dev/staging/prod) dùng credentials riêng?

Encryption:
  - [ ] SSL/TLS đã bật cho database connections?
  - [ ] sslmode ≥ require (không dùng disable/allow/prefer)?
  - [ ] Dữ liệu RESTRICTED đã encrypt column-level?
  - [ ] Encryption keys lưu trong secrets manager (không trong code)?
  - [ ] Key rotation schedule đã setup?
  - [ ] Password hash bằng bcrypt/argon2 (KHÔNG MD5/SHA trần)?
  - [ ] Backup files đã encrypt?

Data Protection:
  - [ ] PII đã xác định và phân loại (PUBLIC/INTERNAL/CONFIDENTIAL/RESTRICTED)?
  - [ ] PII tách ra bảng riêng (nếu áp dụng)?
  - [ ] Có data retention policy cho mỗi bảng chứa PII?
  - [ ] Non-production environments dùng masked/anonymized data?
  - [ ] GDPR right-to-erasure process đã implement?
  - [ ] Audit logging đã bật cho bảng nhạy cảm?

Row-Level Security (nếu multi-tenant):
  - [ ] RLS đã enable trên mọi bảng tenant-scoped?
  - [ ] FORCE RLS đã bật (owner cũng bị filter)?
  - [ ] RLS policies đã test (tenant A không thấy data tenant B)?
  - [ ] RLS cột đã có index phù hợp?

Network:
  - [ ] Database KHÔNG expose public internet?
        → Dùng private subnet / VPC
  - [ ] Firewall rules chỉ allow IP ranges cần thiết?
  - [ ] Database port KHÔNG dùng default (5432 → custom port)?
  - [ ] Connection limit đã set (max_connections)?

Monitoring & Incident Response:
  - [ ] Có log failed login attempts?
  - [ ] Có alert cho unusual query patterns?
  - [ ] Có alert cho bulk DELETE / TRUNCATE?
  - [ ] Có regular security audit schedule (quarterly)?
  - [ ] Có incident response plan cho data breach?
```
