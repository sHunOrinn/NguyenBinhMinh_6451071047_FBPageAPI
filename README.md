# Facebook Page Automation System

Hệ thống tự động quản lý và xử lý bình luận Facebook Page theo kiến trúc microservices.  
Dự án sử dụng ASP.NET Core, Apache Kafka, Docker, Facebook Graph API, Gemini API và Supabase để xây dựng luồng xử lý bình luận phân tán.

---

## 1. Giới thiệu

Facebook Page Automation System là hệ thống backend phân tán dùng để tự động xử lý bình luận trên Facebook Page. Khi người dùng bình luận vào bài viết, hệ thống sẽ nhận sự kiện thông qua Facebook Webhook, đưa dữ liệu vào Kafka, phân tích nội dung bằng AI và quyết định hành động phù hợp như trả lời bình luận, xóa bình luận spam hoặc xử lý retry khi gọi Facebook API thất bại.

Hệ thống được tách thành nhiều service độc lập nhằm dễ mở rộng, dễ kiểm thử và phù hợp với mô hình xử lý sự kiện trong hệ thống phân tán.

---

## 2. Chức năng chính

- Nhận sự kiện bình luận từ Facebook Page thông qua Webhook.
- Chuẩn hóa dữ liệu webhook thành event nội bộ.
- Publish event vào Kafka topic `raw_events`.
- Phân tích bình luận bằng Gemini API.
- Phân loại intent, sentiment, spam và nội dung không phù hợp.
- Tự động trả lời bình luận hợp lệ.
- Tự động xóa bình luận spam hoặc nội dung không phù hợp.
- Chống gửi phản hồi trùng bằng idempotency key.
- Retry khi gọi Facebook Graph API thất bại.
- Đưa message lỗi quá số lần retry vào topic `dead_letter`.
- Lưu idempotency key và command log bằng Supabase.

---

## 3. Kiến trúc hệ thống

Hệ thống gồm 4 service chính:

| Service | Port | Vai trò |
|---|---:|---|
| Webhook Service(NguyenBinhMinh_FBPageAPI) | 3001 | Nhận webhook từ Facebook Page và publish event vào Kafka |
| Core Service | 3002 | Phân tích bình luận, xử lý AI và tạo command |
| Backend API | 3000 | Gọi Facebook Graph API để reply hoặc xóa comment |
| Retry Service | 3003 | Xử lý retry và dead letter cho các command thất bại |

Các thành phần hỗ trợ:

| Thành phần | Vai trò |
|---|---|
| Kafka | Message broker trung gian giữa các service |
| Kafka UI | Theo dõi topic, message và consumer group |
| Gemini API | Phân tích nội dung bình luận và tạo nội dung phản hồi |
| Facebook Graph API | Thực hiện reply hoặc xóa comment trên Facebook Page |
| Supabase | Lưu idempotency key và log xử lý command |

---

## 4. Luồng xử lý tổng quát

```text
Facebook Page
    |
    | HTTP POST Webhook
    v
Webhook Service
    |
    | publish raw_events
    v
Kafka topic: raw_events
    |
    | consume raw_events
    v
Core Service
    |
    | AI classification + decision processing
    | publish reply_commands
    v
Kafka topic: reply_commands
    |
    | consume reply_commands
    v
Backend API
    |
    | call Facebook Graph API
    v
Facebook Page
```

---

## 5. Cấu trúc thư mục

```text
NguyenBinhMinh_FBPageAPI/
│
├── BackendAPI/
│   ├── Controllers/
│   ├── Models/
│   ├── Services/
│   ├── Dockerfile
│   ├── Program.cs
│   └── BackendAPI.csproj
│
├── CoreServices/
│   ├── Controllers/
│   ├── Models/
│   ├── Services/
│   ├── Dockerfile
│   ├── Program.cs
│   └── CoreServices.csproj
│
├── NguyenBinhMinh_FBPageAPI/
│   ├── Controllers/
│   │   └── FacebookWebhookController.cs
│   ├── Models/
│   ├── Services/
│   ├── Dockerfile
│   ├── Program.cs
│   └── NguyenBinhMinh_FBPageAPI.csproj
│
├── RetryService/
│   ├── Controllers/
│   ├── Models/
│   ├── Services/
│   ├── Dockerfile
│   ├── Program.cs
│   └── RetryService.csproj
│
├── docker-compose.yml
├── .dockerignore
├── .gitignore
└── NguyenBinhMinh_FBPageAPI.sln
```

---

## 6. Mô tả các service

### 6.1. Webhook Service

Webhook Service là service tiếp nhận dữ liệu đầu vào từ Facebook Page.

Chức năng:

- Xác thực webhook với Meta.
- Nhận HTTP POST webhook từ Facebook.
- Đọc payload sự kiện bình luận.
- Chuẩn hóa payload thành `NormalizedEvent`.
- Bỏ qua comment do chính Page tạo ra để tránh vòng lặp tự reply.
- Publish event vào Kafka topic `raw_events`.

---

### 6.2. Core Service

Core Service xử lý nghiệp vụ chính sau khi nhận event từ Kafka.

Chức năng:

- Consume Kafka topic `raw_events`.
- Kiểm tra spam bằng rule-based detection.
- Gọi Gemini API để phân tích bình luận.
- Xác định intent, sentiment, spam và nội dung không phù hợp.
- Quyết định hành động xử lý.
- Publish command vào Kafka topic `reply_commands`.

Các action chính:

| Action | Ý nghĩa |
|---|---|
| `reply` | Trả lời bình luận |
| `delete_comment` | Xóa bình luận spam hoặc nội dung xấu |
| `none` | Không thực hiện hành động |

---

### 6.3. Backend API

Backend API là service duy nhất thực hiện gọi Facebook Graph API.

Chức năng:

- Consume Kafka topic `reply_commands`.
- Consume Kafka topic `send_retry`.
- Kiểm tra idempotency key để tránh xử lý trùng.
- Gọi Facebook Graph API để reply comment.
- Gọi Facebook Graph API để xóa comment spam hoặc nội dung không phù hợp.
- Publish message lỗi vào `send_failed` nếu gọi Facebook API thất bại.
- Lưu command log và idempotency key vào Supabase.

---

### 6.4. Retry Service

Retry Service xử lý các command thất bại khi Backend API gọi Facebook Graph API.

Chức năng:

- Consume Kafka topic `send_failed`.
- Tăng `RetryCount`.
- Chờ theo cơ chế exponential backoff.
- Publish lại command vào topic `send_retry`.
- Nếu quá số lần retry, publish vào topic `dead_letter`.


## 7. Supabase

Supabase được dùng để lưu dữ liệu phục vụ chống xử lý trùng và theo dõi command.

Các bảng chính:

| Bảng | Chức năng |
|---|---|
| `idempotency_keys` | Lưu key để tránh reply hoặc xóa comment trùng |
| `command_logs` | Lưu log quá trình xử lý command |

---

## 8. Công nghệ sử dụng

- ASP.NET Core Web API
- C#
- Apache Kafka
- Kafka UI
- Docker
- Facebook Graph API
- Gemini API
- Supabase PostgreSQL
- Swagger UI
- Confluent.Kafka
- Npgsql

## 9. Tác giả

Nguyễn Bình Minh aka sHunOrinn

Thực hành hệ thống quản lý Facebook Page phân tán bằng ASP.NET Core, Kafka, Docker, Gemini API, Facebook Graph API và Supabase.