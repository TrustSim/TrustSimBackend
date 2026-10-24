"""
=============================================================================
TrustSimBackend - Automated API Filter & Integration Test Suite
=============================================================================
Script tự động kiểm thử toàn bộ các API đã sửa lỗi và chuẩn hóa bộ lọc theo
tài liệu: docs/feedbacks/response/20261024_frontend_api_filter_integration_guide.md

Các nhóm kiểm thử:
  1. Authentication & Token Extraction (Admin & User)
  2. P0: Revenue History (Database Query Filter & Whitelist Validation)
  3. P0: Unified Statement (SourceType Routing, Direction, Status)
  4. P1: Documents API (PagedResult<T>, Keyword, Status, DocumentType, Sort)
  5. P1: AI Consultant Sessions & Messages (PagedResult<T>)
  6. P1: Auction Bids History (PagedResult<T>, Min/Max Amount, Sort)
  7. P1: SIM Ownerships & Status Sorting (SimNumber.SimStatus mapping)
  8. P1: Admin Financial Filters (UserId, WalletId)
  9. P2: SIM Valuation History, Pending Settlements, KYC History (PagedResult<T>)
 10. P1 Export: 7 Excel Export APIs (đồng bộ Filter Model, binary response)
=============================================================================
"""

import sys
import os
import json
import time
from typing import Dict, Any, Optional, List
import requests

# Fix Windows console UTF-8 output
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

BASE_URL = os.environ.get("TRUSTSIM_API_URL", "http://localhost:5130")

class Colors:
    GREEN = "\033[92m"
    RED = "\033[91m"
    YELLOW = "\033[93m"
    CYAN = "\033[96m"
    BOLD = "\033[1m"
    RESET = "\033[0m"

class TestResult:
    def __init__(self, name: str, passed: bool, message: str, details: str = ""):
        self.name = name
        self.passed = passed
        self.message = message
        self.details = details

results: List[TestResult] = []

def record(name: str, passed: bool, message: str, details: str = ""):
    results.append(TestResult(name, passed, message, details))
    icon = f"{Colors.GREEN}✔ PASS{Colors.RESET}" if passed else f"{Colors.RED}✖ FAIL{Colors.RESET}"
    print(f"[{icon}] {Colors.BOLD}{name}{Colors.RESET}: {message}", flush=True)
    if not passed and details:
        print(f"       {Colors.YELLOW}Chi tiết lỗi: {details}{Colors.RESET}", flush=True)

def login(username_or_email: str, password: str = "Password123@") -> Optional[str]:
    """Đăng nhập để lấy JWT Token"""
    url = f"{BASE_URL}/api/auth/login"
    payload = {"userNameOrEmail": username_or_email, "password": password}
    try:
        resp = requests.post(url, json=payload, timeout=10)
        if resp.status_code == 200:
            data = resp.json().get("data", {})
            return data.get("token")
        else:
            print(f"Failed to login {username_or_email}: HTTP {resp.status_code} - {resp.text}")
    except Exception as ex:
        print(f"Exception during login: {ex}")
    return None

def verify_paged_result(data: Any, expected_min_items: int = 0) -> tuple[bool, str]:
    """Kiểm tra cấu trúc PagedResult<T>"""
    if not isinstance(data, dict):
        return False, f"Expected dict response but got {type(data)}"
    
    required_keys = ["items", "pageIndex", "pageSize", "totalCount", "totalPages"]
    for k in required_keys:
        if k not in data:
            return False, f"Missing key '{k}' in PagedResult"
            
    if not isinstance(data["items"], list):
        return False, f"'items' is not a list, got {type(data['items'])}"
        
    if len(data["items"]) < expected_min_items:
        return False, f"Expected at least {expected_min_items} items, got {len(data['items'])}"
        
    return True, f"Valid PagedResult (totalCount={data['totalCount']}, items={len(data['items'])})"

# ─────────────────────────────────────────────────────────────────────────────
# MAIN TEST EXECUTION
# ─────────────────────────────────────────────────────────────────────────────

def run_all_tests():
    print(f"\n{Colors.CYAN}{Colors.BOLD}{'='*80}")
    print(f" BẮT ĐẦU CHẠY BỘ KIỂM THỬ TỰ ĐỘNG BỘ LỌC VÀ PHÂN TRANG API")
    print(f" Target API: {BASE_URL}")
    print(f"{'='*80}{Colors.RESET}\n")

    # ─────────────────────────────────────────────────────────────────────────
    # 1. AUTHENTICATION
    # ─────────────────────────────────────────────────────────────────────────
    print(f"{Colors.BOLD}--- 1. XÁC THỰC VÀ LẤY ACCESS TOKEN ---{Colors.RESET}")
    admin_token = login("admin", "Password123@")
    user_token = login("seller_vip_phuquy", "Password123@")

    record("Auth: Login Admin", admin_token is not None, "Đăng nhập tài khoản admin thành công" if admin_token else "Thất bại")
    record("Auth: Login User", user_token is not None, "Đăng nhập tài khoản user (seller_vip_phuquy) thành công" if user_token else "Thất bại")

    admin_headers = {"Authorization": f"Bearer {admin_token}"} if admin_token else {}
    user_headers = {"Authorization": f"Bearer {user_token}"} if user_token else {}

    # ─────────────────────────────────────────────────────────────────────────
    # 2. P0: REVENUE HISTORY
    # ─────────────────────────────────────────────────────────────────────────
    print(f"\n{Colors.BOLD}--- 2. P0: GET /api/Wallets/admin/financial/revenue-history ---{Colors.RESET}")
    # 2.1 Mặc định
    r = requests.get(f"{BASE_URL}/api/Wallets/admin/financial/revenue-history?pageIndex=1&pageSize=10", headers=admin_headers)
    if r.status_code == 200:
        valid, msg = verify_paged_result(r.json().get("data"))
        record("P0: Revenue History Default Query", valid, f"HTTP 200, {msg}")
    else:
        record("P0: Revenue History Default Query", False, f"HTTP {r.status_code}", r.text)

    # 2.2 Whitelist valid transactionType
    r = requests.get(f"{BASE_URL}/api/Wallets/admin/financial/revenue-history?transactionType=ESCROW_RELEASE", headers=admin_headers)
    record("P0: Revenue History Whitelist (ESCROW_RELEASE)", r.status_code == 200, f"HTTP {r.status_code}")

    # 2.3 Whitelist invalid transactionType (phải trả 400 Bad Request)
    r = requests.get(f"{BASE_URL}/api/Wallets/admin/financial/revenue-history?transactionType=INVALID_REVENUE_TYPE", headers=admin_headers)
    record("P0: Revenue History Invalid Whitelist Check (Expect 400)", r.status_code == 400, f"HTTP {r.status_code} (Chặn đúng loại không thuộc whitelist)")

    # ─────────────────────────────────────────────────────────────────────────
    # 3. P0: UNIFIED STATEMENT
    # ─────────────────────────────────────────────────────────────────────────
    print(f"\n{Colors.BOLD}--- 3. P0: GET /api/Wallets/statement ---{Colors.RESET}")
    # 3.1 Tất cả nguồn (Hợp nhất)
    r = requests.get(f"{BASE_URL}/api/Wallets/statement?pageIndex=1&pageSize=10", headers=user_headers)
    if r.status_code == 200:
        valid, msg = verify_paged_result(r.json().get("data"))
        record("P0: Unified Statement (All Sources)", valid, f"HTTP 200, {msg}")
    else:
        record("P0: Unified Statement (All Sources)", False, f"HTTP {r.status_code}", r.text)

    # 3.2 Lọc sourceType=WALLET
    r = requests.get(f"{BASE_URL}/api/Wallets/statement?sourceType=WALLET", headers=user_headers)
    record("P0: Statement with sourceType=WALLET", r.status_code == 200, f"HTTP {r.status_code}")

    # 3.3 Lọc sourceType=AUCTION
    r = requests.get(f"{BASE_URL}/api/Wallets/statement?sourceType=AUCTION", headers=user_headers)
    record("P0: Statement with sourceType=AUCTION", r.status_code == 200, f"HTTP {r.status_code}")

    # 3.4 Lọc direction=CREDIT
    r = requests.get(f"{BASE_URL}/api/Wallets/statement?direction=CREDIT", headers=user_headers)
    record("P0: Statement with direction=CREDIT", r.status_code == 200, f"HTTP {r.status_code}")

    # 3.5 Invalid sourceType (Expect 400)
    r = requests.get(f"{BASE_URL}/api/Wallets/statement?sourceType=UNKNOWN_SOURCE", headers=user_headers)
    record("P0: Statement Invalid sourceType Check (Expect 400)", r.status_code == 400, f"HTTP {r.status_code} (Chặn đúng nguồn không hợp lệ)")

    # ─────────────────────────────────────────────────────────────────────────
    # 4. P1: DOCUMENTS API (PagedResult<DocumentDto>)
    # ─────────────────────────────────────────────────────────────────────────
    print(f"\n{Colors.BOLD}--- 4. P1: GET /api/Documents ---{Colors.RESET}")
    r = requests.get(f"{BASE_URL}/api/Documents?pageIndex=1&pageSize=5", headers=user_headers)
    if r.status_code == 200:
        valid, msg = verify_paged_result(r.json().get("data"))
        record("P1: Documents PagedResult Structure", valid, f"HTTP 200, {msg}")
    else:
        record("P1: Documents PagedResult Structure", False, f"HTTP {r.status_code}", r.text)

    # Lọc keyword & status
    r = requests.get(f"{BASE_URL}/api/Documents?keyword=quy&status=INDEXED&sortBy=2&sortOrder=1", headers=user_headers)
    record("P1: Documents Filter (Keyword, Status, Sort)", r.status_code == 200, f"HTTP {r.status_code}")

    # ─────────────────────────────────────────────────────────────────────────
    # 5. P1: AI CONSULTANT SESSIONS & MESSAGES
    # ─────────────────────────────────────────────────────────────────────────
    print(f"\n{Colors.BOLD}--- 5. P1: GET /api/ai/consultant/sessions & messages ---{Colors.RESET}")
    r = requests.get(f"{BASE_URL}/api/ai/consultant/sessions?pageIndex=1&pageSize=5", headers=user_headers)
    session_id = None
    if r.status_code == 200:
        data = r.json().get("data", {})
        valid, msg = verify_paged_result(data)
        record("P1: AI Consultant Sessions PagedResult", valid, f"HTTP 200, {msg}")
        items = data.get("items", [])
        if items:
            session_id = items[0].get("id") or items[0].get("sessionId")
    else:
        record("P1: AI Consultant Sessions PagedResult", False, f"HTTP {r.status_code}", r.text)

    # Messages
    if session_id:
        r = requests.get(f"{BASE_URL}/api/ai/consultant/sessions/{session_id}/messages?pageIndex=1&pageSize=10", headers=user_headers)
        if r.status_code == 200:
            valid, msg = verify_paged_result(r.json().get("data"))
            record("P1: AI Consultant Messages PagedResult", valid, f"HTTP 200, {msg}")
        else:
            record("P1: AI Consultant Messages PagedResult", False, f"HTTP {r.status_code}", r.text)
    else:
        record("P1: AI Consultant Messages PagedResult", True, "Bỏ qua (Chưa có session nào của user để test sub-messages)")

    # ─────────────────────────────────────────────────────────────────────────
    # 6. P1: AUCTION BIDS (PagedResult<BidDto>)
    # ─────────────────────────────────────────────────────────────────────────
    print(f"\n{Colors.BOLD}--- 6. P1: GET /api/Auctions/{{id}}/bids ---{Colors.RESET}")
    # Lấy 1 auction id từ danh sách auctions
    auction_id = None
    r_auctions = requests.get(f"{BASE_URL}/api/Auctions?pageIndex=1&pageSize=1")
    if r_auctions.status_code == 200:
        items = r_auctions.json().get("data", {}).get("items", [])
        if items:
            auction_id = items[0].get("id")

    if auction_id:
        r = requests.get(f"{BASE_URL}/api/Auctions/{auction_id}/bids?pageIndex=1&pageSize=5&sortBy=1&sortOrder=0", headers=user_headers)
        if r.status_code == 200:
            valid, msg = verify_paged_result(r.json().get("data"))
            record("P1: Auction Bids PagedResult", valid, f"HTTP 200, {msg}")
        else:
            record("P1: Auction Bids PagedResult", False, f"HTTP {r.status_code}", r.text)
    else:
        record("P1: Auction Bids PagedResult", False, "Không tìm thấy phiên đấu giá nào để test bids")

    # ─────────────────────────────────────────────────────────────────────────
    # 7. P1: SIM OWNERSHIPS & SORTING
    # ─────────────────────────────────────────────────────────────────────────
    print(f"\n{Colors.BOLD}--- 7. P1: SIM Ownerships & Status Sorting ---{Colors.RESET}")
    # 7.1 My Ownerships với status
    r = requests.get(f"{BASE_URL}/api/Sims/my-ownerships?status=ACTIVE&pageIndex=1&pageSize=10", headers=user_headers)
    if r.status_code == 200:
        valid, msg = verify_paged_result(r.json().get("data"))
        record("P1: My Sim Ownerships with status filter", valid, f"HTTP 200, {msg}")
    else:
        record("P1: My Sim Ownerships with status filter", False, f"HTTP {r.status_code}", r.text)

    # 7.2 My Sims Sort by Status (SimOwnershipSortBy.Status = 3)
    r = requests.get(f"{BASE_URL}/api/Sims/my-sims?sortBy=3&sortOrder=1", headers=user_headers)
    record("P1: My Sims Sort by Status (Fix Sort Mapping)", r.status_code == 200, f"HTTP {r.status_code}")

    # 7.3 Public Sims Sort by Status (SimSortBy.Status = 4)
    r = requests.get(f"{BASE_URL}/api/Sims?sortBy=4&sortOrder=1")
    record("P1: Public Sims Sort by Status (Fix Sort Mapping)", r.status_code == 200, f"HTTP {r.status_code}")

    # ─────────────────────────────────────────────────────────────────────────
    # 8. P1: ADMIN FINANCIAL FILTERS (UserId, WalletId)
    # ─────────────────────────────────────────────────────────────────────────
    print(f"\n{Colors.BOLD}--- 8. P1: Admin Financial Filters ---{Colors.RESET}")
    # 8.1 Admin withdrawals
    test_user_id = "11000000-0000-0000-0000-000000000003"
    test_wallet_id = "14000000-0000-0000-0000-000000000003"
    r = requests.get(f"{BASE_URL}/api/Wallets/admin/withdrawals?userId={test_user_id}&walletId={test_wallet_id}", headers=admin_headers)
    record("P1: Admin Withdrawals with userId and walletId", r.status_code == 200, f"HTTP {r.status_code}")

    # 8.2 Admin transactions
    r = requests.get(f"{BASE_URL}/api/Wallets/admin/transactions?walletId={test_wallet_id}", headers=admin_headers)
    record("P1: Admin Transactions with walletId", r.status_code == 200, f"HTTP {r.status_code}")

    # ─────────────────────────────────────────────────────────────────────────
    # 9. P2: VALUATION HISTORY, PENDING SETTLEMENTS, KYC HISTORY
    # ─────────────────────────────────────────────────────────────────────────
    print(f"\n{Colors.BOLD}--- 9. P2: Valuation History, Settlements, KYC ---{Colors.RESET}")
    # 9.1 Valuation History
    sim_id = None
    r_sims = requests.get(f"{BASE_URL}/api/Sims?pageIndex=1&pageSize=1")
    if r_sims.status_code == 200:
        s_items = r_sims.json().get("data", {}).get("items", [])
        if s_items:
            sim_id = s_items[0].get("id")

    if sim_id:
        r = requests.get(f"{BASE_URL}/api/ai/valuation/sim/{sim_id}/history?pageIndex=1&pageSize=5", headers=user_headers)
        if r.status_code == 200:
            valid, msg = verify_paged_result(r.json().get("data"))
            record("P2: SIM Valuation History PagedResult", valid, f"HTTP 200, {msg}")
        else:
            record("P2: SIM Valuation History PagedResult", False, f"HTTP {r.status_code}", r.text)
    else:
        record("P2: SIM Valuation History PagedResult", False, "Không tìm thấy SIM để test valuation history")

    # 9.2 Pending Settlements
    r = requests.get(f"{BASE_URL}/api/Auctions/my-pending-settlements?pageIndex=1&pageSize=5", headers=user_headers)
    if r.status_code == 200:
        valid, msg = verify_paged_result(r.json().get("data"))
        record("P2: My Pending Settlements PagedResult", valid, f"HTTP 200, {msg}")
    else:
        record("P2: My Pending Settlements PagedResult", False, f"HTTP {r.status_code}", r.text)

    # 9.3 KYC History
    r = requests.get(f"{BASE_URL}/api/Users/kyc/history?status=VERIFIED&pageIndex=1&pageSize=5", headers=user_headers)
    if r.status_code == 200:
        valid, msg = verify_paged_result(r.json().get("data"))
        record("P2: User KYC History with status filter", valid, f"HTTP 200, {msg}")
    else:
        record("P2: User KYC History with status filter", False, f"HTTP {r.status_code}", r.text)

    # ─────────────────────────────────────────────────────────────────────────
    # 10. P1 EXPORT: 7 EXCEL EXPORT APIS
    # ─────────────────────────────────────────────────────────────────────────
    print(f"\n{Colors.BOLD}--- 10. P1: Excel Export Synchronization (7 Endpoints) ---{Colors.RESET}")
    excel_content_type = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"

    export_tests = [
        ("Auctions Export", f"{BASE_URL}/api/Auctions/export", admin_headers),
        ("AuditLogs Export", f"{BASE_URL}/api/AuditLogs/export", admin_headers),
        ("Disputes Export", f"{BASE_URL}/api/Disputes/export", admin_headers),
        ("Sims Export", f"{BASE_URL}/api/Sims/export", admin_headers),
        ("Users Admin Export", f"{BASE_URL}/api/Users/admin/export", admin_headers),
        ("Users KYC Admin Export", f"{BASE_URL}/api/Users/admin/kyc/export", admin_headers),
        ("Documents Export", f"{BASE_URL}/api/Documents/export", admin_headers),
    ]

    for title, url, headers in export_tests:
        try:
            r = requests.get(url, headers=headers, timeout=15)
            is_excel = r.status_code == 200 and (excel_content_type in r.headers.get("Content-Type", "") or len(r.content) > 1000)
            record(f"Export: {title}", is_excel, f"HTTP {r.status_code}, Bytes={len(r.content)}")
        except Exception as ex:
            record(f"Export: {title}", False, f"Exception: {ex}")

    # ─────────────────────────────────────────────────────────────────────────
    # TỔNG KẾT BÁO CÁO KIỂM THỬ
    # ─────────────────────────────────────────────────────────────────────────
    total = len(results)
    passed = sum(1 for r in results if r.passed)
    failed = total - passed

    print(f"\n{Colors.BOLD}{'='*80}")
    print(f" KẾT QUẢ TỔNG HỢP KIỂM THỬ TỰ ĐỘNG:")
    print(f" Tổng số test cases: {total}")
    print(f" Thành công (PASS): {Colors.GREEN}{passed}{Colors.RESET}")
    print(f" Thất bại (FAIL):  {Colors.RED if failed > 0 else Colors.GREEN}{failed}{Colors.RESET}")
    print(f" Tỷ lệ thành công: {passed / total * 100:.1f}%")
    print(f"{'='*80}{Colors.RESET}\n")

    if failed > 0:
        sys.exit(1)

if __name__ == "__main__":
    run_all_tests()
