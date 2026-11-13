#!/usr/bin/env python3
"""
FinTech Core Transaction Engine - Scenario 5 Stress Test
=========================================================
Kịch bản kiểm thử đồng thời (Stress Test Scenario):
1. Khởi tạo Tài khoản A có 1000 VNĐ.
2. Chạy đồng thời 100 request, mỗi request rút 10 VNĐ.
3. Kỳ vọng:
   - Hệ thống cho phép đúng 100 giao dịch thành công.
   - Các luồng xung đột được giải quyết bằng Atomic CAS & Exponential Backoff Retry.
   - Tổng số dư cuối cùng của Tài khoản A = 0.00 VNĐ, không bao giờ bị âm.
   - Bảng LedgerEntries có đúng 100 cặp bút toán hợp lệ (100 Debit, 100 Credit).
   - Kiểm toán đối soát (Reconciliation): Tổng Nợ (Debit) == Tổng Có (Credit).
4. Thử nghiệm rút thêm 1 giao dịch thứ 101 để xác nhận hệ thống từ chối rút quá số dư (Overdraft protection).
"""

import sys
import time
import requests
from concurrent.futures import ThreadPoolExecutor, as_completed

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

BASE_URL = "http://localhost:5000/api"

# ANSI terminal colors
GREEN = "\033[92m"
RED = "\033[91m"
YELLOW = "\033[93m"
CYAN = "\033[96m"
BOLD = "\033[1m"
RESET = "\033[0m"


def reset_environment():
    """Reset Account A with 1,000 VND and clean transactions"""
    print(f"\n{CYAN}[Step 1/5]{RESET} Khởi tạo môi trường kiểm thử...")
    url = f"{BASE_URL}/demo/reset"
    payload = {
        "accountId": "ACC_A",
        "initialBalance": 1000.0,
        "concurrentRequests": 100,
        "amountPerRequest": 10.0
    }
    resp = requests.post(url, json=payload, timeout=10)
    if resp.status_code != 200:
        print(f"{RED}Failed to reset demo: {resp.text}{RESET}")
        sys.exit(1)
    
    acc_resp = requests.get(f"{BASE_URL}/accounts/ACC_A", timeout=10).json()
    print(f"  ✓ Tài khoản ACC_A: Số dư ban đầu = {BOLD}{acc_resp['currentBalance']:,.2f} VNĐ{RESET}, Version = {acc_resp['version']}")


def send_withdraw_request(req_id: int, amount: float = 10.0):
    """Gửi một request rút tiền với CAS retry bảo vệ"""
    url = f"{BASE_URL}/transactions/withdraw"
    payload = {
        "accountId": "ACC_A",
        "amount": amount,
        "description": f"Stress test withdrawal #{req_id}",
        "maxRetries": 30
    }
    t_start = time.perf_counter()
    try:
        resp = requests.post(url, json=payload, timeout=30)
        t_elapsed = (time.perf_counter() - t_start) * 1000
        return {
            "id": req_id,
            "status_code": resp.status_code,
            "data": resp.json() if resp.status_code in [200, 400, 409] else {"error": resp.text},
            "elapsed_ms": t_elapsed
        }
    except Exception as e:
        t_elapsed = (time.perf_counter() - t_start) * 1000
        return {
            "id": req_id,
            "status_code": 500,
            "data": {"error": str(e)},
            "elapsed_ms": t_elapsed
        }


def run_concurrent_stress_test(num_requests: int = 100, amount: float = 10.0):
    """Chạy đồng thời num_requests rút tiền"""
    print(f"\n{CYAN}[Step 2/5]{RESET} Kích hoạt {BOLD}{num_requests}{RESET} luồng đồng thời (concurrent threads), mỗi luồng rút {amount} VNĐ...")
    
    t_start_all = time.perf_counter()
    results = []
    
    with ThreadPoolExecutor(max_workers=min(num_requests, 50)) as executor:
        futures = [executor.submit(send_withdraw_request, i + 1, amount) for i in range(num_requests)]
        for f in as_completed(futures):
            results.append(f.result())
            
    total_time_ms = (time.perf_counter() - t_start_all) * 1000
    return results, total_time_ms


def analyze_results(results, total_time_ms: float):
    """Phân tích kết quả của 100 request"""
    print(f"\n{CYAN}[Step 3/5]{RESET} Phân tích kết quả thực thi đồng thời...")
    
    success_count = sum(1 for r in results if r["status_code"] == 200)
    fail_count = sum(1 for r in results if r["status_code"] != 200)
    
    total_retries = sum(r["data"].get("retriesUsed", 0) for r in results if r["status_code"] == 200)
    requests_with_retries = sum(1 for r in results if r["data"].get("retriesUsed", 0) > 0)
    
    latencies = [r["elapsed_ms"] for r in results]
    avg_latency = sum(latencies) / len(latencies) if latencies else 0
    p95_latency = sorted(latencies)[int(len(latencies) * 0.95)] if latencies else 0
    
    print(f"  • Tổng thời gian xử lý 100 request: {BOLD}{total_time_ms:.2f} ms{RESET}")
    print(f"  • Số request thành công (HTTP 200): {GREEN}{success_count}/100{RESET}")
    print(f"  • Số request thất bại:              {RED if fail_count > 0 else GREEN}{fail_count}/100{RESET}")
    print(f"  • Số request gặp xung đột CAS:      {YELLOW}{requests_with_retries}/100{RESET} ({total_retries} lượt retry được tự động backoff)")
    print(f"  • Độ trễ trung bình:                {avg_latency:.2f} ms (P95: {p95_latency:.2f} ms)")
    
    assert success_count == 100, f"Kỳ vọng 100 request thành công nhưng chỉ có {success_count}"


def verify_account_and_ledger():
    """Kiểm tra số dư cuối cùng và tính toàn vẹn sổ cái kép"""
    print(f"\n{CYAN}[Step 4/5]{RESET} Kiểm tra số dư tài khoản và Đối soát Sổ cái kép (Ledger Audit)...")
    
    # 1. Kiểm tra số dư Tài khoản A
    acc = requests.get(f"{BASE_URL}/accounts/ACC_A", timeout=10).json()
    final_balance = acc["currentBalance"]
    version = acc["version"]
    
    print(f"  • Số dư cuối cùng Tài khoản ACC_A: {BOLD}{final_balance:,.2f} VNĐ{RESET}")
    print(f"  • Phiên bản CAS Version hiện tại:  {BOLD}{version}{RESET}")
    
    if final_balance == 0.0:
        print(f"  {GREEN}✓ ĐẠT CHUẨN: Số dư cuối cùng chính xác bằng 0.00 VNĐ (Không âm, không thất thoát tiền){RESET}")
    else:
        print(f"  {RED}✗ LỖI: Số dư cuối cùng = {final_balance} (Kỳ vọng 0.00 VNĐ){RESET}")
        sys.exit(1)
        
    # 2. Gọi API Đối soát sổ cái (Audit Reconciliation)
    audit = requests.get(f"{BASE_URL}/audit/reconcile", timeout=10).json()
    print(f"\n  [Kết quả Đối soát Sổ cái Kép - VAS/IFRS Audit]:")
    print(f"  • Tổng Nợ (Total Debits):   {BOLD}{audit['totalDebits']:,.2f} VNĐ{RESET}")
    print(f"  • Tổng Có (Total Credits):  {BOLD}{audit['totalCredits']:,.2f} VNĐ{RESET}")
    print(f"  • Độ lệch chênh lệch:       {BOLD}{audit['imbalanceDelta']:,.2f} VNĐ{RESET}")
    print(f"  • Tổng số bút toán sổ cái:  {BOLD}{audit['totalLedgerEntries']}{RESET} (100 Debit + 100 Credit = 200 bản ghi)")
    print(f"  • Có tài khoản bị âm:       {RED if audit['anyNegativeBalances'] else GREEN}{audit['anyNegativeBalances']}{RESET}")
    print(f"  • Trạng thái cân bằng:      {GREEN if audit['isOverallBalanced'] else RED}{'CÂN BẰNG TUYỆT ĐỐI (100% BALANCED)' if audit['isOverallBalanced'] else 'BỊ LỆCH SỔ CÁI'}{RESET}")
    
    assert audit["isOverallBalanced"] is True, "Sổ cái kép không cân bằng!"
    assert audit["imbalanceDelta"] == 0.0, "Có chênh lệch tiền giữa Nợ và Có!"
    assert audit["totalLedgerEntries"] == 200, f"Kỳ vọng đúng 200 bút toán (100 cặp) nhưng có {audit['totalLedgerEntries']}"


def test_overdraft_protection():
    """Thử nghiệm rút thêm 1 request thứ 101 khi số dư đã hết (Overdraft protection)"""
    print(f"\n{CYAN}[Step 5/5]{RESET} Kiểm tra khả năng chặn thấu chi (Overdraft Protection) với request thứ 101...")
    res = send_withdraw_request(101, amount=10.0)
    
    if res["status_code"] == 400 and res["data"].get("code") == "INSUFFICIENT_FUNDS":
        print(f"  {GREEN}✓ ĐẠT CHUẨN: Request thứ 101 bị từ chối thành công với mã lỗi INSUFFICIENT_FUNDS.{RESET}")
        print(f"    Chi tiết phản hồi: {res['data'].get('error')}")
    else:
        print(f"  {RED}✗ LỖI: Request thứ 101 không bị từ chối đúng cách! Status: {res['status_code']}, Data: {res['data']}{RESET}")
        sys.exit(1)


def main():
    print("=" * 75)
    print(f"{BOLD}FINTECH CORE TRANSACTION ENGINE - STRESS TEST SCENARIO 5{RESET}")
    print("Mô hình: Atomic CAS (Compare-And-Swap) + Sổ cái kép (Double-Entry Ledger)")
    print("=" * 75)
    
    # Kiểm tra server đang chạy
    try:
        r = requests.get(f"{BASE_URL}/accounts", timeout=3)
        if r.status_code != 200:
            raise Exception("API returned status != 200")
    except Exception as e:
        print(f"{RED}Không thể kết nối đến Web API tại {BASE_URL}.{RESET}")
        print("Vui lòng khởi động server bằng lệnh:")
        print(f"  {YELLOW}dotnet run --project src/FinTechEngine.Api --urls \"http://localhost:5000\"{RESET}")
        sys.exit(1)
        
    reset_environment()
    results, total_time = run_concurrent_stress_test(num_requests=100, amount=10.0)
    analyze_results(results, total_time)
    verify_account_and_ledger()
    test_overdraft_protection()
    
    print("\n" + "=" * 75)
    print(f"{GREEN}{BOLD}KẾT QUẢ CUỐI CÙNG: TẤT CẢ 5 BƯỚC KIỂM THỬ ĐÃ VƯỢT QUA XUẤT SẮC 100%!{RESET}")
    print("• Ngăn chặn hoàn toàn Double Spending & Race Condition.")
    print("• Sổ cái kép đối soát cân bằng tuyệt đối giữa Nợ và Có.")
    print("• Độ trễ tối ưu và cơ chế retry mượt mà qua Exponential Backoff.")
    print("=" * 75 + "\n")


if __name__ == "__main__":
    main()
