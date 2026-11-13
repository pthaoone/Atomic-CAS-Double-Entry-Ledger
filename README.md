# ⚡ FinTech Core Transaction Engine

> **Lõi xử lý giao dịch tài chính đa luồng an toàn tuyệt đối** ứng dụng cơ chế **Atomic CAS (Compare-And-Swap / Optimistic Concurrency Control)** kết hợp mô hình **Sổ cái kế toán kép (Double-Entry Ledger)** nhằm đảm bảo chuẩn **ACID**, ngăn chặn triệt để **Race Condition**, **Double-Spending** và **Lost Updates**.

---

## 📑 Mục lục
1. [Ý tưởng & Giải pháp FinTech](#1-ý-tưởng--giải-pháp-fintech)
2. [Kiến trúc Công nghệ (Technology Stack)](#2-kiến-trúc-công-nghệ-technology-stack)
3. [Mô hình Dữ liệu & Thiết kế Hệ thống](#3-mô-hình-dữ-liệu--thiết-kế-hệ-thống)
4. [Cơ chế Atomic CAS & Exponential Backoff](#4-cơ-chế-atomic-cas--exponential-backoff)
5. [Nguyên lý Sổ cái Kép (Double-Entry Ledger) & Đối soát](#5-nguyên-lý-sổ-cái-kép-double-entry-ledger--đối-soát)
6. [Hướng dẫn Khởi chạy Nhanh (Quick Start)](#6-hướng-dẫn-khởi-chạy-nhanh-quick-start)
7. [Kịch bản Kiểm thử Đồng thời (Stress Test - Scenario 5)](#7-kịch-bản-kiểm-thử-đồng-thời-stress-test---scenario-5)
8. [So sánh Thực nghiệm: CAS vs Không CAS (Race Condition Showdown)](#8-so-sánh-thực-nghiệm-cas-vs-không-cas)
9. [Tài liệu API (API Endpoints)](#9-tài-liệu-api-api-endpoints)
10. [Triển khai Docker & Cloud](#10-triển-khai-docker--cloud)

---

## 1. Ý tưởng & Giải pháp FinTech

Trong các hệ thống tài chính/ví điện tử, thách thức lớn nhất khi lưu lượng giao dịch tăng cao là **tranh chấp tài nguyên đồng thời (concurrency contention)**. 
- Nếu dùng **Pessimistic Locking (`SELECT FOR UPDATE`)**, toàn bộ hàng/bảng bị khóa, dẫn đến nghẽn cổ chai (bottleneck), độ trễ cao và dễ rơi vào deadlock.
- Nếu không khóa mà dùng ORM đọc-sửa-ghi thông thường (Naive Read-Modify-Write), các luồng ghi đè số dư lên nhau (**Lost Updates**), dẫn đến hiện tượng **Double-Spending** (tiêu tiền 2 lần) hoặc số dư bị âm.

### Giải pháp của FinTech Core Transaction Engine:
- **Atomic CAS (Compare-And-Swap)**: Cho phép các luồng xử lý song song ở tốc độ tối đa. Khi ghi xuống DB, câu lệnh `UPDATE` nguyên tử kiểm tra điều kiện `WHERE AccountId = @Id AND Version = @OldVersion`. Nếu khớp, tăng `Version = Version + 1` và cập nhật số dư; nếu không khớp (đã bị luồng khác thay đổi), câu lệnh trả về `RowsAffected == 0` và giao dịch lập tức bị rollback.
- **Tự động Retry với Exponential Backoff & Jitter**: Giao dịch bị xung đột version không bị vứt bỏ mà tự động xếp lịch thử lại với độ trễ tăng dần ngẫu nhiên, giúp giải tỏa áp lực CPU và hoàn tất 100% giao dịch hợp lệ.
- **Sổ cái kế toán kép (Double-Entry Bookkeeping)**: Không bao giờ cập nhật số dư đơn thuần mà luôn ghi nhận đồng thời cặp bút toán cân bằng: **Nợ (Debit) tài khoản A = Có (Credit) tài khoản B**. Đảm bảo tổng tài sản hệ thống luôn bảo toàn, không có "tiền ảo" tự sinh ra hay biến mất.

---

## 2. Kiến trúc Công nghệ (Technology Stack)

- **Backend Platform**: C# .NET 10 (ASP.NET Core Web API) với hiệu năng xử lý đa luồng cao (High Throughput / Asynchronous I/O).
- **Data Access & Concurrency**:
  - Entity Framework Core 10 + Dapper / Parametric Raw SQL execution cho câu lệnh Atomic CAS.
  - SQLite Write-Ahead Logging (WAL mode) với Busy Timeout 15s cho môi trường zero-dependency demo; sẵn sàng chuyển đổi PostgreSQL / SQL Server cho production.
- **Frontend / Dashboard**: HTML5 / CSS3 (Dark Fintech Glassmorphism UI) tích hợp sẵn trong Web API (`wwwroot`), hiển thị số dư, biểu đồ giao dịch trực tiếp, nút chạy stress test 1-click và modal đối soát sổ cái.
- **API Documentation**: Scalar OpenAPI Engine tích hợp tại `/scalar/v1`.
- **Automated Stress Testing**: Bộ script Python 3 đa luồng (`ThreadPoolExecutor`) kiểm thử kịch bản 100 concurrent requests.
- **Containerization**: Multi-stage `Dockerfile` và `docker-compose.yml`.

---

## 3. Mô hình Dữ liệu & Thiết kế Hệ thống

### Bảng `Accounts`
| Cột | Kiểu dữ liệu | Mô tả |
| :--- | :--- | :--- |
| `AccountId` | `VARCHAR(64)` (PK) | Mã tài khoản (VD: `ACC_A`, `ACC_B`, `SYSTEM_CASH_VAULT`) |
| `AccountName` | `VARCHAR(128)` | Tên chủ tài khoản hoặc quỹ hệ thống |
| `Type` | `VARCHAR(32)` | `Customer`, `SystemVault`, `Reserve` |
| `CurrentBalance` | `DECIMAL(18,4)` | Số dư hiện tại (luôn >= 0 với tài khoản khách hàng) |
| `Version` | `BIGINT` | **Khóa phiên bản CAS quan trọng nhất**, tăng +1 mỗi lần cập nhật |
| `CreatedAt` / `UpdatedAt` | `DATETIME` | Dấu thời gian kiểm toán |

### Bảng `Transactions`
| Cột | Kiểu dữ liệu | Mô tả |
| :--- | :--- | :--- |
| `TxId` | `VARCHAR(64)` (PK) | Mã định danh duy nhất của giao dịch (GUID) |
| `Timestamp` | `DATETIME` | Thời điểm thực hiện giao dịch |
| `Type` | `VARCHAR(32)` | `Deposit`, `Withdrawal`, `Transfer` |
| `Amount` | `DECIMAL(18,4)` | Số tiền giao dịch |
| `Status` | `VARCHAR(32)` | `Pending`, `Completed`, `Failed`, `RolledBack` |
| `SourceAccountId` | `VARCHAR(64)` | Tài khoản nguồn |
| `DestinationAccountId` | `VARCHAR(64)` | Tài khoản đích |
| `RetryCount` | `INT` | Số lần CAS retry trước khi thành công |
| `LatencyMs` | `BIGINT` | Thời gian thực thi (milliseconds) |

### Bảng `LedgerEntries` (Sổ cái bất biến - Immutable Audit Trail)
| Cột | Kiểu dữ liệu | Mô tả |
| :--- | :--- | :--- |
| `EntryId` | `VARCHAR(64)` (PK) | Mã bút toán |
| `TxId` | `VARCHAR(64)` (FK) | Liên kết với `Transactions` |
| `AccountId` | `VARCHAR(64)` (FK) | Tài khoản chịu tác động |
| `Direction` | `VARCHAR(16)` | `Debit` (Ghi Nợ) hoặc `Credit` (Ghi Có) |
| `Amount` | `DECIMAL(18,4)` | Số tiền ghi sổ |
| `BalanceAfter` | `DECIMAL(18,4)` | Snapshot số dư ngay sau bút toán |
| `CreatedAt` | `DATETIME` | Dấu thời gian |

---

## 4. Cơ chế Atomic CAS & Exponential Backoff

### Quy trình thực thi giao dịch:
```
[Client API Request]
       │
       ▼
1. Đọc số dư & version hiện tại:
   Source = (Balance: S, Version: V_S)
   Dest   = (Balance: D, Version: V_D)
       │
       ▼
2. Kiểm tra Business Rules:
   - S >= Amount? (Chặn thấu chi / Overdraft)
   - Tính toán: New_S = S - Amount; New_D = D + Amount
       │
       ▼
3. Mở Database Transaction (ACID)
       │
       ├──> Ghi nhận Transaction Record (Status: Completed)
       ├──> Ghi nhận 2 bản ghi Sổ cái kép (Debit S, Credit D)
       │
       ▼
4. Thực thi câu lệnh CAS tại tầng CSDL:
   UPDATE Accounts
   SET CurrentBalance = @New_S, Version = Version + 1, UpdatedAt = @Now
   WHERE AccountId = @SourceId AND Version = @V_S;

   UPDATE Accounts
   SET CurrentBalance = @New_D, Version = Version + 1, UpdatedAt = @Now
   WHERE AccountId = @DestId AND Version = @V_D;
       │
       ▼
5. Kiểm tra RowsAffected:
   ├── RowsAffected == 2?
   │     └──> COMMIT TRANSACTION ✅ (Giao dịch hoàn tất thành công!)
   │
   └── RowsAffected < 2? (Xung đột tài nguyên xảy ra do luồng khác đã thay đổi version)
         └──> ROLLBACK TRANSACTION NGAY LẬP TỨC ❌
               │
               ▼
         Tính toán Exponential Backoff:
         Delay = min(250ms, 8ms * 1.4^(attempt-1)) + Jitter(2..15ms)
               │
               ▼
         Thử lại (Retry) với dữ liệu mới từ Bước 1
```

---

## 5. Nguyên lý Sổ cái Kép (Double-Entry Ledger) & Đối soát

Mọi biến động dòng tiền đều tuân thủ nguyên tắc kế toán kép:
1. **Chuyển khoản (Transfer A -> B, số tiền X)**:
   - Ghi Nợ (Debit) Tài khoản A: `X` VNĐ (giảm số dư tài khoản A)
   - Ghi Có (Credit) Tài khoản B: `X` VNĐ (tăng số dư tài khoản B)
2. **Nạp tiền (Deposit vào A, số tiền X)**:
   - Ghi Nợ (Debit) `SYSTEM_CASH_VAULT`: `X` VNĐ
   - Ghi Có (Credit) Tài khoản A: `X` VNĐ
3. **Rút tiền (Withdraw từ A, số tiền X)**:
   - Ghi Nợ (Debit) Tài khoản A: `X` VNĐ
   - Ghi Có (Credit) `SYSTEM_CASH_VAULT`: `X` VNĐ

### Công thức Đối soát (Audit Invariants):
$$\sum \text{Debit.Amount} = \sum \text{Credit.Amount}$$
$$\text{Imbalance Delta} = |\sum \text{Debit} - \sum \text{Credit}| = 0.00 \text{ VNĐ}$$

Hệ thống cung cấp API `/api/audit/reconcile` để đối soát tự động toán học toàn bộ hệ thống.

---

## 6. Hướng dẫn Khởi chạy Nhanh (Quick Start)

### Yêu cầu hệ thống:
- .NET SDK 10 (hoặc .NET 9)
- Python 3.10+ (để chạy script test)

### Bước 1: Khởi động Web API & Dashboard
```powershell
cd d:\CNTCUD
dotnet run --project src/FinTechEngine.Api/FinTechEngine.Api.csproj --urls "http://localhost:5000"
```

### Bước 2: Truy cập Giao diện Web Dashboard & API Docs
- **Interactive Fintech Dashboard**: [http://localhost:5000](http://localhost:5000)
- **Scalar API Documentation**: [http://localhost:5000/scalar/v1](http://localhost:5000/scalar/v1)

---

## 7. Kịch bản Kiểm thử Đồng thời (Stress Test - Scenario 5)

Kịch bản yêu cầu theo đề bài:
1. Khởi tạo **Tài khoản A** có đúng **1,000 VNĐ**.
2. Bắn đồng thời (**concurrently**) **100 request**, mỗi request rút **10 VNĐ**.
3. **Kết quả kỳ vọng**:
   - Đúng 100 giao dịch đầu tiên thành công.
   - Các luồng tranh chấp version được tự động retry và xử lý an toàn.
   - Số dư cuối cùng = **0.00 VNĐ** (không bao giờ âm, không mất tiền).
   - Bảng `LedgerEntries` có đúng **200 bản ghi (100 cặp Debit/Credit)**.
   - Thử tiếp giao dịch thứ 101: Bị từ chối ngay lập tức với lỗi `INSUFFICIENT_FUNDS`.

### Chạy kiểm thử tự động bằng Python:
```powershell
python tests/stress_test.py
```

### Kết quả đầu ra thực tế:
```
===========================================================================
FINTECH CORE TRANSACTION ENGINE - STRESS TEST SCENARIO 5
Mô hình: Atomic CAS (Compare-And-Swap) + Sổ cái kép (Double-Entry Ledger)
===========================================================================

[Step 1/5] Khởi tạo môi trường kiểm thử...
  ✓ Tài khoản ACC_A: Số dư ban đầu = 1,000.00 VNĐ, Version = 1

[Step 2/5] Kích hoạt 100 luồng đồng thời (concurrent threads), mỗi luồng rút 10.0 VNĐ...

[Step 3/5] Phân tích kết quả thực thi đồng thời...
  • Tổng thời gian xử lý 100 request: 2212.48 ms
  • Số request thành công (HTTP 200): 100/100
  • Số request thất bại:              0/100
  • Số request gặp xung đột CAS:      44/100 (52 lượt retry được tự động backoff)
  • Độ trễ trung bình:                781.56 ms (P95: 1662.33 ms)

[Step 4/5] Kiểm tra số dư tài khoản và Đối soát Sổ cái kép (Ledger Audit)...
  • Số dư cuối cùng Tài khoản ACC_A: 0.00 VNĐ
  • Phiên bản CAS Version hiện tại:  101
  ✓ ĐẠT CHUẨN: Số dư cuối cùng chính xác bằng 0.00 VNĐ (Không âm, không thất thoát tiền)

  [Kết quả Đối soát Sổ cái Kép - VAS/IFRS Audit]:
  • Tổng Nợ (Total Debits):   1,000.00 VNĐ
  • Tổng Có (Total Credits):  1,000.00 VNĐ
  • Độ lệch chênh lệch:       0.00 VNĐ
  • Tổng số bút toán sổ cái:  200 (100 Debit + 100 Credit = 200 bản ghi)
  • Có tài khoản bị âm:       False
  • Trạng thái cân bằng:      CÂN BẰNG TUYỆT ĐỐI (100% BALANCED)

[Step 5/5] Kiểm tra khả năng chặn thấu chi (Overdraft Protection) với request thứ 101...
  ✓ ĐẠT CHUẨN: Request thứ 101 bị từ chối thành công với mã lỗi INSUFFICIENT_FUNDS.
    Chi tiết phản hồi: Insufficient funds in account 'ACC_A'. Current balance: 0.00, Requested: 10.00.

===========================================================================
KẾT QUẢ CUỐI CÙNG: TẤT CẢ 5 BƯỚC KIỂM THỬ ĐÃ VƯỢT QUA XUẤT SẮC 100%!
===========================================================================
```

---

## 8. So sánh Thực nghiệm: CAS vs Không CAS

Chạy so sánh thực tế giữa Engine có bảo vệ CAS và Code thông thường không có CAS:
```powershell
python tests/benchmark_comparison.py
```

### Bảng đối sánh thực tế:
| Tiêu chí so sánh | Có Atomic CAS (Engine) | Không có CAS (Naive ORM) |
| :--- | :--- | :--- |
| **Số request thành công** | **100/100** | 100/100 |
| **Số dư kỳ vọng** | `0.00 VNĐ` | `0.00 VNĐ` |
| **Số dư thực tế sau 100 tx** | **`0.00 VNĐ` (Chính xác 100%)** | **`930.00 VNĐ` (Bị sai lệch nghiêm trọng!)** |
| **Lỗi Lost Updates** | **Không** (Bị triệt tiêu hoàn toàn) | **Có** (Các luồng đè số dư lên nhau) |
| **Nguy cơ Double-Spending** | **0%** | **Cực kỳ cao** |
| **Sổ cái kép đối soát** | **Cân bằng tuyệt đối (Delta = 0)** | **Lệch hoàn toàn so với số dư thực** |

---

## 9. Tài liệu API (API Endpoints)

| Method | Endpoint | Mô tả |
| :--- | :--- | :--- |
| `GET` | `/api/accounts` | Danh sách tất cả tài khoản, số dư và CAS version |
| `GET` | `/api/accounts/{id}` | Lấy chi tiết tài khoản |
| `POST` | `/api/accounts` | Tạo tài khoản mới |
| `GET` | `/api/accounts/{id}/ledger` | Lịch sử bút toán sổ cái của tài khoản |
| `POST` | `/api/transactions/deposit` | Nạp tiền (CAS Protected) |
| `POST` | `/api/transactions/withdraw` | Rút tiền (CAS Protected + Overdraft check) |
| `POST` | `/api/transactions/transfer` | Chuyển khoản A -> B (CAS Protected trên cả 2 tài khoản) |
| `GET` | `/api/transactions` | Danh sách lịch sử giao dịch gần nhất |
| `GET` | `/api/audit/reconcile` | **Chạy thuật toán đối soát toán học Sổ cái kép** |
| `GET` | `/api/metrics` | Lấy chỉ số thời gian thực: TPS, độ trễ, số xung đột CAS, số lần retry |
| `POST` | `/api/demo/reset` | Đặt lại môi trường test (Tài khoản A = 1,000 VNĐ) |
| `POST` | `/api/unsafe/withdraw` | Rút tiền không dùng CAS (Dành cho benchmark so sánh lỗi) |

---

## 10. Triển khai Docker & Cloud

Dự án được cấu hình sẵn sàng đóng gói và scale-up trên Cloud thông qua Docker:

```powershell
# Build Docker image
docker build -t fintech-core-engine .

# Hoặc khởi chạy bằng Docker Compose
docker compose up -d
```

---

## 🏛️ Kết luận & Giá trị Nghiệp vụ (Business Value)
Sản phẩm demo này giải quyết trọn vẹn và thực nghiệm hóa toàn bộ các yêu cầu của tài liệu kiến trúc FinTech:
1. **Tuyệt đối tuân thủ ACID**: Đảm bảo toàn vẹn dữ liệu trong mọi tình huống tranh chấp.
2. **Không khóa bảng (Lock-free Read)**: Đạt TPS cao, không gây tắc nghẽn hệ thống.
3. **Chuẩn mực Kiểm toán Quốc tế**: Mô hình Sổ cái kép tạo ra Audit Trail bất biến, minh bạch 100% dòng tiền.
"# Atomic-CAS-Double-Entry-Ledger" 
