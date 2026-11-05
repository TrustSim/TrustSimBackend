// ═══════════════════════════════════════════════════════════════════
// TrustSim · Chat Studio Controller
// Multi-session realtime testing client for Direct & Support chat
// ═══════════════════════════════════════════════════════════════════

const DEMO_USERS = {
  bidder_dai_gia: {
    pass: 'Password123@', role: 'USER', status: 'VERIFIED',
    fullName: 'Phạm Hoàng Nam', email: 'quanidol6224@gmail.com', phone: '0988777003',
    group: 'verified'
  },
  bidder_kinh_doanh: {
    pass: 'Password123@', role: 'USER', status: 'VERIFIED',
    fullName: 'Vũ Minh Trí', email: 'minhtri.bds@gmail.com', phone: '0903444004',
    group: 'verified'
  },
  seller_vip_phuquy: {
    pass: 'Password123@', role: 'USER', status: 'VERIFIED',
    fullName: 'Trần Phú Quý', email: 'supperidol6224@gmail.com', phone: '0909999001',
    group: 'verified'
  },
  seller_daiphat: {
    pass: 'Password123@', role: 'USER', status: 'VERIFIED',
    fullName: 'Nguyễn Đại Phát', email: 'daiphat.telecom@gmail.com', phone: '0912333002',
    group: 'verified'
  },
  bidder_alpha: {
    pass: 'Password123@', role: 'USER', status: 'VERIFIED',
    fullName: 'Hoàng Gia Bảo', email: 'alpha.bidder@gmail.com', phone: '0981111222',
    group: 'verified'
  },
  bidder_phong_thuy: {
    pass: 'Password123@', role: 'USER', status: 'VERIFIED',
    fullName: 'Đặng Quang Đức', email: 'quangduc.fengshui@gmail.com', phone: '0935555005',
    group: 'verified'
  },
  consultant_01: {
    pass: 'Password123@', role: 'CONSULTANT', status: 'VERIFIED',
    fullName: 'Lê Hoàng Chuyên Viên', email: 'consultant01@trustsim.vn', phone: '0900000002',
    group: 'staff'
  },
  admin: {
    pass: 'Password123@', role: 'ADMIN', status: 'VERIFIED',
    fullName: 'Tổng Quản Trị Hệ Thống', email: 'admin@trustsim.vn', phone: '0900000001',
    group: 'staff'
  },
  user_ekyc_pending: {
    pass: 'Password123@', role: 'USER', status: 'PENDING',
    fullName: 'Bùi Thanh Hà', email: 'thanhha.new@gmail.com', phone: '0977111006',
    group: 'unverified'
  },
  user_ekyc_rejected: {
    pass: 'Password123@', role: 'USER', status: 'REJECTED',
    fullName: 'Ngô Việt Cường', email: 'vietcuong.fake@gmail.com', phone: '0944222007',
    group: 'unverified'
  }
};

let backendUrl = 'http://localhost:5130';
let activeQuickTarget = 'A';
const sessions = {};
let logEntries = [];

// ═══════════════════════════════════════════════════════════════════
// Toast & Logger
// ═══════════════════════════════════════════════════════════════════
function showToast(message, type = 'info') {
  const container = document.getElementById('toasts');
  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  toast.innerHTML = `<span>${type === 'success' ? '✔' : type === 'error' ? '✖' : 'ℹ'}</span> <span>${escapeHtml(message)}</span>`;
  container.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = '0';
    setTimeout(() => toast.remove(), 200);
  }, 3500);
}

function addLog(source, text, isError = false) {
  const time = new Date().toLocaleTimeString();
  logEntries.unshift({ time, source, text, isError });
  if (logEntries.length > 500) logEntries.pop();
  document.getElementById('logCount').innerText = logEntries.length;
  renderLogs();
}

function renderLogs() {
  const filter = document.getElementById('logFilter').value;
  const box = document.getElementById('logBox');
  const filtered = logEntries.filter(entry => {
    if (filter === 'error') return entry.isError;
    if (filter === 'api') return entry.source.startsWith('API');
    if (filter === 'rt') return entry.source.startsWith('RT');
    return true;
  });

  box.innerHTML = filtered.map(e => `
    <div class="log-entry">
      <span class="log-time">[${e.time}]</span>
      <span class="log-src ${e.isError ? 'ERR' : e.source.startsWith('RT') ? 'RT' : 'API'}">${escapeHtml(e.source)}</span>
      <span class="log-msg">${escapeHtml(e.text)}</span>
    </div>
  `).join('');
}

function escapeHtml(str) {
  if (str === null || str === undefined) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

// ═══════════════════════════════════════════════════════════════════
// Session State Class (mỗi phiên có token, user, signalr, conv riêng)
// ═══════════════════════════════════════════════════════════════════
class SessionClient {
  constructor(tag) {
    this.tag = tag;
    this.token = '';
    this.user = null;
    this.hub = null;
    this.conversations = [];
    this.activeConv = null;
    this.activeFilter = 'all';
    this.selectedFile = null;
    this.searchTimer = null;

    this.initDom();
  }

  initDom() {
    const tmpl = document.getElementById('sessionTemplate');
    const clone = tmpl.content.cloneNode(true);
    this.el = clone.querySelector('.session');
    this.el.setAttribute('data-session', this.tag);
    this.el.querySelector('.session-tag').innerText = `Phiên ${this.tag}`;

    // Elements
    this.avatarEl = this.el.querySelector('.avatar');
    this.nameEl = this.el.querySelector('.identity-name');
    this.metaEl = this.el.querySelector('.identity-meta');
    this.rtStatusEl = this.el.querySelector('.rt-status');
    this.accountSelect = this.el.querySelector('.account-select');
    this.searchInput = this.el.querySelector('.search-input');
    this.searchResults = this.el.querySelector('.search-results');
    this.convListEl = this.el.querySelector('.conv-list');
    this.chatTitleEl = this.el.querySelector('.chat-title');
    this.chatSubEl = this.el.querySelector('.chat-sub');
    this.messagesEl = this.el.querySelector('.messages');
    this.composer = this.el.querySelector('.composer');
    this.msgInput = this.el.querySelector('.msg-input');
    this.attachInput = this.el.querySelector('.attach-input');
    this.attachNameEl = this.el.querySelector('.attach-name');
    this.claimBtn = this.el.querySelector('.claim-btn');
    this.readBtn = this.el.querySelector('.read-btn');
    this.copyIdBtn = this.el.querySelector('.copy-id-btn');
    this.openIdInput = this.el.querySelector('.open-id-input');

    // Populate account select
    this.accountSelect.innerHTML = `<option value="">-- Đổi tài khoản --</option>` +
      Object.keys(DEMO_USERS).map(u => `<option value="${u}">${u} (${DEMO_USERS[u].role})</option>`).join('');

    // Events
    this.accountSelect.addEventListener('change', () => {
      if (this.accountSelect.value) {
        this.login(this.accountSelect.value);
        this.accountSelect.value = '';
      }
    });

    this.el.querySelector('.logout-btn').addEventListener('click', () => this.logout());
    this.el.querySelector('.support-btn').addEventListener('click', () => this.startSupportChat());
    this.el.querySelector('.refresh-btn').addEventListener('click', () => this.loadConversations());

    this.el.querySelectorAll('.ftab').forEach(btn => {
      btn.addEventListener('click', () => {
        this.el.querySelectorAll('.ftab').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        this.activeFilter = btn.dataset.filter;
        this.renderConversations();
      });
    });

    this.searchInput.addEventListener('input', () => {
      clearTimeout(this.searchTimer);
      const val = this.searchInput.value.trim();
      if (!val) {
        this.searchResults.classList.remove('open');
        this.searchResults.innerHTML = '';
        return;
      }
      this.searchTimer = setTimeout(() => this.searchUsers(val), 300);
    });

    this.el.querySelector('.search-btn').addEventListener('click', () => {
      const val = this.searchInput.value.trim();
      if (val) this.searchUsers(val);
    });

    this.el.querySelector('.open-id-btn').addEventListener('click', () => {
      const id = this.openIdInput.value.trim();
      if (id) this.openById(id);
    });

    this.claimBtn.addEventListener('click', () => {
      if (this.activeConv) this.claimConversation(this.activeConv.id);
    });

    this.readBtn.addEventListener('click', () => {
      if (this.activeConv) this.markAsRead(this.activeConv.id);
    });

    this.copyIdBtn.addEventListener('click', () => {
      if (this.activeConv) {
        navigator.clipboard.writeText(this.activeConv.id);
        showToast(`Đã copy ID: ${this.activeConv.id}`, 'info');
      }
    });

    this.attachInput.addEventListener('change', (e) => {
      const file = e.target.files[0];
      this.selectedFile = file || null;
      this.attachNameEl.innerText = file ? `📎 ${file.name}` : '';
    });

    this.composer.addEventListener('submit', (e) => {
      e.preventDefault();
      const text = this.msgInput.value.trim();
      if (!text && !this.selectedFile) return;
      this.sendMessage(text, this.selectedFile);
    });

    document.getElementById('sessions').appendChild(this.el);
  }

  // ─── API Wrapper with Token ──────────────────────────────────────────
  async api(endpoint, options = {}) {
    options.headers = options.headers || {};
    if (this.token && !options.headers['Authorization']) {
      options.headers['Authorization'] = `Bearer ${this.token}`;
    }
    const url = `${backendUrl}${endpoint}`;
    try {
      const res = await fetch(url, options);
      let data = null;
      const text = await res.text();
      try { data = JSON.parse(text); } catch { data = text; }

      const logMsg = `${options.method || 'GET'} ${endpoint} -> ${res.status}`;
      addLog(`API [${this.tag}]`, `${logMsg}: ${typeof data === 'object' ? JSON.stringify(data).slice(0, 180) : text.slice(0, 180)}`, !res.ok);
      return { ok: res.ok, status: res.status, data };
    } catch (err) {
      addLog(`API [${this.tag}]`, `${options.method || 'GET'} ${endpoint} -> Network Error: ${err.message}`, true);
      return { ok: false, status: 0, error: err.message };
    }
  }

  // ─── Login & SignalR ────────────────────────────────────────────────
  async login(username) {
    const cfg = DEMO_USERS[username];
    if (!cfg) return;

    showToast(`[${this.tag}] Đang đăng nhập: ${username}...`, 'info');
    const res = await this.api('/api/Auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userNameOrEmail: username, password: cfg.pass })
    });

    const isSuccess = res.ok && (res.data?.success || res.data?.isSuccess);
    if (isSuccess && res.data?.data) {
      const d = res.data.data;
      this.token = d.token || d.accessToken || '';
      this.user = {
        id: d.userId || d.user?.id || '',
        username: username,
        fullName: d.fullName || d.user?.fullName || cfg.fullName,
        role: d.role || d.user?.role || cfg.role,
        status: cfg.status,
        email: d.email || d.user?.email || cfg.email,
        phone: cfg.phone
      };

      this.avatarEl.innerText = this.user.fullName ? this.user.fullName.charAt(0).toUpperCase() : username.charAt(0).toUpperCase();
      this.nameEl.innerText = `${this.user.fullName} (@${username})`;
      this.metaEl.innerText = `Role: ${this.user.role} · eKYC: ${this.user.status} · SĐT: ${this.user.phone}`;

      updateQuickBarHighlights();
      showToast(`[${this.tag}] Đã đăng nhập: ${username}`, 'success');

      await this.initSignalR();
      await this.loadConversations();
    } else {
      const msg = res.data?.message || 'Đăng nhập thất bại';
      showToast(`[${this.tag}] Lỗi: ${msg}`, 'error');
    }
  }

  logout() {
    if (this.hub) {
      try { this.hub.stop(); } catch {}
      this.hub = null;
    }
    this.token = '';
    this.user = null;
    this.conversations = [];
    this.activeConv = null;
    this.selectedFile = null;

    this.avatarEl.innerText = '?';
    this.nameEl.innerText = 'Chưa đăng nhập';
    this.metaEl.innerText = '—';
    this.rtStatusEl.className = 'rt-status off';
    this.rtStatusEl.innerText = '● Offline';
    this.convListEl.innerHTML = '';
    this.messagesEl.innerHTML = '<div class="empty muted">Vui lòng đăng nhập để xem tin nhắn.</div>';
    this.chatTitleEl.innerText = 'Chưa đăng nhập';
    this.chatSubEl.innerText = '';
    this.claimBtn.hidden = true;
    this.readBtn.hidden = true;
    this.copyIdBtn.hidden = true;
    updateQuickBarHighlights();
    showToast(`[${this.tag}] Đã đăng xuất`, 'info');
  }

  async initSignalR() {
    if (this.hub) {
      try { await this.hub.stop(); } catch {}
      this.hub = null;
    }
    if (!this.token) return;

    this.rtStatusEl.className = 'rt-status off';
    this.rtStatusEl.innerText = '● Connecting...';

    try {
      this.hub = new signalR.HubConnectionBuilder()
        .withUrl(`${backendUrl}/hubs/chat?access_token=${encodeURIComponent(this.token)}`, {
          accessTokenFactory: () => this.token
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

      // SignalR Event handlers
      this.hub.on('ReceiveChatMessage', (msg) => {
        addLog(`RT [${this.tag}]`, `ReceiveChatMessage [${msg.conversationId.slice(0, 8)}...]: ${msg.senderName}: ${msg.content || '[file]'}`);
        if (this.activeConv && this.activeConv.id.toLowerCase() === msg.conversationId.toLowerCase()) {
          this.appendMessage(msg);
          this.markAsRead(this.activeConv.id, false);
        }
        this.updateConvSummary(msg.conversationId, msg.content, msg.createdDate, msg.senderId);
      });

      this.hub.on('ReceiveConversationUpdated', (conv) => {
        addLog(`RT [${this.tag}]`, `ReceiveConversationUpdated: ${conv.displayTitle} (${conv.supportStatus || conv.type})`);
        this.upsertConversation(conv);
      });

      this.hub.on('ReceiveNewPendingSupportConversation', (conv) => {
        addLog(`RT [${this.tag}]`, `[support_queue] Có hội thoại Sàn mới chờ duyệt: ${conv.id}`);
        showToast(`[${this.tag}] Hàng đợi Sàn: Khách ${conv.displayTitle} cần hỗ trợ`, 'info');
        this.upsertConversation(conv);
      });

      this.hub.on('ReceiveSupportConversationClaimed', (convId, staffId, staffName) => {
        addLog(`RT [${this.tag}]`, `[support_queue] Hội thoại ${convId.slice(0, 8)} đã được ${staffName} duyệt!`);
        // Nếu user này không phải staff duyệt và không phải customer -> phải xóa khỏi danh sách ngay!
        if (this.user && this.user.id.toLowerCase() !== staffId.toLowerCase()) {
          const idx = this.conversations.findIndex(c => c.id.toLowerCase() === convId.toLowerCase());
          if (idx !== -1) {
            const conv = this.conversations[idx];
            if (conv.initiator && conv.initiator.userId.toLowerCase() !== this.user.id.toLowerCase()) {
              this.conversations.splice(idx, 1);
              this.renderConversations();
              if (this.activeConv && this.activeConv.id.toLowerCase() === convId.toLowerCase()) {
                this.activeConv = null;
                this.chatTitleEl.innerText = 'Cuộc hội thoại đã được nhân viên khác tiếp nhận riêng tư';
                this.chatSubEl.innerText = 'Bạn không còn quyền truy cập cuộc trò chuyện này.';
                this.messagesEl.innerHTML = '<div class="empty muted">Cuộc trò chuyện đã được chuyển sang chế độ riêng tư tuyệt đối.</div>';
                this.claimBtn.hidden = true;
                this.readBtn.hidden = true;
                this.copyIdBtn.hidden = true;
              }
              showToast(`[${this.tag}] Hội thoại đã được ${staffName} tiếp nhận. Đã ẩn khỏi hàng đợi của bạn.`, 'info');
            }
          }
        }
      });

      this.hub.on('ReceiveMessagesRead', (convId, readerUserId) => {
        addLog(`RT [${this.tag}]`, `ReceiveMessagesRead: conv ${convId.slice(0, 8)} đọc bởi ${readerUserId.slice(0, 8)}`);
        if (this.activeConv && this.activeConv.id.toLowerCase() === convId.toLowerCase()) {
          this.el.querySelectorAll('.msg.mine .msg-meta').forEach(el => {
            if (!el.innerText.includes('Đã xem')) {
              el.innerText += ' · Đã xem ✔';
            }
          });
        }
      });

      this.hub.onreconnecting(() => {
        this.rtStatusEl.className = 'rt-status off';
        this.rtStatusEl.innerText = '● Reconnecting...';
      });

      this.hub.onreconnected(() => {
        this.rtStatusEl.className = 'rt-status on';
        this.rtStatusEl.innerText = '● SignalR Live';
      });

      this.hub.onclose(() => {
        this.rtStatusEl.className = 'rt-status off';
        this.rtStatusEl.innerText = '● Offline';
      });

      await this.hub.start();
      this.rtStatusEl.className = 'rt-status on';
      this.rtStatusEl.innerText = '● SignalR Live';
      addLog(`RT [${this.tag}]`, `SignalR Connected as ${this.user.username}`);
    } catch (err) {
      this.rtStatusEl.className = 'rt-status off';
      this.rtStatusEl.innerText = '● SignalR Lỗi';
      addLog(`RT [${this.tag}]`, `SignalR connect error: ${err.message}`, true);
    }
  }

  // ─── Search Users ────────────────────────────────────────────────────
  async searchUsers(keyword) {
    if (!this.token) {
      showToast(`[${this.tag}] Cần đăng nhập để tìm kiếm`, 'error');
      return;
    }
    const res = await this.api(`/api/Chat/users/search?keyword=${encodeURIComponent(keyword)}`);
    if (!res.ok) {
      this.searchResults.classList.add('open');
      this.searchResults.innerHTML = `<div class="search-item muted">${res.data?.message || 'Lỗi tìm kiếm'}</div>`;
      return;
    }
    const list = res.data?.data || [];
    this.searchResults.classList.add('open');
    if (!list.length) {
      this.searchResults.innerHTML = `<div class="search-item muted">Không tìm thấy người dùng đã eKYC khớp từ khóa.</div>`;
      return;
    }
    this.searchResults.innerHTML = list.map(u => `
      <div class="search-item">
        <div>
          <b>${escapeHtml(u.fullName)}</b> (@${escapeHtml(u.userName)})<br>
          <span class="muted">📧 ${escapeHtml(u.email || '—')} · 📞 ${escapeHtml(u.phoneNumber || '—')}</span>
        </div>
        <button class="btn sm primary start-direct-btn" data-id="${u.userId}" data-name="${escapeHtml(u.fullName)}">Nhắn tin</button>
      </div>
    `).join('');

    this.searchResults.querySelectorAll('.start-direct-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        this.startDirectChat(btn.dataset.id, `Xin chào ${btn.dataset.name}!`);
        this.searchResults.classList.remove('open');
      });
    });
  }

  // ─── Start Direct / Support Chat ─────────────────────────────────────
  async startDirectChat(targetUserId, initialMessage = '') {
    const res = await this.api('/api/Chat/conversations/direct', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ targetUserId, initialMessage })
    });
    if (res.ok && res.data?.data) {
      showToast(`[${this.tag}] Đã mở hội thoại 1-1`, 'success');
      await this.loadConversations();
      this.selectConversation(res.data.data.id);
    } else {
      showToast(`[${this.tag}] Lỗi: ${res.data?.message || 'Không thể tạo chat 1-1'}`, 'error');
    }
  }

  async startSupportChat(initialMessage = 'Xin chào, tôi cần hỗ trợ dịch vụ!') {
    const res = await this.api('/api/Chat/conversations/support', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ initialMessage })
    });
    if (res.ok && res.data?.data) {
      showToast(`[${this.tag}] Đã gửi yêu cầu hỗ trợ tới Sàn`, 'success');
      await this.loadConversations();
      this.selectConversation(res.data.data.id);
    } else {
      showToast(`[${this.tag}] Lỗi: ${res.data?.message || 'Không thể mở chat Sàn'}`, 'error');
    }
  }

  // ─── Load & Render Conversations ─────────────────────────────────────
  async loadConversations() {
    if (!this.token) return;
    const res = await this.api('/api/Chat/conversations?pageSize=50');
    if (res.ok && res.data?.data?.items) {
      this.conversations = res.data.data.items;
      this.renderConversations();
    }
  }

  upsertConversation(conv) {
    const idx = this.conversations.findIndex(c => c.id.toLowerCase() === conv.id.toLowerCase());
    if (idx !== -1) {
      this.conversations[idx] = conv;
    } else {
      this.conversations.unshift(conv);
    }
    this.renderConversations();
  }

  updateConvSummary(convId, preview, time, senderId) {
    const conv = this.conversations.find(c => c.id.toLowerCase() === convId.toLowerCase());
    if (conv) {
      conv.lastMessagePreview = preview;
      conv.lastMessageAt = time;
      conv.lastMessageSenderId = senderId;
      this.renderConversations();
    }
  }

  renderConversations() {
    let filtered = this.conversations.slice();
    if (this.activeFilter === 'DIRECT') {
      filtered = filtered.filter(c => c.type === 'DIRECT' || c.type === 0);
    } else if (this.activeFilter === 'PENDING') {
      filtered = filtered.filter(c => (c.type === 'SUPPORT' || c.type === 1) && (c.supportStatus === 'PENDING' || c.supportStatus === 1));
    } else if (this.activeFilter === 'CLAIMED') {
      filtered = filtered.filter(c => (c.type === 'SUPPORT' || c.type === 1) && (c.supportStatus === 'CLAIMED' || c.supportStatus === 2));
    }

    if (!filtered.length) {
      this.convListEl.innerHTML = `<div class="empty muted">Không có cuộc trò chuyện nào.</div>`;
      return;
    }

    this.convListEl.innerHTML = filtered.map(c => {
      const isDirect = (c.type === 'DIRECT' || c.type === 0);
      const isPending = (c.supportStatus === 'PENDING' || c.supportStatus === 1);
      const isClaimed = (c.supportStatus === 'CLAIMED' || c.supportStatus === 2);
      const isActive = this.activeConv && this.activeConv.id.toLowerCase() === c.id.toLowerCase();
      const initial = c.displayTitle ? c.displayTitle.charAt(0).toUpperCase() : '?';

      return `
        <div class="conv-item ${isActive ? 'active' : ''}" data-id="${c.id}">
          <div class="conv-avatar">${initial}</div>
          <div class="conv-content">
            <div class="conv-row-1">
              <span class="conv-title">${escapeHtml(c.displayTitle || 'Hội thoại')}</span>
              <span class="conv-time">${c.lastMessageAt ? new Date(c.lastMessageAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''}</span>
            </div>
            <div class="conv-preview">${escapeHtml(c.lastMessagePreview || 'Chưa có tin nhắn...')}</div>
            <div class="conv-tags">
              ${isDirect ? `<span class="conv-tag direct">DIRECT</span>` : ''}
              ${!isDirect && isPending ? `<span class="conv-tag pending">SÀN · CHỜ DUYỆT</span>` : ''}
              ${!isDirect && isClaimed ? `<span class="conv-tag claimed">SÀN · ĐÃ DUYỆT</span>` : ''}
              ${c.isPrivateSupport ? `<span class="conv-tag private">🔒 RIÊNG TƯ</span>` : ''}
            </div>
          </div>
          ${c.unreadCount > 0 ? `<span class="conv-unread">${c.unreadCount}</span>` : ''}
        </div>
      `;
    }).join('');

    this.convListEl.querySelectorAll('.conv-item').forEach(el => {
      el.addEventListener('click', () => this.selectConversation(el.dataset.id));
    });
  }

  // ─── Select & Load Messages ──────────────────────────────────────────
  async selectConversation(convId) {
    const conv = this.conversations.find(c => c.id.toLowerCase() === convId.toLowerCase());
    if (this.activeConv && this.hub) {
      try { await this.hub.invoke('LeaveConversation', this.activeConv.id); } catch {}
    }

    this.activeConv = conv || { id: convId, displayTitle: 'Đang tải...' };
    this.renderConversations();

    this.chatTitleEl.innerText = conv?.displayTitle || `Hội thoại: ${convId.slice(0, 8)}...`;
    const isSupport = conv && (conv.type === 'SUPPORT' || conv.type === 1);
    const isPending = conv && (conv.supportStatus === 'PENDING' || conv.supportStatus === 1);
    const isStaffOrAdmin = this.user && (this.user.role === 'ADMIN' || this.user.role === 'CONSULTANT');

    this.chatSubEl.innerText = isSupport
      ? `Hội thoại hỗ trợ với Sàn · Trạng thái: ${conv.supportStatus} ${conv.assignedStaff ? `· Tiếp nhận: ${conv.assignedStaff.fullName}` : '· Chưa tiếp nhận'}`
      : 'Hội thoại trực tiếp giữa 2 người dùng đã xác thực danh tính';

    this.copyIdBtn.hidden = false;
    this.readBtn.hidden = false;
    this.claimBtn.hidden = !(isSupport && isPending && isStaffOrAdmin);

    if (this.hub) {
      try {
        await this.hub.invoke('JoinConversation', convId);
      } catch (err) {
        addLog(`RT [${this.tag}]`, `JoinConversation error: ${err.message}`, true);
      }
    }

    await this.loadMessages(convId);
    this.markAsRead(convId, false);
  }

  async openById(convId) {
    showToast(`[${this.tag}] Mở hội thoại ID: ${convId.slice(0, 8)}...`, 'info');
    await this.selectConversation(convId);
  }

  async loadMessages(convId) {
    this.messagesEl.innerHTML = '<div class="empty muted">Đang nạp tin nhắn...</div>';
    const res = await this.api(`/api/Chat/conversations/${convId}/messages?pageSize=50`);
    if (res.ok && res.data?.data?.items) {
      const msgs = res.data.data.items;
      if (!msgs.length) {
        this.messagesEl.innerHTML = '<div class="empty muted">Chưa có tin nhắn nào trong hội thoại này.</div>';
        return;
      }
      this.messagesEl.innerHTML = '';
      msgs.forEach(m => this.appendMessage(m, false));
      this.scrollToBottom();
    } else {
      this.messagesEl.innerHTML = `<div class="empty muted" style="color:var(--danger)">${res.data?.message || 'Không thể tải tin nhắn. Bạn có thể không có quyền truy cập.'}</div>`;
      if (res.status === 403) {
        showToast(`[${this.tag}] 403 Forbidden: Cuộc trò chuyện này là riêng tư của nhân viên/người dùng khác!`, 'error');
      }
    }
  }

  appendMessage(msg, scroll = true) {
    const isSystem = (msg.messageType === 'SYSTEM' || msg.messageType === 3);
    const isMine = this.user && msg.senderId.toLowerCase() === this.user.id.toLowerCase();
    const timeStr = msg.createdDate ? new Date(msg.createdDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';

    const el = document.createElement('div');
    el.className = `msg ${isSystem ? 'system' : isMine ? 'mine' : 'theirs'}`;

    let attachHtml = '';
    if (msg.attachmentUrl) {
      const isPdf = (msg.messageType === 'PDF_DOCUMENT' || msg.messageType === 2 || (msg.attachmentFileName || '').endsWith('.pdf'));
      attachHtml = `
        <a class="msg-attach" href="${escapeHtml(msg.attachmentUrl)}" target="_blank" rel="noopener">
          ${isPdf ? '📄' : '🖼️'} ${escapeHtml(msg.attachmentFileName || 'Tệp đính kèm')}
        </a>
      `;
    }

    el.innerHTML = `
      ${!isMine && !isSystem ? `<span class="msg-sender">${escapeHtml(msg.senderName)} (${msg.senderRole})</span>` : ''}
      <div class="msg-bubble">${escapeHtml(msg.content || '')}${attachHtml}</div>
      ${!isSystem ? `<span class="msg-meta">${timeStr} ${isMine ? (msg.isRead ? '· Đã xem ✔' : '· Đã gửi') : ''}</span>` : ''}
    `;

    const empty = this.messagesEl.querySelector('.empty');
    if (empty) empty.remove();

    this.messagesEl.appendChild(el);
    if (scroll) this.scrollToBottom();
  }

  scrollToBottom() {
    this.messagesEl.scrollTop = this.messagesEl.scrollHeight;
  }

  // ─── Send Message ────────────────────────────────────────────────────
  async sendMessage(content, file) {
    if (!this.activeConv) {
      showToast(`[${this.tag}] Hãy chọn cuộc trò chuyện trước!`, 'error');
      return;
    }

    const formData = new FormData();
    if (content) formData.append('Content', content);
    if (file) formData.append('AttachmentFile', file);

    const res = await this.api(`/api/Chat/conversations/${this.activeConv.id}/messages`, {
      method: 'POST',
      body: formData
    });

    if (res.ok) {
      this.msgInput.value = '';
      this.attachInput.value = '';
      this.selectedFile = null;
      this.attachNameEl.innerText = '';
    } else {
      showToast(`[${this.tag}] Gửi lỗi: ${res.data?.message || 'Không gửi được tin nhắn'}`, 'error');
    }
  }

  // ─── Claim Support Conversation ──────────────────────────────────────
  async claimConversation(convId) {
    const res = await this.api(`/api/Chat/conversations/${convId}/claim`, {
      method: 'POST'
    });
    if (res.ok) {
      showToast(`[${this.tag}] Duyệt thành công! Hội thoại nay là riêng tư của bạn.`, 'success');
      this.claimBtn.hidden = true;
      await this.loadConversations();
      this.selectConversation(convId);
    } else {
      showToast(`[${this.tag}] Lỗi duyệt: ${res.data?.message || 'Không thể duyệt'}`, 'error');
    }
  }

  async markAsRead(convId, notify = true) {
    const res = await this.api(`/api/Chat/conversations/${convId}/read`, { method: 'POST' });
    if (res.ok && notify) {
      showToast(`[${this.tag}] Đã đánh dấu đọc tin nhắn`, 'info');
      const conv = this.conversations.find(c => c.id.toLowerCase() === convId.toLowerCase());
      if (conv) {
        conv.unreadCount = 0;
        this.renderConversations();
      }
    }
  }
}

// ═══════════════════════════════════════════════════════════════════
// Quick Login Bar Renderer & Switcher
// ═══════════════════════════════════════════════════════════════════
function renderQuickBar() {
  const container = document.getElementById('quickGroups');
  const groups = {
    verified: { title: 'Người mua & Bán (Đã eKYC)', users: [] },
    staff: { title: 'Nhân viên & Quản trị', users: [] },
    unverified: { title: 'Chưa eKYC (Test chặn)', users: [] }
  };

  Object.entries(DEMO_USERS).forEach(([uname, u]) => {
    groups[u.group].users.push({ uname, ...u });
  });

  container.innerHTML = Object.values(groups).map(g => `
    <div class="quick-group">
      <span class="quick-group-title">${g.title}:</span>
      ${g.users.map(u => {
        const roleClass = u.status !== 'VERIFIED' ? 'unverified' : u.role === 'ADMIN' ? 'admin' : u.role === 'CONSULTANT' ? 'staff' : 'user';
        return `
          <button class="pill-user" data-user="${u.uname}">
            <span>${escapeHtml(u.fullName.split(' ')[0])}</span>
            <span class="role-tag ${roleClass}">${u.status !== 'VERIFIED' ? u.status : u.role}</span>
          </button>
        `;
      }).join('')}
    </div>
  `).join('');

  container.querySelectorAll('.pill-user').forEach(btn => {
    btn.addEventListener('click', () => {
      const uname = btn.dataset.user;
      sessions[activeQuickTarget].login(uname);
    });
  });
}

function updateQuickBarHighlights() {
  document.querySelectorAll('.pill-user').forEach(btn => {
    const u = btn.dataset.user;
    btn.classList.toggle('active-A', sessions.A && sessions.A.user && sessions.A.user.username === u);
    btn.classList.toggle('active-B', sessions.B && sessions.B.user && sessions.B.user.username === u);
  });
}

// ═══════════════════════════════════════════════════════════════════
// Scenario Runner (Automated API Test Suite)
// ═══════════════════════════════════════════════════════════════════
const SCENARIOS = [
  { id: 'CHAT-A01', name: 'User PENDING tìm kiếm người dùng -> 403 Forbidden', run: test_CHAT_A01 },
  { id: 'CHAT-A03', name: 'User PENDING mở chat hỗ trợ Sàn -> 403 Forbidden', run: test_CHAT_A03 },
  { id: 'CHAT-B01', name: 'User đã eKYC tìm kiếm qua Email -> 200 OK trả về đúng người', run: test_CHAT_B01 },
  { id: 'CHAT-B02', name: 'User đã eKYC tìm kiếm qua SĐT -> 200 OK trả về đúng người', run: test_CHAT_B02 },
  { id: 'CHAT-C01', name: 'Mở hội thoại 1-1 (DIRECT) giữa 2 user eKYC -> 200 OK', run: test_CHAT_C01 },
  { id: 'CHAT-C03', name: 'Chặn tự chat với chính mình -> 400 BadRequest', run: test_CHAT_C03 },
  { id: 'CHAT-D01', name: 'User gửi tin nhắn cho Sàn -> Trạng thái PENDING (chờ duyệt)', run: test_CHAT_D01 },
  { id: 'CHAT-D02', name: 'Nhân viên & Admin đều thấy hội thoại PENDING trong danh sách', run: test_CHAT_D02 },
  { id: 'CHAT-D04', name: 'Nhân viên gửi tin nhắn khi chưa duyệt -> 400 BadRequest', run: test_CHAT_D04 },
  { id: 'CHAT-D05', name: 'Nhân viên bấm duyệt (Claim) -> Chuyển sang CLAIMED thành công', run: test_CHAT_D05 },
  { id: 'CHAT-D07', name: 'Admin khác không còn thấy hội thoại đã được duyệt trong danh sách', run: test_CHAT_D07 },
  { id: 'CHAT-D08', name: 'Admin khác cố đọc lén tin nhắn của hội thoại đã duyệt -> 403 Forbidden', run: test_CHAT_D08 },
  { id: 'CHAT-D09', name: 'Admin khác cố gửi tin nhắn can thiệp -> 403 Forbidden', run: test_CHAT_D09 },
  { id: 'CHAT-D10', name: 'Admin khác cố bấm duyệt đè cuộc trò chuyện -> 403/409', run: test_CHAT_D10 },
  { id: 'PDF-F02',  name: 'Kiểm định PDF font không nhúng (Helvetica) -> isCrossDeviceSafe = false', run: test_PDF_F02 },
  { id: 'PDF-F04',  name: 'Kiểm định tệp giả mạo không có %PDF -> 400 BadRequest', run: test_PDF_F04 }
];

async function quickAuthToken(username) {
  const cfg = DEMO_USERS[username];
  const res = await fetch(`${backendUrl}/api/Auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userNameOrEmail: username, password: cfg.pass })
  });
  const data = await res.json();
  return data?.data?.token || data?.data?.accessToken || '';
}

async function test_CHAT_A01() {
  const token = await quickAuthToken('user_ekyc_pending');
  const res = await fetch(`${backendUrl}/api/Chat/users/search?keyword=0903444004`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  return { pass: res.status === 403, expected: '403 Forbidden', actual: `${res.status} ${res.statusText}` };
}

async function test_CHAT_A03() {
  const token = await quickAuthToken('user_ekyc_pending');
  const res = await fetch(`${backendUrl}/api/Chat/conversations/support`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ initialMessage: 'Xin hỗ trợ' })
  });
  return { pass: res.status === 403, expected: '403 Forbidden', actual: `${res.status} ${res.statusText}` };
}

async function test_CHAT_B01() {
  const token = await quickAuthToken('bidder_dai_gia');
  const res = await fetch(`${backendUrl}/api/Chat/users/search?keyword=minhtri.bds@gmail.com`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  const data = await res.json();
  const list = data?.data || [];
  const found = list.some(u => u.userName === 'bidder_kinh_doanh');
  return { pass: res.status === 200 && found, expected: '200 OK & chứa bidder_kinh_doanh', actual: `${res.status} (Tìm thấy: ${list.map(u => u.userName).join(', ') || '0'})` };
}

async function test_CHAT_B02() {
  const token = await quickAuthToken('bidder_dai_gia');
  const res = await fetch(`${backendUrl}/api/Chat/users/search?keyword=0903444004`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  const data = await res.json();
  const list = data?.data || [];
  const found = list.some(u => u.userName === 'bidder_kinh_doanh');
  return { pass: res.status === 200 && found, expected: '200 OK & chứa bidder_kinh_doanh', actual: `${res.status} (Tìm thấy: ${list.map(u => u.userName).join(', ') || '0'})` };
}

async function test_CHAT_C01() {
  const token = await quickAuthToken('bidder_dai_gia');
  const searchRes = await fetch(`${backendUrl}/api/Chat/users/search?keyword=0903444004`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  const sData = await searchRes.json();
  const targetId = sData?.data?.[0]?.userId;
  if (!targetId) return { pass: false, expected: 'Tìm thấy targetId', actual: 'Không tìm thấy user' };

  const res = await fetch(`${backendUrl}/api/Chat/conversations/direct`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ targetUserId: targetId, initialMessage: 'Chào bạn!' })
  });
  const data = await res.json();
  const isDirect = (data?.data?.type === 'DIRECT' || data?.data?.type === 0);
  return { pass: res.status === 200 && isDirect, expected: '200 OK & type=DIRECT', actual: `${res.status} (type: ${data?.data?.type})` };
}

async function test_CHAT_C03() {
  const token = await quickAuthToken('bidder_dai_gia');
  // Lấy ID chính mình
  const lRes = await fetch(`${backendUrl}/api/Chat/conversations?pageSize=1`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  // decode JWT lấy sub
  const payload = JSON.parse(atob(token.split('.')[1]));
  const myId = payload.nameid || payload.sub;

  const res = await fetch(`${backendUrl}/api/Chat/conversations/direct`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ targetUserId: myId })
  });
  return { pass: res.status === 400, expected: '400 BadRequest (chặn tự chat)', actual: `${res.status} ${res.statusText}` };
}

let sharedSupportConvId = null;

async function test_CHAT_D01() {
  const token = await quickAuthToken('bidder_phong_thuy');
  const res = await fetch(`${backendUrl}/api/Chat/conversations/support`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ initialMessage: 'Tôi cần hỗ trợ đấu giá SIM!' })
  });
  const data = await res.json();
  sharedSupportConvId = data?.data?.id;
  const isPending = (data?.data?.supportStatus === 'PENDING' || data?.data?.supportStatus === 1);
  return { pass: res.status === 200 && sharedSupportConvId && isPending, expected: '200 OK & PENDING', actual: `${res.status} (id: ${sharedSupportConvId?.slice(0, 8)}, status: ${data?.data?.supportStatus})` };
}

async function test_CHAT_D02() {
  if (!sharedSupportConvId) await test_CHAT_D01();
  const tConsultant = await quickAuthToken('consultant_01');
  const tAdmin = await quickAuthToken('admin');

  const [r1, r2] = await Promise.all([
    fetch(`${backendUrl}/api/Chat/conversations?type=SUPPORT`, { headers: { 'Authorization': `Bearer ${tConsultant}` } }),
    fetch(`${backendUrl}/api/Chat/conversations?type=SUPPORT`, { headers: { 'Authorization': `Bearer ${tAdmin}` } })
  ]);
  const [d1, d2] = await Promise.all([r1.json(), r2.json()]);
  const inC = (d1?.data?.items || []).some(c => c.id.toLowerCase() === sharedSupportConvId.toLowerCase());
  const inA = (d2?.data?.items || []).some(c => c.id.toLowerCase() === sharedSupportConvId.toLowerCase());

  return { pass: inC && inA, expected: 'Cả Consultant & Admin thấy hội thoại PENDING', actual: `Consultant thấy: ${inC}, Admin thấy: ${inA}` };
}

async function test_CHAT_D04() {
  if (!sharedSupportConvId) await test_CHAT_D01();
  const token = await quickAuthToken('consultant_01');
  const fd = new FormData();
  fd.append('Content', 'Tôi trả lời khi chưa duyệt');
  const res = await fetch(`${backendUrl}/api/Chat/conversations/${sharedSupportConvId}/messages`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` },
    body: fd
  });
  return { pass: res.status === 400, expected: '400 BadRequest (chặn trả lời khi chưa Claim)', actual: `${res.status} ${res.statusText}` };
}

async function test_CHAT_D05() {
  if (!sharedSupportConvId) await test_CHAT_D01();
  const token = await quickAuthToken('consultant_01');
  const res = await fetch(`${backendUrl}/api/Chat/conversations/${sharedSupportConvId}/claim`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` }
  });
  const data = await res.json();
  const isClaimed = (data?.data?.supportStatus === 'CLAIMED' || data?.data?.supportStatus === 2);
  return { pass: res.status === 200 && isClaimed, expected: '200 OK & CLAIMED', actual: `${res.status} (status: ${data?.data?.supportStatus})` };
}

async function test_CHAT_D07() {
  if (!sharedSupportConvId) return { pass: false, expected: 'Conv tồn tại', actual: 'Chưa có conv' };
  const token = await quickAuthToken('admin');
  const res = await fetch(`${backendUrl}/api/Chat/conversations?type=SUPPORT`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  const data = await res.json();
  const inList = (data?.data?.items || []).some(c => c.id.toLowerCase() === sharedSupportConvId.toLowerCase());
  return { pass: !inList, expected: 'Admin KHÔNG còn thấy hội thoại đã được duyệt', actual: inList ? 'Vẫn còn thấy (LỖI BẢO MẬT)' : 'Đã ẩn thành công' };
}

async function test_CHAT_D08() {
  if (!sharedSupportConvId) return { pass: false, expected: 'Conv tồn tại', actual: 'Chưa có conv' };
  const token = await quickAuthToken('admin');
  const res = await fetch(`${backendUrl}/api/Chat/conversations/${sharedSupportConvId}/messages`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  return { pass: res.status === 403, expected: '403 Forbidden (chặn đọc lén)', actual: `${res.status} ${res.statusText}` };
}

async function test_CHAT_D09() {
  if (!sharedSupportConvId) return { pass: false, expected: 'Conv tồn tại', actual: 'Chưa có conv' };
  const token = await quickAuthToken('admin');
  const fd = new FormData();
  fd.append('Content', 'Admin cố xen vào');
  const res = await fetch(`${backendUrl}/api/Chat/conversations/${sharedSupportConvId}/messages`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` },
    body: fd
  });
  return { pass: res.status === 403, expected: '403 Forbidden (chặn can thiệp)', actual: `${res.status} ${res.statusText}` };
}

async function test_CHAT_D10() {
  if (!sharedSupportConvId) return { pass: false, expected: 'Conv tồn tại', actual: 'Chưa có conv' };
  const token = await quickAuthToken('admin');
  const res = await fetch(`${backendUrl}/api/Chat/conversations/${sharedSupportConvId}/claim`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` }
  });
  const pass = res.status === 403 || res.status === 409;
  return { pass, expected: '403 hoặc 409 (chặn cướp quyền)', actual: `${res.status} ${res.statusText}` };
}

async function test_PDF_F02() {
  const token = await quickAuthToken('bidder_dai_gia');
  const badPdfBlob = generateBadPdfBlob();
  const fd = new FormData();
  fd.append('file', badPdfBlob, 'test_unembedded.pdf');
  const res = await fetch(`${backendUrl}/api/Documents/pdf/verify-fonts`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` },
    body: fd
  });
  const data = await res.json();
  const safe = data?.data?.isCrossDeviceSafe;
  return { pass: res.status === 200 && safe === false, expected: '200 OK & isCrossDeviceSafe = false', actual: `${res.status} (safe: ${safe})` };
}

async function test_PDF_F04() {
  const token = await quickAuthToken('bidder_dai_gia');
  const fakeBlob = new Blob(['THIS IS NOT A PDF FILE CONTENT'], { type: 'application/pdf' });
  const fd = new FormData();
  fd.append('file', fakeBlob, 'fake.pdf');
  const res = await fetch(`${backendUrl}/api/Documents/pdf/verify-fonts`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` },
    body: fd
  });
  return { pass: res.status === 400, expected: '400 BadRequest (sai Magic Bytes)', actual: `${res.status} ${res.statusText}` };
}

// ═══════════════════════════════════════════════════════════════════
// PDF Generator Helper
// ═══════════════════════════════════════════════════════════════════
function generateBadPdfBlob() {
  // Minimal valid PDF using Standard Type 1 Font (Helvetica) WITHOUT font descriptor/embedding
  const pdfSource = `%PDF-1.4
1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj
2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj
3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj
4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj
5 0 obj << /Length 44 >> stream
BT /F1 12 Tf 50 700 Td (Hello TrustSim) Tj ET
endstream endobj
xref
0 6
0000000000 65535 f 
0000000009 00000 n 
0000000056 00000 n 
0000000111 00000 n 
0000000212 00000 n 
0000000287 00000 n 
trailer << /Size 6 /Root 1 0 R >>
startxref
381
%%EOF`;
  return new Blob([pdfSource], { type: 'application/pdf' });
}

// ═══════════════════════════════════════════════════════════════════
// Init Runner UI
// ═══════════════════════════════════════════════════════════════════
function initRunnerUI() {
  const tbody = document.getElementById('runnerResults');
  tbody.innerHTML = SCENARIOS.map(s => `
    <tr id="row-${s.id}">
      <td><b>${s.id}</b></td>
      <td>${escapeHtml(s.name)}</td>
      <td class="muted">—</td>
      <td class="muted">—</td>
      <td><span class="status-tag muted">CHƯA CHẠY</span></td>
    </tr>
  `).join('');

  document.getElementById('runAllBtn').addEventListener('click', runAllScenarios);
  document.getElementById('clearRunnerBtn').addEventListener('click', initRunnerUI);
}

async function runAllScenarios() {
  const btn = document.getElementById('runAllBtn');
  btn.disabled = true;
  btn.innerText = '⏳ Đang chạy...';

  let passCount = 0;
  let failCount = 0;

  for (const s of SCENARIOS) {
    const tr = document.getElementById(`row-${s.id}`);
    tr.querySelector('td:last-child').innerHTML = `<span class="status-tag running">ĐANG CHẠY...</span>`;
    try {
      const res = await s.run();
      tr.querySelector('td:nth-child(3)').innerText = res.expected;
      tr.querySelector('td:nth-child(4)').innerText = res.actual;
      if (res.pass) {
        passCount++;
        tr.querySelector('td:last-child').innerHTML = `<span class="status-tag pass">PASS</span>`;
      } else {
        failCount++;
        tr.querySelector('td:last-child').innerHTML = `<span class="status-tag fail">FAIL</span>`;
      }
    } catch (err) {
      failCount++;
      tr.querySelector('td:nth-child(4)').innerText = `Exception: ${err.message}`;
      tr.querySelector('td:last-child').innerHTML = `<span class="status-tag fail">FAIL</span>`;
    }
  }

  const summary = document.getElementById('runnerSummary');
  summary.innerHTML = `
    <span class="summary-badge pass">PASS: ${passCount}</span>
    <span class="summary-badge fail">FAIL: ${failCount}</span>
    <span class="summary-badge total">TỔNG: ${SCENARIOS.length}</span>
  `;

  btn.disabled = false;
  btn.innerText = '▶ Chạy tất cả';
  showToast(`Hoàn thành Scenario Runner: ${passCount}/${SCENARIOS.length} PASS`, passCount === SCENARIOS.length ? 'success' : 'error');
}

// ═══════════════════════════════════════════════════════════════════
// PDF Tab Setup
// ═══════════════════════════════════════════════════════════════════
function initPdfTab() {
  const verifyBtn = document.getElementById('verifyPdfBtn');
  const genBadBtn = document.getElementById('genBadPdfBtn');
  const genFakeBtn = document.getElementById('genFakePdfBtn');
  const dlBadBtn = document.getElementById('downloadBadPdfBtn');
  const fileInput = document.getElementById('pdfFileInput');
  const resultBox = document.getElementById('pdfResult');

  async function checkPdfBlob(blob, filename) {
    if (!sessions.A.token) {
      showToast('Cần đăng nhập Phiên A để gọi API kiểm định PDF!', 'error');
      return;
    }
    resultBox.innerHTML = '<div class="muted">⏳ Đang gửi tệp tới máy chủ để kiểm định font...</div>';
    const fd = new FormData();
    fd.append('file', blob, filename);
    const res = await sessions.A.api('/api/Documents/pdf/verify-fonts', {
      method: 'POST',
      body: fd
    });

    if (res.ok && res.data?.data) {
      const d = res.data.data;
      const isSafe = d.isCrossDeviceSafe;
      resultBox.innerHTML = `
        <div class="pdf-audit-card ${isSafe ? 'safe' : 'unsafe'}">
          <h3>${isSafe ? '✅ ĐẠT CHUẨN TƯƠNG THÍCH ĐA THIẾT BỊ' : '❌ NGUY CƠ LỖI FONT TRÊN THIẾT BỊ KHÁC'}</h3>
          <p style="margin: 6px 0;"><b>Thông báo:</b> ${escapeHtml(res.data.message || '—')}</p>
          <div style="display:grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 10px; margin: 10px 0;">
            <div>Số trang: <b>${d.totalPages}</b></div>
            <div>Tổng số font: <b>${d.totalFonts}</b></div>
            <div>Font đã nhúng: <b>${d.embeddedFontsCount}</b></div>
            <div>Font chưa nhúng: <b style="color:var(--danger)">${d.unembeddedFonts?.length || 0}</b></div>
            <div>Bảng mã cũ: <b style="color:var(--danger)">${d.legacyNonUnicodeFonts?.length || 0}</b></div>
            <div>Thiếu /ToUnicode: <b style="color:var(--warning)">${d.missingUnicodeMapFonts?.length || 0}</b></div>
          </div>
          ${d.errors?.length ? `<div style="color:var(--danger); margin-top:8px;"><b>Lỗi:</b><ul>${d.errors.map(e => `<li>${escapeHtml(e)}</li>`).join('')}</ul></div>` : ''}
          ${d.warnings?.length ? `<div style="color:var(--warning); margin-top:8px;"><b>Cảnh báo:</b><ul>${d.warnings.map(w => `<li>${escapeHtml(w)}</li>`).join('')}</ul></div>` : ''}
        </div>
      `;
    } else {
      resultBox.innerHTML = `
        <div class="pdf-audit-card unsafe">
          <h3>❌ TỪ CHỐI TỆP TIN (${res.status})</h3>
          <p>${escapeHtml(res.data?.message || 'Lỗi kiểm định PDF')}</p>
        </div>
      `;
    }
  }

  verifyBtn.addEventListener('click', () => {
    const file = fileInput.files[0];
    if (!file) {
      showToast('Vui lòng chọn một file PDF từ máy của bạn!', 'error');
      return;
    }
    checkPdfBlob(file, file.name);
  });

  genBadBtn.addEventListener('click', () => {
    const blob = generateBadPdfBlob();
    checkPdfBlob(blob, 'unembedded_helvetica_bad.pdf');
  });

  genFakeBtn.addEventListener('click', () => {
    const fakeBlob = new Blob(['THIS IS NOT A VALID PDF'], { type: 'application/pdf' });
    checkPdfBlob(fakeBlob, 'fake_malware.pdf');
  });

  dlBadBtn.addEventListener('click', () => {
    const blob = generateBadPdfBlob();
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'unembedded_helvetica_bad.pdf';
    a.click();
    showToast('Đã tải tệp unembedded_helvetica_bad.pdf mẫu!', 'info');
  });
}

// ═══════════════════════════════════════════════════════════════════
// Main Initializer
// ═══════════════════════════════════════════════════════════════════
window.addEventListener('DOMContentLoaded', () => {
  // Backend switcher
  const bSelect = document.getElementById('backendSelect');
  bSelect.addEventListener('change', () => {
    backendUrl = bSelect.value;
    showToast(`Đã đổi backend sang: ${backendUrl}`, 'info');
  });

  // Layout switcher
  const lSelect = document.getElementById('layoutSelect');
  lSelect.addEventListener('change', () => {
    const sBox = document.getElementById('sessions');
    if (lSelect.value === 'single') {
      sBox.classList.add('single');
      sessions.B.el.style.display = 'none';
    } else {
      sBox.classList.remove('single');
      sessions.B.el.style.display = '';
    }
  });

  // Target toggle for quickbar
  document.querySelectorAll('#targetSeg .seg-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('#targetSeg .seg-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      activeQuickTarget = btn.dataset.target;
    });
  });

  // Tabs switcher
  document.querySelectorAll('.tabs .tab').forEach(tab => {
    tab.addEventListener('click', () => {
      document.querySelectorAll('.tabs .tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
      tab.classList.add('active');
      document.getElementById(`tab-${tab.dataset.tab}`).classList.add('active');
    });
  });

  // Log filter
  document.getElementById('logFilter').addEventListener('change', renderLogs);
  document.getElementById('clearLogBtn').addEventListener('click', () => {
    logEntries = [];
    document.getElementById('logCount').innerText = '0';
    renderLogs();
  });

  // Instantiate Session A & Session B
  sessions.A = new SessionClient('A');
  sessions.B = new SessionClient('B');

  renderQuickBar();
  initRunnerUI();
  initPdfTab();

  // Auto login default demo pair
  sessions.A.login('bidder_dai_gia');
  sessions.B.login('consultant_01');
});
