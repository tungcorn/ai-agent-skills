# NoSQL Database Design

## Table of Contents
1. [SQL vs NoSQL — Khi nào dùng gì?](#sql-vs-nosql)
2. [MongoDB (Document)](#mongodb)
3. [Redis (Key-Value / Cache)](#redis)
4. [Cassandra (Wide-Column)](#cassandra)
5. [Elasticsearch (Search)](#elasticsearch)
6. [Hybrid Architecture](#hybrid)

---

## 1. SQL vs NoSQL — Khi nào dùng gì?

### Chọn SQL (Relational) khi:
- Dữ liệu có cấu trúc rõ ràng, schema ổn định
- Cần ACID transactions (tài chính, ngân hàng, đặt hàng)
- Quan hệ phức tạp giữa các entity
- Cần ad-hoc queries, reporting linh hoạt
- Team quen với SQL
- Ví dụ: hệ thống bán hàng, ERP, CRM, banking

### Chọn NoSQL khi:
- Schema thay đổi thường xuyên hoặc không đồng nhất
- Cần scale horizontal cực lớn (hàng tỷ records)
- Dữ liệu dạng document, graph, time-series
- Cần low-latency cực cao (< 1ms)
- Ví dụ: catalog sản phẩm đa dạng, social feed, IoT, gaming

### 🎯 Quyết định theo loại dữ liệu:

| Loại dữ liệu | Database phù hợp |
|---|---|
| Structured, relational | PostgreSQL, MySQL |
| Documents (flexible schema) | MongoDB, Firestore |
| Cache, session, leaderboard | Redis |
| Search, full-text | Elasticsearch, Meilisearch |
| Time-series (IoT, metrics) | TimescaleDB, InfluxDB |
| Graph (social network) | Neo4j, Amazon Neptune |
| Wide-column (Cassandra-style) | Cassandra, ScyllaDB |
| Event store | Kafka, EventStoreDB |

---

## 2. MongoDB — Document Database

### Nguyên tắc thiết kế MongoDB

**Rule of thumb: Embed vs Reference**
```
Embed khi:           Reference khi:
- 1:1 relationship   - 1:N với N lớn (>100)
- 1:few (N nhỏ)      - N:M relationships  
- Luôn access cùng   - Cần access riêng lẻ
- Tổng size < 16MB   - Document sẽ grow unbounded
```

### Pattern: Embedded Document
```javascript
// ✅ Embed address vào user (1:1, luôn cần cùng nhau)
{
  _id: ObjectId("..."),
  name: "Nguyen Van A",
  email: "a@example.com",
  addresses: [  // 1:few — embed vì thường < 5 địa chỉ
    {
      type: "home",
      street: "123 Nguyen Hue",
      city: "Ho Chi Minh",
      is_default: true
    }
  ],
  created_at: ISODate("2024-01-01")
}
```

### Pattern: Reference (DBRef)
```javascript
// ✅ Reference khi N lớn hoặc cần query riêng
// Collection: posts
{
  _id: ObjectId("post_id"),
  title: "Bài viết hay",
  author_id: ObjectId("user_id"),  // reference đến users collection
  category_ids: [ObjectId("cat1"), ObjectId("cat2")],  // N:M via references
  content: "...",
  created_at: ISODate("2024-01-01")
}

// Tránh: embedding toàn bộ user object vào post (duplicate, sync issues)
```

### Pattern: Bucket (cho Time-Series)
```javascript
// Thay vì 1 document per measurement, gom theo thời gian
// ✅ Bucket pattern — 1 document = 1 giờ dữ liệu
{
  device_id: "sensor_001",
  bucket_start: ISODate("2024-01-01T10:00:00"),
  bucket_end: ISODate("2024-01-01T11:00:00"),
  count: 60,
  measurements: [
    { ts: ISODate("2024-01-01T10:00:30"), temp: 25.3 },
    { ts: ISODate("2024-01-01T10:01:00"), temp: 25.5 },
    // ... 60 records trong 1 document
  ]
}
```

### MongoDB Schema Template
```javascript
// Collection: users
{
  _id: ObjectId(),          // MongoDB auto-generated
  email: String,            // UNIQUE index
  username: String,         // UNIQUE index
  password_hash: String,
  profile: {                // Embedded — luôn cần cùng nhau
    first_name: String,
    last_name: String,
    avatar_url: String,
    bio: String
  },
  settings: {               // Embedded — ít thay đổi
    notifications_enabled: Boolean,
    language: String
  },
  role: String,             // enum: 'user', 'admin', 'moderator'
  is_active: Boolean,
  created_at: Date,
  updated_at: Date
}

// Indexes
db.users.createIndex({ email: 1 }, { unique: true })
db.users.createIndex({ username: 1 }, { unique: true })
db.users.createIndex({ created_at: -1 })
```

### MongoDB Validation (Schema Enforcement)
```javascript
db.createCollection("products", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["name", "price", "status"],
      properties: {
        name: { bsonType: "string", minLength: 1 },
        price: { bsonType: "decimal", minimum: 0 },
        status: { enum: ["active", "inactive", "archived"] }
      }
    }
  }
})
```

---

## 3. Redis — Key-Value / Cache

### Khi nào dùng Redis
- Session storage
- Cache (database query results, API responses)
- Rate limiting
- Leaderboards / rankings
- Pub/Sub messaging
- Job queues
- Real-time features (online users, presence)

### Redis Data Structures & Use Cases

```
String  → Cache đơn giản, counters, sessions
Hash    → Object/record cache (user profile, product)
List    → Queue, recent activity feed, FIFO/LIFO
Set     → Unique members, tags, followers/following
Sorted Set → Leaderboard, priority queue, time-range queries
Stream  → Event log, message queue
```

### Pattern: Cache-Aside
```python
# Standard cache pattern
def get_user(user_id):
    cache_key = f"user:{user_id}"
    
    # 1. Check cache
    cached = redis.get(cache_key)
    if cached:
        return json.loads(cached)
    
    # 2. Cache miss → query DB
    user = db.query("SELECT * FROM users WHERE id = ?", user_id)
    
    # 3. Store in cache với TTL
    redis.setex(cache_key, 3600, json.dumps(user))  # 1 hour TTL
    return user
```

### Pattern: Leaderboard với Sorted Set
```python
# Add/update score
redis.zadd("game:leaderboard", {"player_123": 9500})

# Lấy top 10
redis.zrevrange("game:leaderboard", 0, 9, withscores=True)

# Rank của một player
redis.zrevrank("game:leaderboard", "player_123")
```

### Pattern: Rate Limiting
```python
def is_rate_limited(user_id, limit=100, window=3600):
    key = f"rate_limit:{user_id}"
    count = redis.incr(key)
    if count == 1:
        redis.expire(key, window)  # Set TTL chỉ lần đầu
    return count > limit
```

### Key Naming Convention cho Redis
```
{service}:{entity}:{id}:{field}

user:session:abc123           → session data
user:profile:456              → user profile cache
product:cache:789             → product data cache
rate_limit:api:user:456       → rate limiting
game:leaderboard:season:2024  → leaderboard
queue:email:pending           → email queue
lock:payment:order_123        → distributed lock
```

---

## 4. Cassandra — Wide-Column

### Nguyên tắc thiết kế Cassandra
**"Design your tables around your queries, not your data"**

Cassandra KHÔNG có JOINs, KHÔNG có foreign keys.
Mọi query phải access đúng 1 partition.

### Quy trình thiết kế Cassandra
```
1. Xác định queries cần thực hiện
2. Thiết kế partition key để distribute data đều
3. Chọn clustering columns để sort trong partition
4. Denormalize data nếu cần (1 query = 1 table)
```

### Ví dụ: Thiết kế cho Time-Series IoT
```sql
-- Query: "Lấy readings của device X từ thời gian A đến B"
CREATE TABLE device_readings (
  device_id UUID,
  recorded_at TIMESTAMP,
  temperature FLOAT,
  humidity FLOAT,
  PRIMARY KEY (device_id, recorded_at)  -- device_id là partition key
) WITH CLUSTERING ORDER BY (recorded_at DESC);

-- Query: "Lấy readings của tất cả devices trong 1 region"
CREATE TABLE region_device_readings (
  region TEXT,
  bucket TEXT,  -- 'region:2024-01' để giới hạn partition size
  device_id UUID,
  recorded_at TIMESTAMP,
  temperature FLOAT,
  PRIMARY KEY ((region, bucket), recorded_at, device_id)
);
```

---

## 5. Elasticsearch — Search Engine

### Khi nào dùng Elasticsearch
- Full-text search
- Log analytics (ELK Stack)
- Faceted search (filter theo nhiều điều kiện)
- Real-time analytics

### Index Design Basics
```json
{
  "mappings": {
    "properties": {
      "title": {
        "type": "text",
        "analyzer": "standard",
        "fields": {
          "keyword": { "type": "keyword" }
        }
      },
      "description": { "type": "text" },
      "price": { "type": "double" },
      "category": { "type": "keyword" },
      "tags": { "type": "keyword" },
      "created_at": { "type": "date" },
      "location": { "type": "geo_point" }
    }
  }
}
```

---

## 6. Hybrid Architecture — Dùng Nhiều DB Cùng Lúc

### Pattern phổ biến: PostgreSQL + Redis + Elasticsearch

```
PostgreSQL (Source of Truth)
  ↓ sync/CDC
Redis (Cache Layer) + Elasticsearch (Search Layer)

Luồng dữ liệu:
Write → PostgreSQL → trigger/CDC → sync to Redis/ES
Read → Check Redis → miss → Check PostgreSQL → update Redis
Search → Elasticsearch (sync từ PG qua CDC hoặc event)
```

### Ví dụ: E-commerce
```
PostgreSQL:     users, orders, payments, inventory (ACID critical)
Redis:          sessions, cart, product cache, rate limiting
Elasticsearch:  product search, full-text, faceted filters
MongoDB:        product catalog (flexible attributes per category)
```

### Database per Service (Microservices)
```
user-service      → PostgreSQL (structured, ACID)
product-service   → MongoDB (flexible schema per category)
search-service    → Elasticsearch
notification      → PostgreSQL + Redis pub/sub
analytics         → ClickHouse / BigQuery (OLAP)
```