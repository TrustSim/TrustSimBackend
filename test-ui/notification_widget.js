(function (global) {
  'use strict';

  const ENUM_BY_INDEX = [
    'SYSTEM_ALERT',                          // 0
    'AUCTION_WON',                           // 1
    'AUCTION_LOST',                          // 2
    'AUCTION_SELLER_CONFIRMATION_REQUIRED',  // 3
    'AUCTION_RUNNER_UP_OFFERED',             // 4
    'AUCTION_DEPOSIT_REFUNDED',              // 5
    'AUCTION_DEPOSIT_FORFEITED',             // 6
    'AUCTION_FAILED',                        // 7
    'WALLET_TOPUP_SUCCESS',                  // 8
    'WALLET_WITHDRAWAL_COMPLETED',           // 9
    'TRADE_TRANSFERRING_SIM',                // 10
    'CONTRACT_SIGNING_REQUIRED',             // 11
    'CONTRACT_FULLY_SIGNED',                 // 12
    'SELLER_SIGNING_TIMEOUT_AUTO_ENFORCED',  // 13
    'TRANSFER_PROOF_SUBMITTED',              // 14
    'TRANSFER_PROOF_WITHDRAWN',              // 15
    'TRANSFER_PROOF_APPROVED',               // 16
    'TRANSFER_PROOF_REJECTED',               // 17
    'TRADE_COMPLETED_LIQUIDATED',            // 18
    'ESCROW_RELEASED_SELLER_PAYOUT',         // 19
    'TRADE_CANCELLED_TIMEOUT',               // 20
    'TRADE_CANCELLED_BY_ADMIN',              // 21
    'TRADE_STATUS_UPDATED',                  // 22
    'DISPUTE_OPENED',                        // 23
    'DISPUTE_RESOLVED',                      // 24
    'TRADE_DEADLINE_REMINDER'                // 25
  ];

  const TYPE_META = {
    SYSTEM_ALERT: { icon: '📢', label: 'Thông báo hệ thống', tone: 'info', color: '#7dd3fc', bg: 'rgba(56,189,248,0.14)', border: 'rgba(56,189,248,0.35)' },
    AUCTION_WON: { icon: '🏆', label: 'Thắng đấu giá', tone: 'success', color: '#34d399', bg: 'rgba(16,185,129,0.15)', border: 'rgba(16,185,129,0.4)' },
    AUCTION_LOST: { icon: 'ℹ️', label: 'Kết quả đấu giá', tone: 'info', color: '#94a3b8', bg: 'rgba(148,163,184,0.14)', border: 'rgba(148,163,184,0.35)' },
    AUCTION_SELLER_CONFIRMATION_REQUIRED: { icon: '🤝', label: 'Chờ Seller duyệt Á quân', tone: 'warning', color: '#fbbf24', bg: 'rgba(245,158,11,0.15)', border: 'rgba(245,158,11,0.4)' },
    AUCTION_RUNNER_UP_OFFERED: { icon: '🥈', label: 'Mời Á quân mua lại', tone: 'warning', color: '#fbbf24', bg: 'rgba(245,158,11,0.15)', border: 'rgba(245,158,11,0.4)' },
    AUCTION_DEPOSIT_REFUNDED: { icon: '💰', label: 'Hoàn tiền cọc', tone: 'success', color: '#34d399', bg: 'rgba(16,185,129,0.15)', border: 'rgba(16,185,129,0.4)' },
    AUCTION_DEPOSIT_FORFEITED: { icon: '💥', label: 'Phạt tịch thu cọc', tone: 'error', color: '#fda4af', bg: 'rgba(244,63,94,0.16)', border: 'rgba(244,63,94,0.4)' },
    AUCTION_FAILED: { icon: '❌', label: 'Phiên đấu giá thất bại', tone: 'error', color: '#fda4af', bg: 'rgba(244,63,94,0.16)', border: 'rgba(244,63,94,0.4)' },
    WALLET_TOPUP_SUCCESS: { icon: '💳', label: 'Nạp ví thành công', tone: 'success', color: '#34d399', bg: 'rgba(16,185,129,0.15)', border: 'rgba(16,185,129,0.4)' },
    WALLET_WITHDRAWAL_COMPLETED: { icon: '🏦', label: 'Rút tiền hoàn tất', tone: 'success', color: '#34d399', bg: 'rgba(16,185,129,0.15)', border: 'rgba(16,185,129,0.4)' },
    TRADE_TRANSFERRING_SIM: { icon: '🚚', label: 'Đang chuyển nhượng SIM', tone: 'info', color: '#7dd3fc', bg: 'rgba(56,189,248,0.14)', border: 'rgba(56,189,248,0.35)' },
    CONTRACT_SIGNING_REQUIRED: { icon: '✍️', label: 'Yêu cầu ký hợp đồng', tone: 'warning', color: '#fbbf24', bg: 'rgba(245,158,11,0.15)', border: 'rgba(245,158,11,0.4)' },
    CONTRACT_FULLY_SIGNED: { icon: '🔏', label: 'Hợp đồng đã niêm phong', tone: 'success', color: '#34d399', bg: 'rgba(16,185,129,0.16)', border: 'rgba(16,185,129,0.4)' },
    SELLER_SIGNING_TIMEOUT_AUTO_ENFORCED: { icon: '⚖️', label: 'Quá hạn ký - Cưỡng chế', tone: 'warning', color: '#fbbf24', bg: 'rgba(245,158,11,0.16)', border: 'rgba(245,158,11,0.45)' },
    TRANSFER_PROOF_SUBMITTED: { icon: '📤', label: 'Đã nộp bằng chứng', tone: 'info', color: '#7dd3fc', bg: 'rgba(56,189,248,0.15)', border: 'rgba(56,189,248,0.4)' },
    TRANSFER_PROOF_WITHDRAWN: { icon: '↩️', label: 'Thu hồi bằng chứng', tone: 'warning', color: '#fbbf24', bg: 'rgba(245,158,11,0.15)', border: 'rgba(245,158,11,0.4)' },
    TRANSFER_PROOF_APPROVED: { icon: '✅', label: 'Bằng chứng đã duyệt', tone: 'success', color: '#34d399', bg: 'rgba(16,185,129,0.16)', border: 'rgba(16,185,129,0.45)' },
    TRANSFER_PROOF_REJECTED: { icon: '❌', label: 'Bằng chứng bị từ chối', tone: 'error', color: '#fda4af', bg: 'rgba(244,63,94,0.18)', border: 'rgba(244,63,94,0.5)' },
    TRADE_COMPLETED_LIQUIDATED: { icon: '🎉', label: 'Hoàn tất & Thanh lý VB3', tone: 'success', color: '#34d399', bg: 'rgba(16,185,129,0.18)', border: 'rgba(16,185,129,0.5)' },
    ESCROW_RELEASED_SELLER_PAYOUT: { icon: '💸', label: 'Giải ngân Escrow về ví', tone: 'success', color: '#34d399', bg: 'rgba(16,185,129,0.18)', border: 'rgba(16,185,129,0.5)' },
    TRADE_CANCELLED_TIMEOUT: { icon: '⏱️', label: 'Hủy do quá hạn', tone: 'error', color: '#fda4af', bg: 'rgba(244,63,94,0.18)', border: 'rgba(244,63,94,0.5)' },
    TRADE_CANCELLED_BY_ADMIN: { icon: '🛑', label: 'Admin hủy giao dịch', tone: 'error', color: '#fda4af', bg: 'rgba(244,63,94,0.18)', border: 'rgba(244,63,94,0.5)' },
    TRADE_STATUS_UPDATED: { icon: '🔄', label: 'Cập nhật trạng thái GD', tone: 'info', color: '#7dd3fc', bg: 'rgba(56,189,248,0.15)', border: 'rgba(56,189,248,0.4)' },
    DISPUTE_OPENED: { icon: '⚖️', label: 'Mở tranh chấp', tone: 'warning', color: '#fbbf24', bg: 'rgba(245,158,11,0.18)', border: 'rgba(245,158,11,0.5)' },
    DISPUTE_RESOLVED: { icon: '🏛️', label: 'Phán quyết tranh chấp', tone: 'success', color: '#34d399', bg: 'rgba(16,185,129,0.16)', border: 'rgba(16,185,129,0.45)' },
    TRADE_DEADLINE_REMINDER: { icon: '⏰', label: 'Nhắc hạn giao dịch', tone: 'warning', color: '#fbbf24', bg: 'rgba(245,158,11,0.18)', border: 'rgba(245,158,11,0.5)' }
  };

  function escapeHtml(value) {
    return String(value ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function normalizeType(rawType) {
    if (rawType === null || rawType === undefined || rawType === '') {
      return 'SYSTEM_ALERT';
    }
    if (typeof rawType === 'number') {
      return ENUM_BY_INDEX[rawType] || 'SYSTEM_ALERT';
    }
    const str = String(rawType).trim();
    if (/^\d+$/.test(str)) {
      return ENUM_BY_INDEX[Number(str)] || 'SYSTEM_ALERT';
    }
    return str.toUpperCase();
  }

  function getTypeMeta(rawType) {
    const key = normalizeType(rawType);
    const meta = TYPE_META[key] || TYPE_META.SYSTEM_ALERT;
    return { key, ...meta };
  }

  function normalizeNotification(raw) {
    if (!raw || typeof raw !== 'object') return null;
    const rawType = raw.notificationType !== undefined ? raw.notificationType : raw.NotificationType;
    const typeKey = normalizeType(rawType);
    return {
      id: raw.id ?? raw.Id ?? '',
      userId: raw.userId ?? raw.UserId ?? '',
      title: raw.title ?? raw.Title ?? 'Thông báo mới',
      content: raw.content ?? raw.Content ?? '',
      notificationType: typeKey,
      referenceType: raw.referenceType ?? raw.ReferenceType ?? '',
      referenceId: raw.referenceId ?? raw.ReferenceId ?? '',
      metadataJson: raw.metadataJson ?? raw.MetadataJson ?? '',
      actionUrl: raw.actionUrl ?? raw.ActionUrl ?? '',
      isRead: Boolean(raw.isRead ?? raw.IsRead ?? false),
      readAt: raw.readAt ?? raw.ReadAt ?? null,
      createdDate: raw.createdDate ?? raw.CreatedDate ?? new Date().toISOString()
    };
  }

  function parseMetadata(notif) {
    const json = notif?.metadataJson ?? notif?.MetadataJson;
    if (!json) return {};
    if (typeof json === 'object') return json;
    try {
      return JSON.parse(json) || {};
    } catch {
      return {};
    }
  }

  function isPdfOrExternalUrl(url) {
    if (!url) return false;
    const lower = String(url).toLowerCase();
    return lower.includes('.supabase.co')
      || lower.endsWith('.pdf')
      || lower.startsWith('/uploads/')
      || /^https?:\/\//i.test(url);
  }

  function resolveNavigationTarget(rawNotif) {
    const notif = normalizeNotification(rawNotif);
    if (!notif) return { kind: 'none' };

    const meta = parseMetadata(notif);
    const refType = (notif.referenceType || '').trim();
    const refId = (notif.referenceId || '').trim();
    const actionUrl = (notif.actionUrl || '').trim();
    const typeKey = notif.notificationType;

    const tradeId = meta.tradeId || (refType === 'TradeTransaction' ? refId : '');
    const auctionId = meta.auctionId || (refType === 'Auction' ? refId : '');
    const ticketId = meta.ticketId || (refType === 'DisputeTicket' ? refId : '');

    if (isPdfOrExternalUrl(actionUrl)) {
      return {
        kind: 'pdf',
        url: actionUrl,
        tradeId,
        auctionId,
        ticketId,
        meta
      };
    }

    if (tradeId) {
      const preferredPage = (
        typeKey === 'CONTRACT_SIGNING_REQUIRED'
        || typeKey === 'CONTRACT_FULLY_SIGNED'
        || typeKey === 'SELLER_SIGNING_TIMEOUT_AUTO_ENFORCED'
      ) ? 'contract_studio.html' : 'transfer_review_studio.html';

      return {
        kind: 'trade',
        tradeId,
        auctionId,
        ticketId,
        preferredPage,
        actionUrl,
        meta
      };
    }

    const urlAuctionMatch = actionUrl.match(/\/auctions\/([0-9a-f-]{36})/i);
    if (auctionId || urlAuctionMatch || (!refType && refId && typeKey.startsWith('AUCTION_'))) {
      const resolvedAucId = auctionId || urlAuctionMatch?.[1] || refId || '';
      if (resolvedAucId) {
        return {
          kind: 'auction',
          auctionId: resolvedAucId,
          actionUrl,
          meta
        };
      }
    }

    return { kind: 'none', actionUrl, meta };
  }

  function ensureStylesInjected() {
    if (document.getElementById('ts-notif-widget-styles')) return;
    const style = document.createElement('style');
    style.id = 'ts-notif-widget-styles';
    style.textContent = `
      .ts-notif-bell-btn {
        position: relative;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 6px;
        padding: 7px 12px;
        border-radius: 8px;
        border: 1px solid rgba(56, 189, 248, 0.35);
        background: rgba(56, 189, 248, 0.12);
        color: #f8fafc;
        font-size: 12px;
        font-weight: 700;
        cursor: pointer;
        transition: all 0.15s ease;
      }
      .ts-notif-bell-btn:hover {
        background: rgba(56, 189, 248, 0.22);
        border-color: #38bdf8;
      }
      .ts-notif-badge {
        background: #ef4444;
        color: #fff;
        font-size: 10px;
        font-weight: 800;
        padding: 1px 6px;
        border-radius: 999px;
        min-width: 18px;
        text-align: center;
        line-height: 1.4;
      }
      .ts-notif-backdrop {
        position: fixed;
        inset: 0;
        background: rgba(2, 6, 23, 0.82);
        backdrop-filter: blur(6px);
        z-index: 2500;
        display: none;
        align-items: center;
        justify-content: center;
        padding: 18px;
      }
      .ts-notif-backdrop.active {
        display: flex;
      }
      .ts-notif-modal {
        background: #0f172a;
        border: 1px solid rgba(56, 189, 248, 0.4);
        border-radius: 14px;
        width: 100%;
        max-width: 720px;
        max-height: 86vh;
        display: flex;
        flex-direction: column;
        overflow: hidden;
        color: #f8fafc;
        box-shadow: 0 24px 60px rgba(0, 0, 0, 0.65);
        font-family: inherit;
      }
      .ts-notif-head {
        padding: 14px 18px;
        border-bottom: 1px solid rgba(148, 163, 184, 0.18);
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 10px;
        flex-wrap: wrap;
        background: rgba(255, 255, 255, 0.02);
      }
      .ts-notif-toolbar {
        padding: 10px 18px;
        border-bottom: 1px solid rgba(148, 163, 184, 0.14);
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 8px;
        flex-wrap: wrap;
      }
      .ts-notif-tabs {
        display: flex;
        gap: 6px;
      }
      .ts-notif-tab {
        padding: 4px 10px;
        border-radius: 999px;
        border: 1px solid rgba(148, 163, 184, 0.25);
        background: rgba(255, 255, 255, 0.04);
        color: #94a3b8;
        font-size: 11px;
        font-weight: 700;
        cursor: pointer;
      }
      .ts-notif-tab.active {
        background: rgba(56, 189, 248, 0.18);
        border-color: #38bdf8;
        color: #f8fafc;
      }
      .ts-notif-list {
        padding: 14px 18px;
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 10px;
        flex: 1;
      }
      .ts-notif-card {
        background: rgba(2, 6, 23, 0.6);
        border: 1px solid rgba(148, 163, 184, 0.18);
        border-radius: 10px;
        padding: 12px 14px;
        display: flex;
        flex-direction: column;
        gap: 8px;
        transition: border-color 0.15s ease;
      }
      .ts-notif-card.unread {
        background: rgba(30, 58, 138, 0.2);
        border-color: rgba(56, 189, 248, 0.45);
      }
      .ts-notif-card-top {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 10px;
      }
      .ts-notif-type-pill {
        display: inline-flex;
        align-items: center;
        gap: 5px;
        font-size: 10px;
        font-weight: 700;
        padding: 2px 8px;
        border-radius: 999px;
      }
      .ts-notif-reject-reason {
        background: rgba(244, 63, 94, 0.14);
        border: 1px solid rgba(244, 63, 94, 0.4);
        color: #fecdd3;
        padding: 7px 10px;
        border-radius: 6px;
        font-size: 11.5px;
      }
      .ts-notif-actions {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 8px;
        flex-wrap: wrap;
        font-size: 11px;
        color: #94a3b8;
      }
      .ts-notif-btn {
        padding: 4px 10px;
        border-radius: 6px;
        border: 1px solid rgba(148, 163, 184, 0.28);
        background: rgba(255, 255, 255, 0.06);
        color: #f8fafc;
        font-size: 11px;
        font-weight: 600;
        cursor: pointer;
        text-decoration: none;
        display: inline-flex;
        align-items: center;
        gap: 5px;
      }
      .ts-notif-btn:hover {
        background: rgba(255, 255, 255, 0.12);
      }
      .ts-notif-btn.primary {
        background: rgba(56, 189, 248, 0.2);
        border-color: rgba(56, 189, 248, 0.5);
        color: #7dd3fc;
      }
      .ts-notif-btn.emerald {
        background: rgba(16, 185, 129, 0.2);
        border-color: rgba(16, 185, 129, 0.5);
        color: #34d399;
      }
    `;
    document.head.appendChild(style);
  }

  function createClient(options) {
    ensureStylesInjected();

    const instanceId = `tsNotif_${Math.random().toString(36).slice(2, 8)}`;
    let connection = null;
    let joinedUserId = '';
    let notifications = [];
    let unreadCount = 0;
    let activeFilter = 'ALL';

    const mountEl = options.mountSelector ? document.querySelector(options.mountSelector) : null;
    let badgeEl = null;
    let modalEl = null;
    let listEl = null;
    let subtitleEl = null;

    if (mountEl) {
      const bellBtn = document.createElement('button');
      bellBtn.type = 'button';
      bellBtn.className = 'ts-notif-bell-btn';
      bellBtn.title = 'Xem thông báo cá nhân';
      bellBtn.innerHTML = `<span>🔔 Thông báo</span><span class="ts-notif-badge" id="${instanceId}_badge" style="display:none;">0</span>`;
      bellBtn.addEventListener('click', openModal);
      mountEl.insertAdjacentElement(options.insertPosition || 'beforeend', bellBtn);
      badgeEl = bellBtn.querySelector(`#${instanceId}_badge`);
    }

    modalEl = document.createElement('div');
    modalEl.className = 'ts-notif-backdrop';
    modalEl.id = `${instanceId}_modal`;
    modalEl.innerHTML = `
      <div class="ts-notif-modal" role="dialog" aria-modal="true">
        <div class="ts-notif-head">
          <div>
            <div style="font-size:15px;font-weight:800;">📬 Hộp Thư Thông Báo Cá Nhân</div>
            <div id="${instanceId}_subtitle" style="font-size:11px;color:#94a3b8;">Nhận realtime qua SignalR (/hubs/auction) &amp; lưu trữ DB</div>
          </div>
          <div style="display:flex;gap:8px;align-items:center;">
            <button type="button" class="ts-notif-btn" id="${instanceId}_refreshBtn">🔄 Làm mới</button>
            <button type="button" class="ts-notif-btn primary" id="${instanceId}_readAllBtn">✓ Đọc tất cả</button>
            <button type="button" class="ts-notif-btn" id="${instanceId}_closeBtn">✕</button>
          </div>
        </div>
        <div class="ts-notif-toolbar">
          <div class="ts-notif-tabs">
            <button type="button" class="ts-notif-tab active" data-filter="ALL">Tất cả</button>
            <button type="button" class="ts-notif-tab" data-filter="UNREAD">Chưa đọc</button>
          </div>
          <span id="${instanceId}_countLabel" style="font-size:11px;color:#94a3b8;">0 thông báo</span>
        </div>
        <div class="ts-notif-list" id="${instanceId}_list">
          <div style="text-align:center;color:#64748b;padding:24px;font-size:12px;">Chưa có thông báo.</div>
        </div>
      </div>
    `;
    document.body.appendChild(modalEl);

    listEl = modalEl.querySelector(`#${instanceId}_list`);
    subtitleEl = modalEl.querySelector(`#${instanceId}_subtitle`);
    const countLabelEl = modalEl.querySelector(`#${instanceId}_countLabel`);

    modalEl.addEventListener('click', (e) => {
      if (e.target === modalEl) closeModal();
    });
    modalEl.querySelector(`#${instanceId}_closeBtn`).addEventListener('click', closeModal);
    modalEl.querySelector(`#${instanceId}_refreshBtn`).addEventListener('click', () => refreshNotifications(false));
    modalEl.querySelector(`#${instanceId}_readAllBtn`).addEventListener('click', markAllAsRead);
    modalEl.querySelectorAll('.ts-notif-tab').forEach(btn => {
      btn.addEventListener('click', () => {
        activeFilter = btn.dataset.filter || 'ALL';
        modalEl.querySelectorAll('.ts-notif-tab').forEach(b => b.classList.toggle('active', b === btn));
        renderList();
      });
    });

    function getHeaders() {
      const headers = {};
      const token = options.getToken ? options.getToken() : '';
      if (token) headers.Authorization = `Bearer ${token}`;
      return headers;
    }

    function updateBadge(count) {
      unreadCount = Number(count) || 0;
      if (!badgeEl) return;
      if (unreadCount > 0) {
        badgeEl.textContent = unreadCount > 99 ? '99+' : String(unreadCount);
        badgeEl.style.display = 'inline-block';
      } else {
        badgeEl.style.display = 'none';
      }
    }

    function openModal() {
      modalEl.classList.add('active');
      const uname = options.getUserName ? options.getUserName() : '';
      const role = options.getUserRole ? options.getUserRole() : '';
      if (subtitleEl) {
        subtitleEl.textContent = uname
          ? `Tài khoản: ${uname}${role ? ` (${role})` : ''} • SignalR user_${(options.getUserId?.() || '').toLowerCase()}`
          : 'Vui lòng đăng nhập để xem thông báo';
      }
      refreshNotifications(false);
    }

    function closeModal() {
      modalEl.classList.remove('active');
    }

    async function refreshNotifications(showUnreadToasts = false) {
      const token = options.getToken ? options.getToken() : '';
      const baseUrl = options.getBackendUrl ? options.getBackendUrl() : '';
      if (!token || !baseUrl) {
        notifications = [];
        updateBadge(0);
        renderList();
        return;
      }

      try {
        const [listRes, countRes] = await Promise.all([
          fetch(`${baseUrl}/api/Notifications?pageIndex=1&pageSize=30`, { headers: getHeaders(), credentials: 'include' }),
          fetch(`${baseUrl}/api/Notifications/unread-count`, { headers: getHeaders(), credentials: 'include' })
        ]);

        if (listRes.ok) {
          const payload = await listRes.json();
          const rawItems = payload?.data?.items || [];
          notifications = rawItems.map(normalizeNotification).filter(Boolean);
        }

        if (countRes.ok) {
          const countPayload = await countRes.json();
          updateBadge(countPayload?.data ?? notifications.filter(n => !n.isRead).length);
        } else {
          updateBadge(notifications.filter(n => !n.isRead).length);
        }

        renderList();

        if (showUnreadToasts) {
          const unreadItems = notifications.filter(n => !n.isRead).slice(0, 2);
          unreadItems.forEach((n, idx) => {
            setTimeout(() => notifyToast(n), idx * 350);
          });
        }
      } catch (err) {
        console.warn('[TrustSimNotifications] refreshNotifications error:', err);
      }
    }

    async function markAsRead(notifId) {
      const token = options.getToken ? options.getToken() : '';
      const baseUrl = options.getBackendUrl ? options.getBackendUrl() : '';
      if (!token || !baseUrl || !notifId) return;
      try {
        const res = await fetch(`${baseUrl}/api/Notifications/${notifId}/read`, {
          method: 'PATCH',
          headers: getHeaders(),
          credentials: 'include'
        });
        if (res.ok) {
          const item = notifications.find(n => n.id === notifId);
          if (item && !item.isRead) {
            item.isRead = true;
            updateBadge(Math.max(0, unreadCount - 1));
          }
          renderList();
        }
      } catch (err) {
        console.warn('[TrustSimNotifications] markAsRead error:', err);
      }
    }

    async function markAllAsRead() {
      const unread = notifications.filter(n => !n.isRead);
      for (const item of unread) {
        await markAsRead(item.id);
      }
      updateBadge(0);
      renderList();
    }

    async function handleAction(notif, mode = 'auto') {
      await markAsRead(notif.id);
      const target = resolveNavigationTarget(notif);

      if ((mode === 'open-pdf' || (mode === 'auto' && target.kind === 'pdf')) && target.url) {
        window.open(target.url, '_blank');
        return;
      }

      if ((mode === 'open-trade' || target.kind === 'trade' || mode === 'open-pdf') && target.tradeId) {
        closeModal();
        if (typeof options.onNavigateTrade === 'function') {
          await options.onNavigateTrade(target.tradeId, target.auctionId || '', notif, target);
        } else {
          const userParam = options.getUserName ? `&user=${encodeURIComponent(options.getUserName())}` : '';
          window.location.href = `${target.preferredPage}?tradeId=${encodeURIComponent(target.tradeId)}${userParam}`;
        }
        return;
      }

      if (target.auctionId && typeof options.onNavigateAuction === 'function') {
        closeModal();
        await options.onNavigateAuction(target.auctionId, notif);
      }
    }

    function notifyToast(notif) {
      const meta = getTypeMeta(notif.notificationType);
      const msg = `${meta.icon} [${meta.label}] ${notif.title}${notif.content ? `: ${notif.content}` : ''}`;
      if (typeof options.onToast === 'function') {
        options.onToast(msg, meta.tone, notif);
      }
    }

    function renderList() {
      if (!listEl) return;
      const items = activeFilter === 'UNREAD'
        ? notifications.filter(n => !n.isRead)
        : notifications;

      if (countLabelEl) {
        countLabelEl.textContent = `${items.length} thông báo (${unreadCount} chưa đọc)`;
      }

      if (!items.length) {
        listEl.innerHTML = `<div style="text-align:center;color:#64748b;padding:26px;font-size:12px;">${activeFilter === 'UNREAD' ? 'Không có thông báo chưa đọc.' : 'Hộp thư thông báo đang trống.'}</div>`;
        return;
      }

      listEl.innerHTML = items.map(n => {
        const typeMeta = getTypeMeta(n.notificationType);
        const meta = parseMetadata(n);
        const target = resolveNavigationTarget(n);
        const timeStr = n.createdDate ? new Date(n.createdDate).toLocaleString('vi-VN') : '--';

        const rejectHtml = meta.rejectionReason
          ? `<div class="ts-notif-reject-reason"><strong>Lý do từ chối:</strong> ${escapeHtml(meta.rejectionReason)}</div>`
          : '';

        const pdfBtn = target.kind === 'pdf' && target.url
          ? `<button type="button" class="ts-notif-btn emerald" data-action="open-pdf" data-id="${escapeHtml(n.id)}">📄 Mở PDF</button>`
          : '';

        const tradeBtn = target.tradeId
          ? `<button type="button" class="ts-notif-btn primary" data-action="open-trade" data-id="${escapeHtml(n.id)}">🔎 Mở giao dịch</button>`
          : (!target.tradeId && target.kind === 'auction' && target.auctionId
            ? `<button type="button" class="ts-notif-btn primary" data-action="open-auction" data-id="${escapeHtml(n.id)}">🏛️ Mở phiên</button>`
            : '');

        const markBtn = !n.isRead
          ? `<button type="button" class="ts-notif-btn" data-action="read" data-id="${escapeHtml(n.id)}">✓ Đã đọc</button>`
          : '<span style="color:#34d399;font-size:10px;font-weight:700;">✓ Đã đọc</span>';

        return `
          <div class="ts-notif-card ${n.isRead ? '' : 'unread'}">
            <div class="ts-notif-card-top">
              <div style="display:flex;flex-direction:column;gap:5px;">
                <div>
                  <span class="ts-notif-type-pill" style="color:${typeMeta.color};background:${typeMeta.bg};border:1px solid ${typeMeta.border};">
                    ${typeMeta.icon} ${escapeHtml(typeMeta.label)}
                  </span>
                </div>
                <div style="font-weight:700;font-size:13px;color:#f8fafc;">${escapeHtml(n.title)}</div>
              </div>
              <span style="font-size:10.5px;color:#94a3b8;white-space:nowrap;">🕒 ${escapeHtml(timeStr)}</span>
            </div>
            <div style="font-size:12px;color:#cbd5e1;line-height:1.5;">${escapeHtml(n.content)}</div>
            ${rejectHtml}
            <div class="ts-notif-actions">
              <span style="font-family:monospace;font-size:10px;color:#64748b;">
                ${meta.tradeCode ? `Mã GD: ${escapeHtml(meta.tradeCode)}` : (n.referenceType ? `${escapeHtml(n.referenceType)}` : '')}
              </span>
              <div style="display:flex;gap:6px;align-items:center;">
                ${pdfBtn}
                ${tradeBtn}
                ${markBtn}
              </div>
            </div>
          </div>
        `;
      }).join('');

      listEl.querySelectorAll('button[data-action]').forEach(btn => {
        btn.addEventListener('click', async (e) => {
          e.stopPropagation();
          const id = btn.dataset.id;
          const notif = notifications.find(x => x.id === id);
          if (!notif) return;
          if (btn.dataset.action === 'read') {
            await markAsRead(id);
          } else {
            await handleAction(notif, btn.dataset.action);
          }
        });
      });
    }

    async function connectSignalR() {
      if (typeof global.signalR === 'undefined') {
        options.onSignalRStatusChange?.(false, 'SignalR: Missing Lib');
        return;
      }

      const baseUrl = options.getBackendUrl ? options.getBackendUrl() : '';
      const token = options.getToken ? options.getToken() : '';
      const userId = options.getUserId ? options.getUserId() : '';

      if (!baseUrl) return;

      if (connection) {
        try {
          if (joinedUserId && connection.state === global.signalR.HubConnectionState.Connected) {
            await connection.invoke('LeavePersonalNotificationFeed', joinedUserId);
          }
          await connection.stop();
        } catch {
          // ignore cleanup failures
        }
        connection = null;
        joinedUserId = '';
      }

      options.onSignalRStatusChange?.(false, 'SignalR: Connecting...');

      try {
        connection = new global.signalR.HubConnectionBuilder()
          .withUrl(`${baseUrl}/hubs/auction`, {
            accessTokenFactory: () => (options.getToken ? options.getToken() : token) || ''
          })
          .withAutomaticReconnect([0, 2000, 5000, 10000])
          .configureLogging(global.signalR.LogLevel.Warning)
          .build();

        connection.on('ReceivePersonalNotification', async (rawNotif) => {
          const notif = normalizeNotification(rawNotif);
          if (!notif) return;

          const existedUnread = notifications.some(x => x.id === notif.id && !x.isRead);
          notifications = [notif, ...notifications.filter(x => x.id !== notif.id)];
          if (!existedUnread && !notif.isRead) {
            updateBadge(unreadCount + 1);
          }
          renderList();
          notifyToast(notif);

          if (typeof options.onRealtimeNotification === 'function') {
            await options.onRealtimeNotification(notif, rawNotif);
          }
        });

        connection.on('ReceiveAuctionStatusChanged', async (auctionId, status, winnerId, finalPrice) => {
          if (typeof options.onAuctionStatusChanged === 'function') {
            await options.onAuctionStatusChanged(auctionId, status, winnerId, finalPrice);
          }
        });

        connection.onreconnecting(() => {
          options.onSignalRStatusChange?.(false, 'SignalR: Reconnecting...');
        });

        connection.onreconnected(async () => {
          options.onSignalRStatusChange?.(true, 'SignalR: Connected');
          const uid = options.getUserId ? options.getUserId() : '';
          if (uid) {
            try {
              await connection.invoke('JoinPersonalNotificationFeed', uid);
              joinedUserId = uid;
            } catch {
              // ignore
            }
          }
        });

        connection.onclose(() => {
          options.onSignalRStatusChange?.(false, 'SignalR: Disconnected');
        });

        await connection.start();
        options.onSignalRStatusChange?.(true, 'SignalR: Connected');

        if (userId) {
          await connection.invoke('JoinPersonalNotificationFeed', userId);
          joinedUserId = userId;
        }
      } catch (err) {
        console.warn('[TrustSimNotifications] SignalR connection error:', err);
        options.onSignalRStatusChange?.(false, 'SignalR: Error');
      }
    }

    async function syncSession(showUnreadToasts = false) {
      await refreshNotifications(showUnreadToasts);
      await connectSignalR();
    }

    return {
      syncSession,
      refreshNotifications,
      connectSignalR,
      openModal,
      closeModal,
      markAsRead,
      markAllAsRead,
      getNotifications: () => notifications.slice()
    };
  }

  global.TrustSimNotifications = {
    ENUM_BY_INDEX,
    TYPE_META,
    escapeHtml,
    normalizeType,
    getTypeMeta,
    normalizeNotification,
    parseMetadata,
    resolveNavigationTarget,
    createClient
  };
})(window);
