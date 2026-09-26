/**
 * TrustSim Realtime Auction Testing Arena Engine
 * Handles REST APIs, JWT Auth, SignalR WebSocket Events, and Realtime UI State.
 */

// ─── Configuration & State ───
function getInitialBackendUrl() {
  const saved = localStorage.getItem('trustsim_backend_url');
  if (saved) return saved;

  const port = window.location.port;
  // If served directly by ASP.NET Core backend
  if (port === "5130" || port === "7120" || port === "5000" || port === "5001" || port === "5246") {
    return window.location.origin;
  }

  // Default fallback to ASP.NET Core port 5130 (especially when running Live Server on 5500/5501)
  return "http://localhost:5130";
}

let API_BASE_URL = getInitialBackendUrl();
let HUB_URL = `${API_BASE_URL}/hubs/auction`;

function setBackendUrl(url) {
  API_BASE_URL = url.replace(/\/+$/, '');
  HUB_URL = `${API_BASE_URL}/hubs/auction`;
  localStorage.setItem('trustsim_backend_url', API_BASE_URL);
  syncServerSelectUI();
}

function syncServerSelectUI() {
  const select = document.getElementById('serverUrlSelect');
  if (!select) return;
  const known = Array.from(select.options).map(o => o.value);
  if (known.includes(API_BASE_URL)) {
    select.value = API_BASE_URL;
  } else {
    select.value = 'custom';
  }
}

async function changeBackendServer(val) {
  let target = val;
  if (val === 'custom') {
    const customUrl = prompt('Nhập URL Backend API (ví dụ: http://localhost:5130 hoặc https://localhost:7120):', API_BASE_URL);
    if (!customUrl) {
      syncServerSelectUI();
      return;
    }
    target = customUrl.trim();
  }
  setBackendUrl(target);
  addLog('SYSTEM', `🔄 Đã chuyển sang Backend: ${API_BASE_URL}`);
  
  if (state.currentUser?.userName) {
    await quickLogin(state.currentUser.userName);
  } else {
    await quickLogin('bidder_dai_gia');
  }
  await initSignalR();
  await loadAuctions();
}

let state = {
  currentUser: null,       // { userId, userName, fullName, role, token, available, locked }
  hubConnection: null,     // SignalR HubConnection
  auctions: [],            // List of auctions
  activeFilter: 'ALL',     // ALL | ENGLISH | DUTCH
  activeAuctionId: null,   // Currently selected auction ID
  activeAuction: null,     // Detailed auction object
  activeRoomId: null,      // Group joined in SignalR
  countdownTimer: null,    // Interval timer
  logs: []                 // Event logs
};

// ─── Theme Management ───
function initTheme() {
  const saved = localStorage.getItem('trustsim_theme') || 'light';
  setTheme(saved);
}

function setTheme(theme) {
  const btn = document.getElementById('themeToggleBtn');
  const icon = document.getElementById('themeIcon');
  const text = document.getElementById('themeText');

  if (theme === 'dark') {
    document.documentElement.setAttribute('data-theme', 'dark');
    if (icon) icon.textContent = '🌙';
    if (text) text.textContent = 'Giao diện tối';
  } else {
    document.documentElement.removeAttribute('data-theme');
    if (icon) icon.textContent = '☀️';
    if (text) text.textContent = 'Giao diện sáng';
  }
  localStorage.setItem('trustsim_theme', theme);
}

function toggleTheme() {
  const current = document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
  setTheme(current === 'dark' ? 'light' : 'dark');
}

// ─── App Initialization ───
document.addEventListener('DOMContentLoaded', async () => {
  initTheme();
  syncServerSelectUI();
  addLog('SYSTEM', `Đang khởi động TrustSim Arena Client (API: ${API_BASE_URL})...`);
  // Default login as bidder_dai_gia
  await quickLogin('bidder_dai_gia');
  await initSignalR();
  await loadAuctions();
});

// ─── 1. Authentication & Quick Switcher ───
async function quickLogin(userName) {
  try {
    addLog('SYSTEM', `Đang đăng nhập nhanh tài khoản: ${userName}...`);
    const res = await fetch(`${API_BASE_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        userNameOrEmail: userName,
        password: 'Password123@'
      })
    });

    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Đăng nhập thất bại');
    }

    const auth = data.data;
    state.currentUser = {
      userId: auth.userId,
      userName: auth.userName,
      fullName: auth.fullName,
      role: auth.role,
      token: auth.token
    };

    // Update active UI pill
    document.querySelectorAll('.pill-btn').forEach(btn => {
      btn.classList.toggle('active', btn.dataset.username === userName);
    });

    // Update user header profile
    document.getElementById('userFullName').textContent = auth.fullName || auth.userName;
    document.getElementById('userRoleBadge').textContent = auth.role;
    document.getElementById('userAvatar').textContent = (auth.fullName || auth.userName).substring(0, 2).toUpperCase();

    // Fetch user wallet balance
    await fetchUserWallet();

    addLog('SYSTEM', `✅ Đăng nhập thành công: ${auth.fullName} (${auth.role})`);

    // Re-connect SignalR with new token if already created
    if (state.hubConnection) {
      await reconnectSignalR();
    }

    // Refresh stage participant status if an auction is opened
    if (state.activeAuctionId) {
      await selectAuction(state.activeAuctionId);
    }
  } catch (err) {
    addLog('SYSTEM', `❌ Lỗi đăng nhập: ${err.message}`);
    alert(`Lỗi đăng nhập: ${err.message}`);
  }
}

async function fetchUserWallet() {
  if (!state.currentUser?.token) return;
  try {
    const res = await fetch(`${API_BASE_URL}/api/wallets/me`, {
      headers: { 'Authorization': `Bearer ${state.currentUser.token}` }
    });
    if (res.ok) {
      const data = await res.json();
      if (data.success && data.data) {
        state.currentUser.available = data.data.availableBalance || 0;
        state.currentUser.locked = data.data.lockedBalance || 0;
        document.getElementById('userAvailBalance').textContent = formatVND(state.currentUser.available);
        document.getElementById('userLockedBalance').textContent = formatVND(state.currentUser.locked);
      }
    }
  } catch (e) {
    console.warn('fetchUserWallet error:', e);
  }
}

// ─── 2. SignalR WebSocket Client ───
async function initSignalR() {
  try {
    if (state.hubConnection) {
      await state.hubConnection.stop();
    }

    state.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => state.currentUser?.token || ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Register Event Handlers
    registerSignalREvents(state.hubConnection);

    await state.hubConnection.start();
    updateSignalRIndicator(true);
    addLog('SYSTEM', `✅ Đã kết nối SignalR WebSocket (${HUB_URL})`);

    // Join default listings feed
    await state.hubConnection.invoke('JoinListingFeed');
    document.getElementById('activeRoomTag').textContent = 'Room: Listing Feed';

    // If active auction was selected, re-join its group
    if (state.activeAuctionId) {
      await joinAuctionGroup(state.activeAuctionId);
    }
  } catch (err) {
    updateSignalRIndicator(false);
    addLog('SYSTEM', `⚠️ Không thể kết nối SignalR: ${err.message}. Sẽ tự thử lại...`);
  }
}

async function reconnectSignalR() {
  if (state.hubConnection) {
    try {
      await state.hubConnection.stop();
      await initSignalR();
    } catch (e) {
      console.error(e);
    }
  }
}

function updateSignalRIndicator(connected) {
  const indicator = document.getElementById('signalrIndicator');
  const text = document.getElementById('signalrStatusText');
  if (connected) {
    indicator.classList.add('connected');
    text.textContent = 'SignalR: Connected';
  } else {
    indicator.classList.remove('connected');
    text.textContent = 'SignalR: Disconnected';
  }
}

function registerSignalREvents(connection) {
  // 1. ReceiveNewBid: (bid, newCurrentPrice, totalBids)
  connection.on('ReceiveNewBid', (bid, newCurrentPrice, totalBids) => {
    const bidderText = bid.userNameMasked || 'Người đấu giá';
    addLog('BID', `🔥 Đặt giá mới: ${formatVND(newCurrentPrice)} bởi ${bidderText} (Tổng ${totalBids} lượt)`, bid);
    if (state.activeAuction && state.activeAuction.id.toLowerCase() === bid.auctionId.toLowerCase()) {
      state.activeAuction.currentPrice = newCurrentPrice;
      document.getElementById('stageCurrentPrice').textContent = formatVND(newCurrentPrice);
      document.getElementById('stageLeadingBidderText').textContent = `Dẫn đầu: ${bidderText}`;
      updateBidStepLimits();
      loadBids(state.activeAuction.id);
    }
    fetchUserWallet();
  });

  // 2. ReceivePriceDrop: (currentPrice, nextDropInSeconds)
  connection.on('ReceivePriceDrop', (currentPrice, nextDropInSeconds) => {
    addLog('PRICE_DROP', `🇳🇱 Giảm giá Hà Lan: Còn ${formatVND(currentPrice)} (Nấc tiếp theo sau ${nextDropInSeconds}s)`);
    if (state.activeAuction) {
      state.activeAuction.currentPrice = currentPrice;
      document.getElementById('stageCurrentPrice').textContent = formatVND(currentPrice);
      const tag = document.getElementById('dutchCurrentPriceTag');
      if (tag) tag.textContent = formatVND(currentPrice);
    }
  });

  // 3. ReceiveAuctionExtended: (newEndAt, extensionSeconds)
  connection.on('ReceiveAuctionExtended', (newEndAt, extensionSeconds) => {
    addLog('EXT', `⚡ ANTI-SNIPING KÍCH HOẠT! Gia hạn thêm +${extensionSeconds}s (Hết hạn: ${formatTime(newEndAt)})`);
    if (state.activeAuction) {
      state.activeAuction.endAt = newEndAt;
      const snipeBox = document.getElementById('snipeIndicator');
      if (snipeBox) snipeBox.style.display = 'block';
      startCountdown(newEndAt, state.activeAuction.startAt, state.activeAuction.auctionStatus);
    }
  });

  // 4. ReceiveAuctionStatusChanged: (auctionId, status, winnerId, finalPrice)
  connection.on('ReceiveAuctionStatusChanged', (auctionId, status, winnerId, finalPrice) => {
    addLog('SYSTEM', `📢 Phiên ${auctionId.substring(0, 8)} chuyển trạng thái -> ${status}`);
    if (state.activeAuction && state.activeAuction.id.toLowerCase() === auctionId.toLowerCase()) {
      state.activeAuction.auctionStatus = status;
      document.getElementById('stageStatusTag').textContent = status;
      document.getElementById('stageStatusTag').className = `status-tag ${status.toLowerCase()}`;
    }
    loadAuctions();
  });

  // 5. ReceiveNewAuctionCreated: (auction)
  connection.on('ReceiveNewAuctionCreated', (auction) => {
    addLog('SYSTEM', `✨ Phiên đấu giá mới xuất hiện: ${auction.auctionCode}`);
    loadAuctions();
  });

  // 6. ReceiveAuctionEnded: (result)
  connection.on('ReceiveAuctionEnded', (result) => {
    addLog('END', `🏁 PHIÊN KẾT THÚC! Giá thắng: ${formatVND(result.finalPrice)}, Quán quân: ${result.winnerName || result.winnerId || 'N/A'}`, result);
    if (state.activeAuction && state.activeAuction.id.toLowerCase() === (result.auctionId || '').toLowerCase()) {
      state.activeAuction.auctionStatus = 'END';
      document.getElementById('stageStatusTag').textContent = 'END';
      document.getElementById('stageStatusTag').className = 'status-tag end';
      showSettlementPanel(result);
    }
    loadAuctions();
    fetchUserWallet();
  });

  // 7. ReceiveAuctionFailed: (reason)
  connection.on('ReceiveAuctionFailed', (reason) => {
    addLog('END', `❌ PHIÊN THẤT BẠI: ${reason || 'Không đạt giá sàn hoặc không có người tham gia'}`);
    if (state.activeAuction) {
      state.activeAuction.auctionStatus = 'FAILED';
      document.getElementById('stageStatusTag').textContent = 'FAILED';
      document.getElementById('stageStatusTag').className = 'status-tag end';
    }
    loadAuctions();
  });

  // 8. ReceiveAuctionNotification: (message, type)
  connection.on('ReceiveAuctionNotification', (message, type) => {
    addLog('SYSTEM', `📢 [${type}]: ${message}`);
  });

  // 9. UpdateViewerCount: (viewerCount)
  connection.on('UpdateViewerCount', (count) => {
    addLog('SYSTEM', `👥 Số người đang theo dõi phòng: ${count}`);
  });
}

function showSettlementPanel(data) {
  const panel = document.getElementById('settlementPanel');
  const title = document.getElementById('settlementTitle');
  const desc = document.getElementById('settlementDesc');
  const btnPay = document.getElementById('btnWinnerPay');
  const btnAccept = document.getElementById('btnAcceptRunnerUp');
  const btnReject = document.getElementById('btnRejectRunnerUp');

  panel.style.display = 'flex';
  title.textContent = `Phiên đấu giá đã hoàn tất`;
  desc.textContent = `Người chiến thắng: ${data.winnerName || 'N/A'} - Mức giá: ${formatVND(data.finalPrice)}`;

  const isWinner = state.currentUser?.userId?.toLowerCase() === data.winnerId?.toLowerCase();
  btnPay.style.display = isWinner ? 'block' : 'none';
  btnAccept.style.display = 'none';
  btnReject.style.display = 'none';
}

// ─── 3. Auction Group Management ───
async function joinAuctionGroup(auctionId) {
  if (!state.hubConnection || state.hubConnection.state !== signalR.HubConnectionState.Connected) return;
  try {
    if (state.activeRoomId) {
      await state.hubConnection.invoke('LeaveAuctionGroup', state.activeRoomId);
    }
    await state.hubConnection.invoke('JoinAuctionGroup', auctionId);
    state.activeRoomId = auctionId;
    document.getElementById('activeRoomTag').textContent = `Room: ${auctionId.substring(0, 8)}...`;
    addLog('SYSTEM', `Đã tham gia phòng đấu giá: ${auctionId}`);
  } catch (err) {
    console.error('JoinAuctionGroup error:', err);
  }
}

// ─── 4. Load & Filter Auctions ───
async function loadAuctions() {
  const container = document.getElementById('auctionsListContainer');
  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions?pageSize=50`);
    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Lỗi tải danh sách phiên');
    }

    state.auctions = data.data.items || [];
    renderAuctionsList();
  } catch (err) {
    container.innerHTML = `<div class="error-msg">❌ ${err.message}</div>`;
  }
}

function filterAuctions(filterType) {
  state.activeFilter = filterType;
  document.querySelectorAll('.filter-tab').forEach(tab => {
    tab.classList.toggle('active', tab.dataset.filter === filterType);
  });
  renderAuctionsList();
}

function renderAuctionsList() {
  const container = document.getElementById('auctionsListContainer');
  let filtered = state.auctions;

  if (state.activeFilter === 'ENGLISH') {
    filtered = filtered.filter(a => a.auctionType === 'ENGLISH' || a.auctionType === 1);
  } else if (state.activeFilter === 'DUTCH') {
    filtered = filtered.filter(a => a.auctionType === 'DUTCH' || a.auctionType === 2);
  }

  if (filtered.length === 0) {
    container.innerHTML = `<div class="empty-state">Không có phiên đấu giá nào phù hợp</div>`;
    return;
  }

  container.innerHTML = filtered.map(a => {
    const isSelected = state.activeAuctionId === a.id;
    const statusClass = (a.auctionStatus === 'ACTIVE' || a.auctionStatus === 1) ? 'active' : ((a.auctionStatus === 'UPCOMING' || a.auctionStatus === 0) ? 'upcoming' : 'end');
    const phoneRaw = a.simPhoneNumber || a.phoneNumber || a.simNumber?.phoneNumber || '';
    const simText = formatSimNumber(phoneRaw);
    const carrier = a.carrierName || '';
    const carrierClass = getCarrierClass(carrier);

    return `
      <div class="auction-item-card ${isSelected ? 'active' : ''}" onclick="selectAuction('${a.id}')">
        <div class="auction-item-top">
          <span class="item-code">${a.auctionCode}</span>
          <span class="item-badge ${statusClass}">${a.auctionStatus}</span>
        </div>
        <div class="item-sim-row">
          <span class="item-sim">${simText}</span>
          ${carrier ? `<span class="carrier-pill ${carrierClass}">${carrier}</span>` : ''}
        </div>
        <div class="item-prices">
          <span class="item-type-tag">${a.auctionType}</span>
          <strong class="item-current-price">${formatVND(a.currentPrice || a.startingPrice)}</strong>
        </div>
      </div>
    `;
  }).join('');
}

// ─── 5. Select & Render Live Auction Stage ───
async function selectAuction(auctionId) {
  state.activeAuctionId = auctionId;
  renderAuctionsList();

  document.getElementById('arenaPlaceholder').style.display = 'none';
  document.getElementById('arenaContent').style.display = 'flex';

  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}`, {
      headers: state.currentUser?.token ? { 'Authorization': `Bearer ${state.currentUser.token}` } : {}
    });
    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Lỗi tải chi tiết phiên');
    }

    const a = data.data;
    state.activeAuction = a;

    // Join WebSocket Group
    await joinAuctionGroup(auctionId);

    // Populate Stage Banner
    document.getElementById('stageTypeTag').textContent = a.auctionType;
    document.getElementById('stageStatusTag').textContent = a.auctionStatus;
    document.getElementById('stageStatusTag').className = `status-tag ${String(a.auctionStatus).toLowerCase()}`;
    
    // SIM Phone Number & Carrier
    const phoneRaw = a.simPhoneNumber || a.phoneNumber || a.simNumber?.phoneNumber || '';
    document.getElementById('stageSimNumber').textContent = formatSimNumber(phoneRaw);
    document.getElementById('stageAuctionCode').textContent = a.auctionCode;
    
    // Carrier & Category Badges
    const carrierTag = document.getElementById('stageCarrierTag');
    if (a.carrierName) {
      carrierTag.textContent = a.carrierName;
      carrierTag.className = `carrier-tag ${getCarrierClass(a.carrierName)}`;
      carrierTag.style.display = 'inline-block';
    } else {
      carrierTag.style.display = 'none';
    }

    const catTag = document.getElementById('stageCategoryTag');
    const catText = a.formatRhythmName || (a.categories && a.categories.length > 0 ? a.categories.join(', ') : '');
    if (catText) {
      catTag.textContent = catText;
      catTag.style.display = 'inline-block';
    } else {
      catTag.style.display = 'none';
    }

    // Author & Participants
    document.getElementById('stageSellerName').textContent = a.authorName || 'Người bán';
    document.getElementById('stageParticipantsCount').textContent = a.totalParticipants || (a.participants ? a.participants.length : 0);

    // Prices & Rules
    const step = a.minBidStep || a.rule?.minimumBidIncrement || 500000;
    const deposit = a.depositAmount || a.rule?.depositAmount || 0;
    const maxMultiplier = a.maxStepMultiplier || a.rule?.maxStepMultiplier || 10;

    document.getElementById('stageCurrentPrice').textContent = formatVND(a.currentPrice || a.startingPrice);
    document.getElementById('stageStartingPrice').textContent = formatVND(a.startingPrice);
    document.getElementById('stageStepPrice').textContent = formatVND(step);
    document.getElementById('stageDepositPrice').textContent = formatVND(deposit);
    document.getElementById('stageMaxMultiplier').textContent = `${maxMultiplier}`;

    // Switch Consoles (English vs Dutch)
    const isEnglish = a.auctionType === 'ENGLISH' || a.auctionType === 1;
    document.getElementById('englishBidConsole').style.display = isEnglish ? 'flex' : 'none';
    document.getElementById('dutchBidConsole').style.display = !isEnglish ? 'flex' : 'none';

    if (!isEnglish) {
      document.getElementById('dutchCurrentPriceTag').textContent = formatVND(a.currentPrice || a.startingPrice);
    }

    // Update Step labels & limits
    updateBidStepLimits();

    // Start Live Countdown Timer
    startCountdown(a.endAt, a.startAt, a.auctionStatus);

    // Update Participant status & button
    updateStageParticipantStatus();

    // Hide Settlement panel unless finished
    document.getElementById('settlementPanel').style.display = 'none';

    // Load Bids History
    await loadBids(auctionId);
  } catch (err) {
    alert(`Lỗi tải phiên: ${err.message}`);
  }
}

function updateBidStepLimits() {
  if (!state.activeAuction) return;
  const isEnglish = state.activeAuction.auctionType === 'ENGLISH' || state.activeAuction.auctionType === 1;
  if (!isEnglish) return;

  const a = state.activeAuction;
  const current = a.currentPrice || a.startingPrice;
  const step = a.minBidStep || a.rule?.minimumBidIncrement || 500000;
  const maxN = a.maxStepMultiplier || a.rule?.maxStepMultiplier || 10;

  document.getElementById('stepLabel1').textContent = `+${formatVND(step)}`;
  document.getElementById('stepLabel2').textContent = `+${formatVND(step * 2)}`;
  document.getElementById('stepLabel3').textContent = `+${formatVND(step * 3)}`;
  document.getElementById('stepLabelMax').textContent = `+${formatVND(step * maxN)}`;

  const maxAllowedBid = current + (step * maxN);
  document.getElementById('bidLimitInfo').textContent = `Tối thiểu: ${formatVND(current + step)} | Tối đa (N=${maxN}): ${formatVND(maxAllowedBid)}`;
  document.getElementById('bidAmountInput').value = current + step;
  document.getElementById('bidAmountInput').min = current + step;
  document.getElementById('bidAmountInput').max = maxAllowedBid;
}

function applyStepPreset(multiplier) {
  if (!state.activeAuction) return;
  const a = state.activeAuction;
  const current = a.currentPrice || a.startingPrice;
  const step = a.minBidStep || a.rule?.minimumBidIncrement || 500000;
  const maxN = a.maxStepMultiplier || a.rule?.maxStepMultiplier || 10;

  const n = multiplier === 'max' ? maxN : multiplier;
  const target = current + (step * n);
  document.getElementById('bidAmountInput').value = target;
}

function updateStageParticipantStatus() {
  if (!state.activeAuction || !state.currentUser) return;
  const a = state.activeAuction;
  const userId = state.currentUser.userId;
  const deposit = a.depositAmount || a.rule?.depositAmount || 0;

  const isParticipant = a.isUserRegistered || (a.participants && a.participants.some(p => p.userAccountId?.toLowerCase() === userId?.toLowerCase()));
  const bar = document.getElementById('participantBar');
  const text = document.getElementById('participantStatusText');
  const btn = document.getElementById('btnRegisterAuction');
  const btnPlaceBid = document.getElementById('btnPlaceBid');
  const btnDutchBuy = document.getElementById('btnDutchBuy');

  if (isParticipant) {
    bar.classList.add('registered');
    text.innerHTML = `<span>✅ <strong>ĐÃ ĐẶT CỌC</strong> (${formatVND(deposit)}) - Đủ điều kiện trả giá</span>`;
    btn.style.display = 'none';
    if (btnPlaceBid) btnPlaceBid.disabled = false;
    if (btnDutchBuy) btnDutchBuy.disabled = false;
  } else if (a.auctionStatus === 'SCHEDULE' || a.auctionStatus === 'UPCOMING') {
    bar.classList.remove('registered');
    text.innerHTML = `<span>🔒 Bạn chưa đặt cọc cho phiên này (Tiền cọc: <strong>${formatVND(deposit)}</strong>) - Vui lòng đặt cọc trước khi phiên bắt đầu!</span>`;
    btn.style.display = 'block';
    if (btnPlaceBid) btnPlaceBid.disabled = true;
    if (btnDutchBuy) btnDutchBuy.disabled = true;
  } else {
    bar.classList.remove('registered');
    text.innerHTML = `<span>⛔ <strong>ĐÃ ĐÓNG CỔNG ĐẶT CỌC</strong> (Phiên đang diễn ra/đã kết thúc. Bạn chỉ có thể theo dõi mà không thể đặt giá)</span>`;
    btn.style.display = 'none';
    if (btnPlaceBid) btnPlaceBid.disabled = true;
    if (btnDutchBuy) btnDutchBuy.disabled = true;
  }
}

// ─── 6. Countdown Timer Loop ───
function startCountdown(endAtStr, startAtStr, status) {
  if (state.countdownTimer) clearInterval(state.countdownTimer);

  const endAt = new Date(endAtStr).getTime();
  const startAt = new Date(startAtStr).getTime();

  function update() {
    const now = Date.now();
    const label = document.getElementById('countdownLabel');
    const digits = document.getElementById('countdownDigits');

    if (now < startAt && (status === 'UPCOMING' || status === 'SCHEDULE')) {
      label.textContent = 'Bắt đầu sau';
      digits.textContent = formatDuration(startAt - now);
      digits.style.color = 'var(--accent-primary)';
    } else if (now < endAt) {
      label.textContent = 'Thời gian còn lại';
      digits.textContent = formatDuration(endAt - now);
      digits.style.color = 'var(--accent-gold)';
    } else {
      label.textContent = 'Trạng thái';
      digits.textContent = 'ĐÃ HẾT GIỜ';
      digits.style.color = 'var(--accent-red)';
    }
  }

  update();
  state.countdownTimer = setInterval(update, 1000);
}

function formatDuration(ms) {
  if (ms <= 0) return '00:00:00';
  const totalSeconds = Math.floor(ms / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

// ─── 7. Bidding & Auction Actions ───

// Đăng ký đặt cọc
async function registerCurrentAuction() {
  if (!state.activeAuction || !state.currentUser?.token) return;
  const auctionId = state.activeAuction.id;
  const deposit = state.activeAuction.depositAmount || state.activeAuction.rule?.depositAmount || 0;

  try {
    addLog('SYSTEM', `Đang đăng ký đặt cọc cho phiên ${auctionId}...`);
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/register`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${state.currentUser.token}`,
        'Content-Type': 'application/json'
      }
    });

    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Đăng ký đặt cọc thất bại');
    }

    addLog('SYSTEM', `✅ Đăng ký đặt cọc thành công! Khóa cọc ${formatVND(deposit)}`);
    alert('Đăng ký đặt cọc thành công!');
    await selectAuction(auctionId);
    await fetchUserWallet();
  } catch (err) {
    addLog('SYSTEM', `❌ Lỗi đăng ký: ${err.message}`);
    alert(`Lỗi đăng ký cọc: ${err.message}`);
  }
}

// Trả giá phiên Anh
async function placeBid() {
  if (!state.activeAuction || !state.currentUser?.token) return;
  const auctionId = state.activeAuction.id;
  const amount = Number(document.getElementById('bidAmountInput').value);

  if (!amount || isNaN(amount)) {
    alert('Vui lòng nhập số tiền trả giá hợp lệ');
    return;
  }

  const btn = document.getElementById('btnPlaceBid');
  btn.disabled = true;

  try {
    addLog('BID', `Đang gửi lệnh đặt giá ${formatVND(amount)}...`);
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/bids`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${state.currentUser.token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ bidAmount: amount })
    });

    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Lỗi đặt giá');
    }

    addLog('BID', `✅ Đặt giá THÀNH CÔNG: ${formatVND(amount)}`);
  } catch (err) {
    addLog('BID', `❌ Lỗi đặt giá: ${err.message}`);
    alert(`Lỗi đặt giá: ${err.message}`);
  } finally {
    btn.disabled = false;
  }
}

// Mua ngay phiên Hà Lan
async function acceptDutchBuy() {
  if (!state.activeAuction || !state.currentUser?.token) return;
  const auctionId = state.activeAuction.id;
  if (!confirm(`Bạn có chắc chắn muốn MUA NGAY SIM này với mức giá ${formatVND(state.activeAuction.currentPrice)} không?`)) return;

  const btn = document.getElementById('btnDutchBuy');
  btn.disabled = true;

  try {
    addLog('PRICE_DROP', `Đang gửi yêu cầu MUA NGAY phiên Hà Lan...`);
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/dutch-buy`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${state.currentUser.token}`,
        'Content-Type': 'application/json'
      }
    });

    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Lỗi mua ngay');
    }

    addLog('PRICE_DROP', `🎉 MUA NGAY THÀNH CÔNG! Đang chờ thanh toán.`);
    alert('Mua ngay thành công! Vui lòng hoàn tất thanh toán.');
    await selectAuction(auctionId);
  } catch (err) {
    addLog('PRICE_DROP', `❌ Lỗi mua ngay: ${err.message}`);
    alert(`Lỗi mua ngay: ${err.message}`);
  } finally {
    btn.disabled = false;
  }
}

// Thanh toán người thắng
async function confirmWinnerPayment() {
  if (!state.activeAuction || !state.currentUser?.token) return;
  const auctionId = state.activeAuction.id;

  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/confirm-payment`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${state.currentUser.token}` }
    });
    const data = await res.json();
    if (!res.ok || !data.success) throw new Error(data.message || 'Lỗi thanh toán');

    alert('Thanh toán thành công! SIM đã chính thức thuộc về bạn.');
    await selectAuction(auctionId);
    await fetchUserWallet();
  } catch (err) {
    alert(`Lỗi thanh toán: ${err.message}`);
  }
}

// Á quân chấp nhận
async function acceptRunnerUpOffer() {
  if (!state.activeAuction || !state.currentUser?.token) return;
  const auctionId = state.activeAuction.id;

  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/runner-up/accept`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${state.currentUser.token}` }
    });
    const data = await res.json();
    if (!res.ok || !data.success) throw new Error(data.message || 'Lỗi chấp nhận mua');

    alert('Đã chấp nhận mua SIM (Á Quân)! Vui lòng thanh toán.');
    await selectAuction(auctionId);
  } catch (err) {
    alert(`Lỗi: ${err.message}`);
  }
}

// Á quân từ chối
async function rejectRunnerUpOffer() {
  if (!state.activeAuction || !state.currentUser?.token) return;
  const auctionId = state.activeAuction.id;

  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/runner-up/reject`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${state.currentUser.token}` }
    });
    const data = await res.json();
    if (!res.ok || !data.success) throw new Error(data.message || 'Lỗi từ chối mua');

    alert('Đã từ chối quyền mua.');
    await selectAuction(auctionId);
  } catch (err) {
    alert(`Lỗi: ${err.message}`);
  }
}

// ─── 8. Load Bids History ───
async function loadBids(auctionId) {
  const tbody = document.getElementById('bidsTableBody');
  const countSpan = document.getElementById('bidsCount');

  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/bids?top=50`);
    const data = await res.json();
    if (!res.ok || !data.success) return;

    const bids = data.data || [];
    countSpan.textContent = bids.length;

    if (bids.length === 0) {
      tbody.innerHTML = `<tr><td colspan="4" class="empty-state">Chưa có lượt trả giá nào</td></tr>`;
      return;
    }

    tbody.innerHTML = bids.map(b => {
      const statusClass = b.status === 'WINNING' ? 'winning' : (b.status === 'OUTBID' || b.status === 'OUTBIB' ? 'outbid' : 'rejected');
      const timeStr = new Date(b.placedAt).toLocaleTimeString('vi-VN');
      const bidderText = b.bidderName || b.participant?.userAccount?.userName || 'N/A';

      return `
        <tr class="${statusClass}">
          <td>${timeStr}</td>
          <td><strong>${bidderText}</strong></td>
          <td>${formatVND(b.bidAmount)}</td>
          <td>${b.status}</td>
        </tr>
      `;
    }).join('');
  } catch (e) {
    console.error('Load bids error:', e);
  }
}

// ─── 9. Create Auction Modal ───
async function openCreateAuctionModal() {
  const modal = document.getElementById('createAuctionModal');
  modal.style.display = 'flex';

  // Load user's owned SIMs
  const select = document.getElementById('modalSimSelect');
  select.innerHTML = `<option value="">-- Đang tải SIM sở hữu... --</option>`;

  if (!state.currentUser?.token) {
    select.innerHTML = `<option value="">Vui lòng đăng nhập tài khoản Seller (seller_phuquy hoặc seller_daiphat)</option>`;
    return;
  }

  try {
    const res = await fetch(`${API_BASE_URL}/api/sims/my-ownerships`, {
      headers: { 'Authorization': `Bearer ${state.currentUser.token}` }
    });
    const data = await res.json();
    const ownerships = data.data || [];

    const availableSims = ownerships.filter(o => 
      o.simStatus === 'AVAILABLE' || 
      o.ownershipStatus === 'ACTIVE' || 
      o.simNumber?.simStatus === 'AVAILABLE'
    );

    if (availableSims.length === 0) {
      select.innerHTML = `<option value="">Bạn không có SIM khả dụng nào (Đăng nhập seller_phuquy hoặc seller_daiphat)</option>`;
      return;
    }

    select.innerHTML = availableSims.map(o => {
      const phone = formatSimNumber(o.phoneNumber || o.simNumber?.phoneNumber);
      const carrier = o.carrierName || o.carrierCode || '';
      const price = o.price || o.simNumber?.price || 0;
      return `<option value="${o.simNumberId}">${phone} (${carrier}) - Giá gốc: ${formatVND(price)}</option>`;
    }).join('');
  } catch (err) {
    select.innerHTML = `<option value="">Lỗi tải SIM: ${err.message}</option>`;
  }
}

function closeCreateAuctionModal() {
  document.getElementById('createAuctionModal').style.display = 'none';
}

function toggleAuctionTypeFields() {
  const type = document.getElementById('modalAuctionType').value;
  document.getElementById('englishFieldsRow').style.display = type === 'ENGLISH' ? 'flex' : 'none';
  document.getElementById('dutchFieldsRow').style.display = type === 'DUTCH' ? 'flex' : 'none';
}

async function submitCreateAuction(event) {
  event.preventDefault();
  if (!state.currentUser?.token) {
    alert('Vui lòng đăng nhập!');
    return;
  }

  const simNumberId = document.getElementById('modalSimSelect').value;
  if (!simNumberId) {
    alert('Vui lòng chọn số SIM!');
    return;
  }

  const auctionType = document.getElementById('modalAuctionType').value;
  const presetMin = Number(document.getElementById('modalStartPreset').value);
  const startAt = new Date(Date.now() + presetMin * 60 * 1000).toISOString();
  const durationMin = Number(document.getElementById('modalDurationMinutes').value);
  const endAt = new Date(Date.now() + (presetMin + durationMin) * 60 * 1000).toISOString();

  const body = {
    simNumberId: simNumberId,
    auctionType: auctionType,
    startingPrice: Number(document.getElementById('modalStartingPrice').value),
    reservePrice: Number(document.getElementById('modalReservePrice').value),
    startAt: startAt,
    endAt: endAt,
    registrationStartAt: new Date(Date.now() - 60000).toISOString(),
    registrationEndAt: startAt,
    depositAmount: Number(document.getElementById('modalDepositAmount').value),
    maxStepMultiplier: Number(document.getElementById('modalMaxStepMultiplier')?.value || 10),
    maxSnipeExtensionsPerParticipant: 3,
    paymentGracePeriodHours: 24,
    minimumBidIncrement: Number(document.getElementById('modalMinIncrement')?.value || 5000000),
    dutchDecreaseAmount: Number(document.getElementById('modalDutchDecreaseAmount')?.value || 2000000),
    dutchDecreaseIntervalSeconds: Number(document.getElementById('modalDutchInterval')?.value || 60),
    extensionThresholdSeconds: 120,
    extensionDurationSeconds: 300
  };

  try {
    addLog('SYSTEM', `Đang tạo phiên đấu giá mới...`);
    const res = await fetch(`${API_BASE_URL}/api/auctions`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${state.currentUser.token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(body)
    });

    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Lỗi tạo phiên');
    }

    addLog('SYSTEM', `🎉 ĐÃ TẠO PHIÊN ${data.data.auctionCode}! Đã đẩy vào Redis Delay Queue.`);
    alert(`Tạo phiên thành công! Mã: ${data.data.auctionCode}`);
    closeCreateAuctionModal();
    await loadAuctions();
    await selectAuction(data.data.id);
  } catch (err) {
    addLog('SYSTEM', `❌ Lỗi tạo phiên: ${err.message}`);
    alert(`Lỗi tạo phiên: ${err.message}`);
  }
}

// ─── 10. Realtime Event Log Stream ───
function addLog(type, title, rawData = null) {
  const container = document.getElementById('eventStreamContainer');
  if (!container) return;
  const empty = container.querySelector('.stream-empty');
  if (empty) empty.remove();

  const timeStr = new Date().toLocaleTimeString('vi-VN');
  const logItem = document.createElement('div');
  logItem.className = `log-entry ${type.toLowerCase()}`;
  logItem.dataset.type = type;

  logItem.innerHTML = `
    <div class="log-meta">
      <span class="log-tag">[${type}]</span>
      <span class="log-time">${timeStr}</span>
    </div>
    <div class="log-title">${title}</div>
    ${rawData ? `<div class="log-body">${JSON.stringify(rawData)}</div>` : ''}
  `;

  container.prepend(logItem);

  // Limit logs count to 100
  while (container.children.length > 100) {
    container.removeChild(container.lastChild);
  }
}

function clearEventLogs() {
  document.getElementById('eventStreamContainer').innerHTML = `<div class="stream-empty">Đã xóa sạch nhật ký sự kiện.</div>`;
}

function filterLogs(filterType) {
  document.querySelectorAll('.log-filter').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.logfilter === filterType);
  });

  const entries = document.querySelectorAll('.log-entry');
  entries.forEach(entry => {
    if (filterType === 'ALL' || entry.dataset.type === filterType) {
      entry.style.display = 'flex';
    } else {
      entry.style.display = 'none';
    }
  });
}

function pingServer() {
  addLog('SYSTEM', `📡 Ping SignalR: Kết nối đang ${state.hubConnection?.state || 'Disconnected'}`);
}

// ─── Helper Utilities ───
function formatSimNumber(phone) {
  if (!phone) return 'SIM Đấu Giá';
  const digits = phone.toString().replace(/\D/g, '');
  if (digits.length === 10) {
    return `${digits.substring(0, 4)}.${digits.substring(4, 7)}.${digits.substring(7, 10)}`;
  }
  return phone;
}

function getCarrierClass(carrierName) {
  if (!carrierName) return '';
  const lower = carrierName.toLowerCase();
  if (lower.includes('viettel')) return 'viettel';
  if (lower.includes('vina')) return 'vinaphone';
  if (lower.includes('mobi')) return 'mobifone';
  return '';
}

function formatVND(amount) {
  if (amount === undefined || amount === null) return '0 ₫';
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
}

function formatTime(isoStr) {
  if (!isoStr) return 'N/A';
  return new Date(isoStr).toLocaleTimeString('vi-VN');
}
