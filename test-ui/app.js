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

    // Fetch user pending settlements
    await loadMyPendingSettlements();

    // Fetch auctions to refresh my participated badges & counter
    await loadAuctions();

    // Fetch user unread notifications & show pop-up toast if any
    await loadMyNotifications(true);

    addLog('SYSTEM', `✅ Đăng nhập thành công: ${auth.fullName} (${auth.role})`);

    // Re-connect SignalR with new token if already created
    if (state.hubConnection) {
      await reconnectSignalR();
    }

    // Refresh stage participant status if an auction is opened
    if (state.activeAuctionId) {
      await selectAuction(state.activeAuctionId);
    }

    // Refresh owned SIMs if create auction modal is currently open
    if (document.getElementById('createAuctionModal')?.style.display === 'flex') {
      await loadUserOwnedSims(1);
    }
  } catch (err) {
    addLog('SYSTEM', `❌ Lỗi đăng nhập: ${err.message}`);
    alert(`Lỗi đăng nhập: ${err.message}`);
  }
}

function handleGracePresetChange(val) {
  const input = document.getElementById('modalGracePeriodMinutes');
  if (!input) return;
  if (val !== 'custom') {
    input.value = val;
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

// ─── 2. SignalR WebSocket Client & Room Scoping ───
async function initSignalR() {
  try {
    if (state.hubConnection) {
      await state.hubConnection.stop();
    }

    state.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => state.currentUser?.token || ''
      })
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Register Hub Lifecycle Handlers
    state.hubConnection.onreconnecting((error) => {
      updateSignalRIndicator(false);
      addLog('SYSTEM', `⚠️ SignalR đang kết nối lại... (${error ? error.message : 'mất mạng'})`);
    });

    state.hubConnection.onreconnected(async (connectionId) => {
      updateSignalRIndicator(true);
      addLog('SYSTEM', `✅ SignalR đã kết nối lại thành công! (ID: ${connectionId})`);
      
      // Tự động tham gia lại các phòng sau khi reconnect
      try {
        await state.hubConnection.invoke('JoinListingFeed');
        if (state.currentUser?.userId) {
          await state.hubConnection.invoke('JoinPersonalNotificationFeed', state.currentUser.userId);
        }
        if (state.activeAuctionId) {
          await state.hubConnection.invoke('JoinAuctionGroup', state.activeAuctionId);
          const roomTag = document.getElementById('activeRoomTag');
          if (roomTag) roomTag.textContent = `Room: auction_${state.activeAuctionId.substring(0, 8)}`;
        }
      } catch (err) {
        console.warn('Lỗi re-join room sau khi reconnect:', err);
      }
    });

    state.hubConnection.onclose((error) => {
      updateSignalRIndicator(false);
      addLog('SYSTEM', `❌ SignalR đã ngắt kết nối. (${error ? error.message : 'kết thúc'})`);
    });

    // Register Event Handlers
    registerSignalREvents(state.hubConnection);

    await state.hubConnection.start();
    updateSignalRIndicator(true);
    addLog('SYSTEM', `✅ Đã kết nối SignalR WebSocket (${HUB_URL})`);

    // Join default listings feed
    await state.hubConnection.invoke('JoinListingFeed');
    const roomTag = document.getElementById('activeRoomTag');
    if (roomTag) roomTag.textContent = 'Room: Listing Feed';

    // Auto join user personal feed
    if (state.currentUser?.userId) {
      await state.hubConnection.invoke('JoinPersonalNotificationFeed', state.currentUser.userId);
    }

    // If active auction was selected, re-join its group
    if (state.activeAuctionId) {
      await joinAuctionGroup(state.activeAuctionId);
    }
  } catch (err) {
    updateSignalRIndicator(false);
    addLog('SYSTEM', `⚠️ Không thể kết nối SignalR: ${err.message}. Sẽ tự thử lại...`);
  }
}

async function joinAuctionGroup(auctionId) {
  if (!auctionId) return;
  try {
    // Đảm bảo WebSocket đã kết nối sẵn sàng trước khi invoke
    if (!state.hubConnection || state.hubConnection.state !== signalR.HubConnectionState.Connected) {
      let attempts = 0;
      while ((!state.hubConnection || state.hubConnection.state !== signalR.HubConnectionState.Connected) && attempts < 15) {
        await new Promise(r => setTimeout(r, 200));
        attempts++;
      }
    }

    if (state.hubConnection && state.hubConnection.state === signalR.HubConnectionState.Connected) {
      if (state.activeRoomId && state.activeRoomId.toLowerCase() === auctionId.toLowerCase()) {
        return; // Đã ở trong phòng này, không cần invoke WebSocket lại
      }
      if (state.activeRoomId) {
        await state.hubConnection.invoke('LeaveAuctionGroup', state.activeRoomId);
      }
      await state.hubConnection.invoke('JoinAuctionGroup', auctionId);
      state.activeRoomId = auctionId;
      const roomTag = document.getElementById('activeRoomTag');
      if (roomTag) roomTag.textContent = `Room: auction_${auctionId.substring(0, 8)}`;
      addLog('SYSTEM', `🚪 Đã tham gia phòng SignalR: auction_${auctionId.substring(0, 8)}...`);
    }
  } catch (e) {
    console.warn('joinAuctionGroup error:', e);
  }
}

async function leaveAuctionGroup(auctionId) {
  if (!state.hubConnection || state.hubConnection.state !== signalR.HubConnectionState.Connected || !auctionId) return;
  try {
    await state.hubConnection.invoke('LeaveAuctionGroup', auctionId);
    if (state.activeRoomId && state.activeRoomId.toLowerCase() === auctionId.toLowerCase()) {
      state.activeRoomId = null;
      const roomTag = document.getElementById('activeRoomTag');
      if (roomTag) roomTag.textContent = `Room: Listing Feed`;
    }
  } catch (e) {
    console.warn('leaveAuctionGroup error:', e);
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
    const bidderText = bid.userNameMasked || bid.UserNameMasked || 'Người đấu giá';
    addLog('BID', `🔥 Đặt giá mới: ${formatVND(newCurrentPrice)} bởi ${bidderText} (Tổng ${totalBids || 1} lượt)`, bid);
    
    const bidAuctionId = (bid.auctionId || bid.AuctionId || '').toLowerCase();
    const currentActiveId = (state.activeAuctionId || state.activeAuction?.id || '').toLowerCase();

    if (currentActiveId && (!bidAuctionId || currentActiveId === bidAuctionId)) {
      if (state.activeAuction) {
        state.activeAuction.currentPrice = newCurrentPrice;
      }
      const curPriceEl = document.getElementById('stageCurrentPrice');
      if (curPriceEl) curPriceEl.textContent = formatVND(newCurrentPrice);
      
      const leaderEl = document.getElementById('stageLeadingBidderText');
      if (leaderEl) leaderEl.textContent = `Dẫn đầu: ${bidderText}`;
      
      updateBidStepLimits();

      // Cập nhật DOM bảng Realtime ngay lập tức mà không gọi loadBids đè xóa
      prependBidToTable(bid, newCurrentPrice, totalBids);
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
      const tag = document.getElementById('stageStatusTag');
      if (tag) {
        tag.textContent = status;
        tag.className = `status-tag ${status.toLowerCase()}`;
      }
      updateStageParticipantStatus(); // Tự động mở khóa nút Gửi trả giá / Mua ngay khi phiên ACTIVE
      updateBidStepLimits();          // Cập nhật lại các mốc bước giá
      startCountdown(state.activeAuction.endAt, state.activeAuction.startAt, status);
    }
    debouncedRefreshAll();
  });

  // 5. ReceiveNewAuctionCreated: (auction)
  connection.on('ReceiveNewAuctionCreated', (auction) => {
    addLog('SYSTEM', `✨ Phiên đấu giá mới xuất hiện: ${auction.auctionCode}`);
    debouncedRefreshAll();
  });

  // 6. ReceiveAuctionEnded: (result)
  connection.on('ReceiveAuctionEnded', (result) => {
    addLog('END', `🏁 PHIÊN KẾT THÚC! Giá thắng: ${formatVND(result.finalPrice)}, Quán quân: ${result.winnerName || result.winnerId || 'N/A'}`, result);
    if (state.activeAuction && state.activeAuction.id.toLowerCase() === (result.auctionId || '').toLowerCase()) {
      state.activeAuction.auctionStatus = 'END';
      state.activeAuction.result = result;
      const tag = document.getElementById('stageStatusTag');
      if (tag) {
        tag.textContent = 'END';
        tag.className = 'status-tag end';
      }
      updateStageParticipantStatus();
      startCountdown(state.activeAuction.endAt, state.activeAuction.startAt, 'END', result.auctionResultStatus);
      showSettlementPanel(result);
    }
    debouncedRefreshAll();
  });

  // 7. ReceiveAuctionFailed: (reason)
  connection.on('ReceiveAuctionFailed', (reason) => {
    addLog('END', `❌ PHIÊN THẤT BẠI: ${reason || 'Không đạt giá sàn hoặc không có người tham gia'}`);
    if (state.activeAuction) {
      state.activeAuction.auctionStatus = 'FAILED';
      const tag = document.getElementById('stageStatusTag');
      if (tag) {
        tag.textContent = 'FAILED';
        tag.className = 'status-tag end';
      }
      updateStageParticipantStatus();
      startCountdown(state.activeAuction.endAt, state.activeAuction.startAt, 'FAILED', 'FAILED');
      showSettlementPanel({
        auctionResultStatus: 'FAILED',
        cancelledReason: reason || 'Phiên đấu giá kết thúc thất bại.'
      });
    }
    debouncedRefreshAll();
  });

  // 8. ReceiveAuctionNotification: (message, type)
  connection.on('ReceiveAuctionNotification', (message, type) => {
    addLog('SYSTEM', `📢 [${type}]: ${message}`);
  });

  // 9. UpdateViewerCount: (viewerCount)
  connection.on('UpdateViewerCount', (count) => {
    addLog('SYSTEM', `👥 Số người đang theo dõi phòng: ${count}`);
  });

  // 10. ReceivePersonalNotification: (notification)
  connection.on('ReceivePersonalNotification', (notification) => {
    addLog('SYSTEM', `📬 [THÔNG BÁO CÁ NHÂN]: ${notification.title} - ${notification.content}`);
    showToastNotification(notification);
    debouncedRefreshAll();
    loadMyNotifications(false);
  });
}

function showSettlementPanel(data) {
  const panel = document.getElementById('settlementPanel');
  const title = document.getElementById('settlementTitle');
  const desc = document.getElementById('settlementDesc');
  const btnPay = document.getElementById('btnWinnerPay');
  const btnForfeit = document.getElementById('btnWinnerForfeit');
  const btnSellerApprove = document.getElementById('btnSellerApproveRunnerUp');
  const btnSellerReject = document.getElementById('btnSellerRejectRunnerUp');
  const btnAccept = document.getElementById('btnAcceptRunnerUp');
  const btnReject = document.getElementById('btnRejectRunnerUp');

  panel.style.display = 'flex';
  const status = data.auctionResultStatus || (data.status ? String(data.status) : 'PENDING_PAYMENT');
  const finalPrice = data.finalPrice || data.runnerUpFinalPrice || 0;
  const currentUserId = state.currentUser?.userId?.toLowerCase();
  const isWinner = currentUserId && (data.winnerId?.toLowerCase() === currentUserId || data.winnerUserId?.toLowerCase() === currentUserId);
  const isRunnerUp = currentUserId && (data.runnerUpParticipantId?.toLowerCase() === currentUserId || data.runnerUpUserId?.toLowerCase() === currentUserId);
  const isSeller = currentUserId && (state.activeAuction?.authorId?.toLowerCase() === currentUserId || state.currentUser?.role === 'SELLER');

  // Hide all by default
  if (btnPay) btnPay.style.display = 'none';
  if (btnForfeit) btnForfeit.style.display = 'none';
  if (btnSellerApprove) btnSellerApprove.style.display = 'none';
  if (btnSellerReject) btnSellerReject.style.display = 'none';
  if (btnAccept) btnAccept.style.display = 'none';
  if (btnReject) btnReject.style.display = 'none';

  if (status === 'PENDING_PAYMENT' || status === 1) {
    title.textContent = `⏳ Chờ Người Thắng Hoàn Tất Thanh Toán`;
    const deadlineStr = data.paymentDeadline ? `Hạn chót: ${formatTime(data.paymentDeadline)}` : '';
    desc.textContent = `Người thắng cuộc: ${data.winnerFullName || data.winnerName || data.winnerUserName || data.winnerId || 'N/A'} - Mức giá chốt: ${formatVND(finalPrice)}. ${deadlineStr}`;
    if (isWinner || !currentUserId) {
      if (btnPay) btnPay.style.display = 'inline-block';
      if (btnForfeit) btnForfeit.style.display = 'inline-block';
    }
  } else if (status === 'PENDING_SELLER_CONFIRMATION' || status === 5) {
    title.textContent = `👨‍💼 Chờ Người Bán (Seller) Xác Nhận Bán SIM Cho Á Quân`;
    desc.textContent = `Người thắng ban đầu (${data.initialWinnerUserName || 'Quán quân'}) đã từ bỏ quyền mua (bùng cọc). Tiền cọc đã phạt 50% bồi thường ví Seller & 50% nộp Sàn. Á Quân (${data.runnerUpUserName || data.runnerUpParticipantId || 'N/A'}) có giá bid: ${formatVND(data.runnerUpFinalPrice || finalPrice)}. Vui lòng xác nhận bán.`;
    if (isSeller || !currentUserId) {
      if (btnSellerApprove) btnSellerApprove.style.display = 'inline-block';
      if (btnSellerReject) btnSellerReject.style.display = 'inline-block';
    }
  } else if (status === 'RUNNER_UP_PENDING' || status === 4) {
    title.textContent = `⚠️ Quyền Mua Chuyển Sang Á Quân (Runner-Up)`;
    const ruDeadlineStr = data.runnerUpPaymentDeadline ? `Hạn chót: ${formatTime(data.runnerUpPaymentDeadline)}` : '';
    desc.textContent = `Quán quân ban đầu (${data.initialWinnerUserName || 'Người thắng'}) đã từ bỏ quyền mua / quá hạn thanh toán. Á Quân (${data.runnerUpUserName || data.runnerUpParticipantId || 'N/A'}) có quyền mua lại SIM với giá bid: ${formatVND(data.runnerUpFinalPrice || finalPrice)}. ${ruDeadlineStr}`;
    if (isRunnerUp || !currentUserId) {
      if (btnAccept) btnAccept.style.display = 'inline-block';
      if (btnReject) btnReject.style.display = 'inline-block';
    }
  } else if (status === 'CONFIRMED' || status === 2) {
    const isRunnerUpTakeover = data.winType === 'RUNNER_UP_TAKEOVER' || data.winType === 2;
    const isDutchBuy = data.winType === 'DUTCH_BUY_NOW' || data.winType === 3;

    if (isRunnerUpTakeover) {
      title.innerHTML = `🎉 Giao Dịch Thành Công - 🥈 <strong>Á Quân Tiếp Quản Mua Lại</strong>`;
      desc.innerHTML = `Người mua chốt: <strong>${data.winnerFullName || data.winnerUserName || 'Á Quân'}</strong> (${formatVND(finalPrice)}).<br>Lịch sử: Quán quân ban đầu <em>${data.initialWinnerUserName || 'N/A'}</em> (Giá thắng: ${formatVND(data.initialWinningPrice || 0)}) đã bùng cọc và bị xử phạt theo quy chế. SIM đang chuyển giao!`;
    } else if (isDutchBuy) {
      title.innerHTML = `🎉 Giao Dịch Thành Công - ⚡ <strong>Mua Ngay Phiên Hà Lan</strong>`;
      desc.innerHTML = `Người mua: <strong>${data.winnerFullName || data.winnerUserName || 'Người mua'}</strong> - Mức giá: ${formatVND(finalPrice)}. Quỹ Escrow đã được bảo đảm an toàn!`;
    } else {
      title.innerHTML = `🎉 Giao Dịch Thành Công - 🏆 <strong>Quán Quân Thắng Trực Tiếp</strong>`;
      desc.innerHTML = `Người thắng cuộc: <strong>${data.winnerFullName || data.winnerUserName || 'Quán quân'}</strong> - Mức giá trúng: ${formatVND(finalPrice)}. Quỹ Escrow đã được ký quỹ an toàn!`;
    }
  } else if (status === 'FAILED' || status === 'CANCELLED' || status === 'NO_WINNER' || status === 3 || status === 0) {
    title.textContent = `❌ Phiên Đấu Giá Kết Thúc Thất Bại`;
    desc.textContent = `Lý do: ${data.cancelledReason || 'Không có người chiến thắng hoặc các bên đã từ chối nhận quyền mua SIM.'}`;
  } else {
    title.textContent = `Thông Báo Kết Quả Phiên`;
    desc.textContent = `Mức giá chốt: ${formatVND(finalPrice)}`;
    if (isWinner || !currentUserId) {
      if (btnPay) btnPay.style.display = 'inline-block';
      if (btnForfeit) btnForfeit.style.display = 'inline-block';
    }
  }
}



// ─── 3.5. My Pending Settlements Widget & Smart Top-Up ───
let pendingSettlementTimer = null;

async function loadMyPendingSettlements() {
  const section = document.getElementById('pendingSettlementsSection');
  const countSpan = document.getElementById('pendingSettlementsCount');
  const grid = document.getElementById('pendingSettlementsGrid');
  if (!section || !grid) return;

  if (!state.currentUser?.token) {
    section.style.display = 'none';
    return;
  }

  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions/my-pending-settlements`, {
      headers: { 'Authorization': `Bearer ${state.currentUser.token}` }
    });

    if (!res.ok) {
      section.style.display = 'none';
      return;
    }

    const data = await res.json();
    if (!data.success || !Array.isArray(data.data) || data.data.length === 0) {
      section.style.display = 'none';
      if (countSpan) countSpan.textContent = '0';
      return;
    }

    const items = data.data;
    if (countSpan) countSpan.textContent = items.length;
    section.style.display = 'block';

    renderPendingSettlements(items);
  } catch (err) {
    console.warn('loadMyPendingSettlements error:', err);
  }
}

function renderPendingSettlements(items) {
  const grid = document.getElementById('pendingSettlementsGrid');
  if (!grid) return;

  grid.innerHTML = items.map(item => {
    const roleClass = (item.role || 'WINNER').toLowerCase().replace('_', '-');
    const roleTitle = item.role === 'WINNER' ? '🏆 Người Thắng Cuộc' : (item.role === 'RUNNER_UP' ? '🥈 Á Quân Được Mua' : '👨‍💼 Người Bán (Seller)');
    const phoneFormatted = formatSimNumber(item.simPhoneNumber);

    let priceRows = '';
    let actionButtons = '';
    let deficitBanner = '';

    if (item.role === 'WINNER') {
      priceRows = `
        <div class="pending-breakdown-row"><span class="lbl">Giá chốt trúng:</span><span class="val">${formatVND(item.finalPrice)}</span></div>
        <div class="pending-breakdown-row"><span class="lbl">Đã khấu trừ cọc:</span><span class="val text-cyan">-${formatVND(item.depositLocked)}</span></div>
        <div class="pending-breakdown-row highlight"><span class="lbl">Số tiền cần thanh toán:</span><span class="val">${formatVND(item.remainingPaymentRequired)}</span></div>
        <div class="pending-breakdown-row"><span class="lbl">Số dư khả dụng của bạn:</span><span class="val">${formatVND(item.availableBalance)}</span></div>
      `;

      if (item.deficitAmount > 0) {
        deficitBanner = `
          <div class="deficit-banner">
            <span class="deficit-text">⚠️ Ví thiếu ${formatVND(item.deficitAmount)}</span>
            <button class="btn-smart-topup-inline" onclick="openSmartTopup(${item.deficitAmount})">⚡ Nạp ngay ${formatVND(item.deficitAmount)}</button>
          </div>
        `;
      }

      actionButtons = `
        <button class="btn-pending-action pay" onclick="paySettlementFromPending('${item.auctionId}')" ${item.deficitAmount > 0 ? 'title="Số dư không đủ"' : ''}>💳 Thanh Toán</button>
        <button class="btn-pending-action forfeit" onclick="forfeitFromPending('${item.auctionId}')">💥 Bỏ Mua</button>
      `;
    } else if (item.role === 'RUNNER_UP') {
      priceRows = `
        <div class="pending-breakdown-row"><span class="lbl">Giá chào bán Á quân:</span><span class="val">${formatVND(item.finalPrice)}</span></div>
        <div class="pending-breakdown-row"><span class="lbl">Đã khấu trừ cọc:</span><span class="val text-cyan">-${formatVND(item.depositLocked)}</span></div>
        <div class="pending-breakdown-row highlight"><span class="lbl">Số tiền cần thanh toán:</span><span class="val">${formatVND(item.remainingPaymentRequired)}</span></div>
        <div class="pending-breakdown-row"><span class="lbl">Số dư khả dụng của bạn:</span><span class="val">${formatVND(item.availableBalance)}</span></div>
      `;

      if (item.deficitAmount > 0) {
        deficitBanner = `
          <div class="deficit-banner">
            <span class="deficit-text">⚠️ Ví thiếu ${formatVND(item.deficitAmount)}</span>
            <button class="btn-smart-topup-inline" onclick="openSmartTopup(${item.deficitAmount})">⚡ Nạp ngay ${formatVND(item.deficitAmount)}</button>
          </div>
        `;
      }

      actionButtons = `
        <button class="btn-pending-action approve" onclick="acceptRunnerUpFromPending('${item.auctionId}')">🤝 Nhận Mua</button>
        <button class="btn-pending-action reject" onclick="rejectRunnerUpFromPending('${item.auctionId}')">❌ Từ Chối</button>
      `;
    } else if (item.role === 'SELLER') {
      priceRows = `
        <div class="pending-breakdown-row"><span class="lbl">Giá chốt ban đầu:</span><span class="val">${formatVND(item.finalPrice)}</span></div>
        <div class="pending-breakdown-row highlight"><span class="lbl">Á Quân đặt mua với giá:</span><span class="val text-highlight">${formatVND(item.runnerUpFinalPrice || 0)}</span></div>
        <div class="pending-breakdown-row"><span class="lbl">Trạng thái:</span><span class="val">Chờ bạn duyệt hoặc hủy</span></div>
      `;

      actionButtons = `
        <button class="btn-pending-action approve" onclick="sellerApproveFromPending('${item.auctionId}', true)">✅ Duyệt Bán</button>
        <button class="btn-pending-action reject" onclick="sellerApproveFromPending('${item.auctionId}', false)">🚫 Từ Chối</button>
      `;
    }

    const deadlineData = item.deadline ? new Date(item.deadline).toISOString() : '';

    return `
      <div class="pending-card ${roleClass}" onclick="selectAuction('${item.auctionId}')" style="cursor: pointer;">
        <div class="pending-card-header">
          <div class="pending-sim-info">
            <span class="pending-sim-number">${phoneFormatted}</span>
            <span class="pending-auc-code">${item.auctionCode} (${item.carrierName || 'SIM'})</span>
          </div>
          <span class="role-badge-tag ${roleClass}">${roleTitle}</span>
        </div>

        <div class="pending-price-breakdown">
          ${priceRows}
        </div>

        ${deficitBanner}

        <div class="pending-footer-row" onclick="event.stopPropagation()">
          <div class="pending-deadline-countdown" data-deadline="${deadlineData}">
            ⏳ <span class="countdown-val">Đang tính...</span>
          </div>
          <div class="pending-actions-row">
            ${actionButtons}
          </div>
        </div>
      </div>
    `;
  }).join('');

  startPendingCountdownLoop();
}

function startPendingCountdownLoop() {
  if (pendingSettlementTimer) clearInterval(pendingSettlementTimer);

  function update() {
    const elements = document.querySelectorAll('.pending-deadline-countdown');
    const now = Date.now();

    elements.forEach(el => {
      const deadlineStr = el.dataset.deadline;
      const valSpan = el.querySelector('.countdown-val');
      if (!deadlineStr || !valSpan) return;

      const target = new Date(deadlineStr).getTime();
      const diff = target - now;

      if (diff <= 0) {
        valSpan.textContent = 'HẾT HẠN';
        valSpan.style.color = '#ef4444';
      } else {
        valSpan.textContent = formatDuration(diff);
        valSpan.style.color = '#f59e0b';
      }
    });
  }

  update();
  pendingSettlementTimer = setInterval(update, 1000);
}

function openSmartTopup(amount) {
  openDepositModal();
  const input = document.getElementById('depositAmountInput');
  if (input) input.value = amount;
  generateNewDepositKey();
  createDepositIntent();
}

async function paySettlementFromPending(auctionId) {
  await selectAuction(auctionId);
  await confirmWinnerPayment();
  await loadMyPendingSettlements();
}

async function forfeitFromPending(auctionId) {
  await selectAuction(auctionId);
  await forfeitWinnerPurchase();
  await loadMyPendingSettlements();
}

async function acceptRunnerUpFromPending(auctionId) {
  await selectAuction(auctionId);
  await acceptRunnerUpOffer();
  await loadMyPendingSettlements();
}

async function rejectRunnerUpFromPending(auctionId) {
  await selectAuction(auctionId);
  await rejectRunnerUpOffer();
  await loadMyPendingSettlements();
}

async function sellerApproveFromPending(auctionId, isApproved) {
  await selectAuction(auctionId);
  await sellerDecideRunnerUp(isApproved);
  await loadMyPendingSettlements();
}

// ─── 3.8. Performance Optimization: Debounced Multi-Widget Refresh ───
let refreshDebounceTimer = null;
function debouncedRefreshAll() {
  if (refreshDebounceTimer) clearTimeout(refreshDebounceTimer);
  refreshDebounceTimer = setTimeout(() => {
    loadAuctions();
    loadMyPendingSettlements();
    fetchUserWallet();
  }, 250);
}

// ─── 4. Load & Filter Auctions ───
async function loadAuctions() {
  const container = document.getElementById('auctionsListContainer');
  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions?pageSize=50`, {
      headers: state.currentUser?.token ? { 'Authorization': `Bearer ${state.currentUser.token}` } : {}
    });
    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Lỗi tải danh sách phiên');
    }

    state.auctions = data.data.items || [];
    updateMyAuctionsCounter();
    renderAuctionsList();
  } catch (err) {
    if (container) container.innerHTML = `<div class="error-msg">❌ ${err.message}</div>`;
  }
}

function updateMyAuctionsCounter() {
  const countSpan = document.getElementById('myAuctionsCount');
  if (!countSpan) return;
  const currentUserId = state.currentUser?.userId?.toLowerCase();
  if (!currentUserId) {
    countSpan.textContent = '0';
    return;
  }
  const myCount = state.auctions.filter(a => 
    a.isUserRegistered || 
    (a.authorId && a.authorId.toLowerCase() === currentUserId) ||
    (a.participants && a.participants.some(p => p.userAccountId?.toLowerCase() === currentUserId))
  ).length;
  countSpan.textContent = myCount;
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
  if (!container) return;
  let filtered = state.auctions;
  const currentUserId = state.currentUser?.userId?.toLowerCase();

  if (state.activeFilter === 'ENGLISH') {
    filtered = filtered.filter(a => a.auctionType === 'ENGLISH' || a.auctionType === 1);
  } else if (state.activeFilter === 'DUTCH') {
    filtered = filtered.filter(a => a.auctionType === 'DUTCH' || a.auctionType === 2);
  } else if (state.activeFilter === 'MY_AUCTIONS') {
    filtered = filtered.filter(a => 
      a.isUserRegistered || 
      (a.authorId && a.authorId.toLowerCase() === currentUserId) ||
      (a.participants && a.participants.some(p => p.userAccountId?.toLowerCase() === currentUserId))
    );
  }

  if (filtered.length === 0) {
    const emptyMsg = state.activeFilter === 'MY_AUCTIONS'
      ? 'Bạn chưa tham gia đặt cọc hoặc tạo phiên đấu giá nào.'
      : 'Không có phiên đấu giá nào phù hợp';
    container.innerHTML = `<div class="empty-state" style="padding: 24px 12px; text-align: center; color: var(--text-dim);">${emptyMsg}</div>`;
    return;
  }

  container.innerHTML = filtered.map(a => {
    const isSelected = state.activeAuctionId === a.id;
    const isFailed = a.result?.auctionResultStatus === 'FAILED' || a.auctionStatus === 'FAILED';
    const displayStatus = isFailed ? 'FAILED' : a.auctionStatus;
    const statusClass = (displayStatus === 'ACTIVE' || a.auctionStatus === 1) ? 'active' : ((displayStatus === 'SCHEDULE' || displayStatus === 'UPCOMING' || a.auctionStatus === 0) ? 'upcoming' : 'end');
    const phoneRaw = a.simPhoneNumber || a.phoneNumber || a.simNumber?.phoneNumber || '';
    const simText = formatSimNumber(phoneRaw);
    const carrier = a.carrierName || '';
    const carrierClass = getCarrierClass(carrier);

    const isUserParticipant = a.isUserRegistered || (a.participants && a.participants.some(p => p.userAccountId?.toLowerCase() === currentUserId));
    const isUserSeller = a.authorId && a.authorId.toLowerCase() === currentUserId;

    return `
      <div class="auction-item-card ${isSelected ? 'active' : ''}" onclick="selectAuction('${a.id}')">
        <div class="auction-item-top">
          <span class="item-code">${a.auctionCode}</span>
          <div class="item-badges-group">
            ${isUserSeller ? '<span class="badge-seller">👑 Người bán</span>' : (isUserParticipant ? '<span class="badge-participating">✅ Đã cọc</span>' : '')}
            <span class="item-badge ${statusClass}">${displayStatus}</span>
          </div>
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
    const resultStatus = a.result?.auctionResultStatus;
    const isFailed = resultStatus === 'FAILED' || resultStatus === 3 || resultStatus === 0 || a.auctionStatus === 'FAILED';
    const stageStatusText = isFailed ? 'FAILED' : a.auctionStatus;
    document.getElementById('stageStatusTag').textContent = stageStatusText;
    document.getElementById('stageStatusTag').className = `status-tag ${stageStatusText.toLowerCase()}`;
    
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
    startCountdown(a.endAt, a.startAt, a.auctionStatus, a.result?.auctionResultStatus);

    // Update Participant status & button
    updateStageParticipantStatus();

    // Show Settlement panel if auction has ended or has a result
    if (a.auctionStatus === 'END' || a.auctionStatus === 'ENDED' || a.auctionStatus === 'FAILED' || a.auctionStatus === 'CANCELLED' || a.result) {
      if (a.result) {
        showSettlementPanel(a.result);
      } else {
        showSettlementPanel({
          finalPrice: a.currentPrice,
          auctionResultStatus: a.auctionStatus
        });
      }
    } else {
      document.getElementById('settlementPanel').style.display = 'none';
    }

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
  const statusStr = String(a.auctionStatus || '').toUpperCase();

  if (isParticipant && (statusStr === 'ACTIVE' || statusStr === '2' || a.auctionStatus === 2)) {
    bar.classList.add('registered');
    text.innerHTML = `<span>✅ <strong>ĐÃ ĐẶT CỌC</strong> (${formatVND(deposit)}) - Đủ điều kiện trả giá</span>`;
    btn.style.display = 'none';
    if (btnPlaceBid) btnPlaceBid.disabled = false;
    if (btnDutchBuy) btnDutchBuy.disabled = false;
  } else if (isParticipant && (statusStr === 'SCHEDULE' || statusStr === 'UPCOMING' || statusStr === '1' || statusStr === '0' || a.auctionStatus === 0 || a.auctionStatus === 1)) {
    bar.classList.add('registered');
    text.innerHTML = `<span>✅ <strong>ĐÃ ĐẶT CỌC</strong> (${formatVND(deposit)}) - Chờ phiên bắt đầu để trả giá</span>`;
    btn.style.display = 'none';
    if (btnPlaceBid) btnPlaceBid.disabled = true;
    if (btnDutchBuy) btnDutchBuy.disabled = true;
  } else if (statusStr === 'SCHEDULE' || statusStr === 'UPCOMING' || statusStr === '1' || statusStr === '0' || a.auctionStatus === 0 || a.auctionStatus === 1) {
    bar.classList.remove('registered');
    text.innerHTML = `<span>🔒 Bạn chưa đặt cọc cho phiên này (Tiền cọc: <strong>${formatVND(deposit)}</strong>) - Vui lòng đặt cọc trước khi phiên bắt đầu!</span>`;
    btn.style.display = 'block';
    if (btnPlaceBid) btnPlaceBid.disabled = true;
    if (btnDutchBuy) btnDutchBuy.disabled = true;
  } else {
    bar.classList.remove('registered');
    const isEnded = statusStr === 'END' || statusStr === 'ENDED' || statusStr === 'CANCELLED' || statusStr === 'FAILED' || statusStr === '3';
    const statusText = isEnded ? 'PHIÊN ĐÃ KẾT THÚC / HỦY' : 'ĐÃ ĐÓNG CỔNG ĐẶT CỌC';
    text.innerHTML = `<span>⛔ <strong>${statusText}</strong> (Không thể đặt giá)</span>`;
    btn.style.display = 'none';
    if (btnPlaceBid) btnPlaceBid.disabled = true;
    if (btnDutchBuy) btnDutchBuy.disabled = true;
  }
}

// ─── 6. Countdown Timer Loop ───
function startCountdown(endAtStr, startAtStr, status, resultStatus) {
  if (state.countdownTimer) {
    clearInterval(state.countdownTimer);
    state.countdownTimer = null;
  }

  const label = document.getElementById('countdownLabel');
  const digits = document.getElementById('countdownDigits');
  if (!label || !digits) return;

  const curStatus = String(status || state.activeAuction?.auctionStatus || '').toUpperCase();
  const curResultStatus = String(resultStatus || state.activeAuction?.result?.auctionResultStatus || '').toUpperCase();

  // Nếu phiên đã kết thúc, bị hủy hoặc thất bại -> Dừng đếm ngược ngay
  if (curStatus === 'END' || curStatus === 'ENDED' || curStatus === 'FAILED' || curStatus === 'CANCELLED' || curResultStatus === 'FAILED' || curResultStatus === 'CANCELLED' || curResultStatus === 'NO_WINNER') {
    label.textContent = 'Trạng thái';
    if (curResultStatus === 'FAILED' || curStatus === 'FAILED' || curStatus === 'CANCELLED') {
      digits.textContent = 'ĐÃ HỦY / THẤT BẠI';
    } else {
      digits.textContent = 'ĐÃ KẾT THÚC';
    }
    digits.style.color = 'var(--accent-red)';
    return;
  }

  const endAt = new Date(endAtStr).getTime();
  const startAt = new Date(startAtStr).getTime();

  function update() {
    const now = Date.now();
    const liveStatus = String(state.activeAuction?.auctionStatus || curStatus).toUpperCase();
    const liveResultStatus = String(state.activeAuction?.result?.auctionResultStatus || curResultStatus).toUpperCase();

    if (liveStatus === 'END' || liveStatus === 'ENDED' || liveStatus === 'FAILED' || liveStatus === 'CANCELLED' || liveResultStatus === 'FAILED' || liveResultStatus === 'CANCELLED' || liveResultStatus === 'NO_WINNER') {
      label.textContent = 'Trạng thái';
      digits.textContent = (liveResultStatus === 'FAILED' || liveStatus === 'FAILED' || liveStatus === 'CANCELLED') ? 'ĐÃ HỦY / THẤT BẠI' : 'ĐÃ KẾT THÚC';
      digits.style.color = 'var(--accent-red)';
      if (state.countdownTimer) {
        clearInterval(state.countdownTimer);
        state.countdownTimer = null;
      }
      return;
    }

    if (now < startAt && (liveStatus === 'UPCOMING' || liveStatus === 'SCHEDULE' || liveStatus === '0' || liveStatus === '1')) {
      label.textContent = 'Bắt đầu sau';
      digits.textContent = formatDuration(startAt - now);
      digits.style.color = 'var(--accent-primary)';
    } else if (now >= startAt && now < endAt && (liveStatus === 'UPCOMING' || liveStatus === 'SCHEDULE' || liveStatus === '0' || liveStatus === '1')) {
      // Khi đã chạm mốc giờ bắt đầu -> Tự động chuyển giao diện sang ACTIVE và active nút gửi trả giá
      if (state.activeAuction) {
        state.activeAuction.auctionStatus = 'ACTIVE';
        const tag = document.getElementById('stageStatusTag');
        if (tag) {
          tag.textContent = 'ACTIVE';
          tag.className = 'status-tag active';
        }
        updateStageParticipantStatus();
        updateBidStepLimits();
      }
      label.textContent = 'Thời gian còn lại';
      digits.textContent = formatDuration(endAt - now);
      digits.style.color = 'var(--accent-gold)';
    } else if (now < endAt && (liveStatus === 'ACTIVE' || liveStatus === '2')) {
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
    const bidDto = data.data;
    if (state.activeAuction) {
      state.activeAuction.currentPrice = amount;
    }
    const curPriceEl = document.getElementById('stageCurrentPrice');
    if (curPriceEl) curPriceEl.textContent = formatVND(amount);

    const leaderEl = document.getElementById('stageLeadingBidderText');
    if (leaderEl) leaderEl.textContent = `Dẫn đầu: Bạn (${state.currentUser?.fullName || state.currentUser?.userName || 'Bạn'})`;

    updateBidStepLimits();

    if (bidDto) {
      prependBidToTable(bidDto, amount, null);
    }
    await fetchUserWallet();
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

    if (confirm('🎉 THANH TOÁN ESCROW THÀNH CÔNG!\n\nSố tiền đã được bảo đảm trong quỹ Escrow an toàn. Giao dịch đã chuyển sang trạng thái CHỜ KÝ HỢP ĐỒNG ĐIỆN TỬ (PendingSignature).\n\nBạn có muốn mở ngay Studio Hợp Đồng để xem trước và ký xác nhận OTP không?')) {
      window.open('contract_studio.html', '_blank');
    }
    await selectAuction(auctionId);
    await fetchUserWallet();
  } catch (err) {
    alert(`Lỗi thanh toán: ${err.message}`);
  }
}

// Người thắng chủ động từ bỏ mua SIM (phạt cọc & chuyển quyền mua ngay cho Á Quân)
async function forfeitWinnerPurchase() {
  if (!state.activeAuction || !state.currentUser?.token) return;
  const auctionId = state.activeAuction.id;
  if (!confirm('⚠️ BẠN CÓ CHẮC CHẮN MUỐN TỪ BỎ QUYỀN MUA SIM NÀY KHÔNG?\n\nBạn sẽ bị TỊCH THU TOÀN BỘ TIỀN ĐẶT CỌC và hệ thống sẽ hủy ngay hạn nộp tiền 24h để chuyển quyền mua ngay cho Á Quân.')) return;

  const btn = document.getElementById('btnWinnerForfeit');
  if (btn) btn.disabled = true;

  try {
    addLog('SYSTEM', `Đang gửi yêu cầu từ bỏ quyền mua SIM...`);
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/forfeit-purchase`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${state.currentUser.token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ reason: 'Người thắng chủ động bấm từ bỏ mua SIM trên giao diện Arena.' })
    });

    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Lỗi xử lý từ bỏ mua SIM');
    }

    addLog('SYSTEM', `✅ Đã từ bỏ quyền mua SIM và xử lý phạt cọc thành công.`);
    alert('Đã từ bỏ quyền mua SIM thành công. Tiền cọc đã bị xử phạt và quyền mua được chuyển cho Á Quân.');
    await selectAuction(auctionId);
    await fetchUserWallet();
  } catch (err) {
    addLog('SYSTEM', `❌ Lỗi: ${err.message}`);
    alert(`Lỗi: ${err.message}`);
  } finally {
    if (btn) btn.disabled = false;
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

    if (confirm('🎉 ĐÃ MUA SIM THÀNH CÔNG (Á QUÂN)!\n\nTiền đã được ký quỹ Escrow bảo đảm. Giao dịch đã chuyển sang trạng thái CHỜ KÝ HỢP ĐỒNG ĐIỆN TỬ (PendingSignature).\n\nBạn có muốn mở ngay Studio Hợp Đồng để xem trước và ký xác nhận OTP không?')) {
      window.open('contract_studio.html', '_blank');
    }
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

// Người bán (Seller) duyệt bán hoặc từ chối bán SIM cho Á Quân
async function sellerDecideRunnerUp(isApproved) {
  if (!state.activeAuction || !state.currentUser?.token) return;
  const auctionId = state.activeAuction.id;
  const actionText = isApproved ? 'DUYỆT BÁN CHO Á QUÂN' : 'TỪ CHỐI BÁN CHO Á QUÂN';

  if (!confirm(`Bạn có chắc chắn muốn [${actionText}] đối với phiên này không?`)) return;

  try {
    addLog('SYSTEM', `Đang gửi quyết định Seller (${actionText})...`);
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/runner-up/seller-decision`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${state.currentUser.token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        isApproved: isApproved,
        rejectionReason: isApproved ? null : 'Người bán từ chối trao quyền bán cho Á quân.'
      })
    });

    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Lỗi gửi quyết định Seller');
    }

    addLog('SYSTEM', `✅ ${data.message || 'Đã ghi nhận quyết định của Người bán thành công!'}`);
    alert(`Thành công: ${data.message || actionText}`);
    await selectAuction(auctionId);
    await fetchUserWallet();
  } catch (err) {
    addLog('SYSTEM', `❌ Lỗi: ${err.message}`);
    alert(`Lỗi: ${err.message}`);
  }
}

// ─── 8. Load Bids History & Realtime Prepending ───
function prependBidToTable(bid, currentPrice, totalBids) {
  const tbody = document.getElementById('bidsTableBody');
  const countSpan = document.getElementById('bidsCount');
  if (!tbody) return;

  const bidId = (bid.id || bid.Id || '').toString();

  // Chống nhân bản DOM nếu dòng bid này đã tồn tại trên bảng
  if (bidId && tbody.querySelector(`tr[data-bid-id="${bidId}"]`)) {
    return;
  }

  if (totalBids && countSpan) {
    countSpan.textContent = totalBids;
  } else if (countSpan) {
    const currentCount = parseInt(countSpan.textContent) || 0;
    countSpan.textContent = currentCount + 1;
  }

  // Remove empty-state placeholder row if present
  const emptyRows = tbody.querySelectorAll('.empty-row, tr:has(.empty-state)');
  emptyRows.forEach(r => r.remove());

  // Mark all previous rows as outbid
  tbody.querySelectorAll('tr').forEach(tr => {
    tr.className = 'outbid';
    const tag = tr.querySelector('.status-tag');
    if (tag) {
      tag.className = 'status-tag end';
      tag.textContent = 'BỊ VƯỢT';
    }
  });

  const timeStr = bid.placedAt ? new Date(bid.placedAt).toLocaleTimeString('vi-VN') : new Date().toLocaleTimeString('vi-VN');
  const bidderText = bid.userNameMasked || bid.UserNameMasked || bid.bidderName || 'Đấu giá viên';
  const amount = bid.bidAmount || bid.BidAmount || currentPrice;

  const newRow = document.createElement('tr');
  newRow.className = 'winning';
  if (bidId) newRow.setAttribute('data-bid-id', bidId);
  newRow.innerHTML = `
    <td>${timeStr}</td>
    <td><strong>${bidderText}</strong></td>
    <td>${formatVND(amount)}</td>
    <td><span class="status-tag active" style="padding: 2px 6px; font-size: 10px;">DẪN ĐẦU</span></td>
  `;

  tbody.insertBefore(newRow, tbody.firstChild);
}

async function loadBids(auctionId) {
  const tbody = document.getElementById('bidsTableBody');
  const countSpan = document.getElementById('bidsCount');
  if (!auctionId || !tbody) return;

  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions/${auctionId}/bids?top=50`);
    const data = await res.json();
    if (!res.ok || !data.success) return;

    const bids = data.data || [];
    
    // Nếu API có danh sách bids, render danh sách đầy đủ
    if (bids.length > 0) {
      if (countSpan) countSpan.textContent = bids.length;
      tbody.innerHTML = bids.map((b, idx) => {
        const isWinning = idx === 0 || b.status === 'WINNING' || b.status === 3 || b.status === 'ACCEPTED' || b.status === 1;
        const statusClass = isWinning ? 'winning' : 'outbid';
        const statusText = isWinning ? 'DẪN ĐẦU' : 'BỊ VƯỢT';
        const timeStr = b.placedAt ? new Date(b.placedAt).toLocaleTimeString('vi-VN') : '---';
        const bidderText = b.userNameMasked || b.UserNameMasked || b.bidderName || 'Đấu giá viên';
        const bId = b.id || b.Id || '';

        return `
          <tr class="${statusClass}" ${bId ? `data-bid-id="${bId}"` : ''}>
            <td>${timeStr}</td>
            <td><strong>${bidderText}</strong></td>
            <td>${formatVND(b.bidAmount || b.BidAmount)}</td>
            <td><span class="status-tag ${isWinning ? 'active' : 'end'}" style="padding: 2px 6px; font-size: 10px;">${statusText}</span></td>
          </tr>
        `;
      }).join('');
      return;
    }

    // Nếu API trả về 0 lượt: Chỉ hiển thị placeholder nếu hiện tại bảng chưa có dòng realtime nào
    const existingRows = tbody.querySelectorAll('tr:not(.empty-row)');
    if (existingRows.length === 0) {
      if (countSpan) countSpan.textContent = '0';
      tbody.innerHTML = `<tr class="empty-row"><td colspan="4" class="empty-state">Chưa có lượt trả giá nào</td></tr>`;
    }
  } catch (e) {
    console.error('Load bids error:', e);
  }
}

// ─── 9. Create Auction Modal ───
function parseNumberInput(val) {
  if (val === undefined || val === null) return 0;
  if (typeof val === 'number') return isNaN(val) ? 0 : val;
  const str = val.toString().trim();
  if (!str) return 0;
  const digitsOnly = str.replace(/[^\d]/g, '');
  return digitsOnly ? Number(digitsOnly) : 0;
}

function formatVietnameseCurrencyText(amount) {
  if (!amount || amount <= 0) return '0 ₫';
  if (amount >= 1e12) return (amount / 1e12).toLocaleString('vi-VN', { maximumFractionDigits: 2 }) + ' nghìn tỷ đồng';
  if (amount >= 1e9) return (amount / 1e9).toLocaleString('vi-VN', { maximumFractionDigits: 2 }) + ' tỷ đồng';
  if (amount >= 1e6) return (amount / 1e6).toLocaleString('vi-VN', { maximumFractionDigits: 2 }) + ' triệu đồng';
  if (amount >= 1e3) return (amount / 1e3).toLocaleString('vi-VN', { maximumFractionDigits: 2 }) + ' nghìn đồng';
  return amount.toLocaleString('vi-VN') + ' đồng';
}

let cachedSellerDepositCalculation = null;

async function fetchCalculatedSellerDeposit(startPrice) {
  const parsedPrice = typeof startPrice === 'number' ? startPrice : parseNumberInput(startPrice);
  const amountEl = document.getElementById('sellerDepositAmountText');
  const formulaEl = document.getElementById('sellerDepositFormulaText');
  const breakdownEl = document.getElementById('sellerDepositBreakdownText');
  const badgeEl = document.getElementById('sellerDepositBadge');
  const checkTextEl = document.getElementById('sellerBalanceCheckText');
  const availEl = document.getElementById('sellerAvailableBalanceText');
  const available = state.currentUser?.available || 0;

  if (availEl) availEl.textContent = formatVND(available);

  try {
    const res = await fetch(`${API_BASE_URL}/api/auctions/calculate-seller-deposit?startingPrice=${parsedPrice}`);
    if (!res.ok) {
      throw new Error(`HTTP ${res.status}`);
    }

    const json = await res.json();
    if (!json.success || !json.data) {
      throw new Error(json.message || 'Lỗi tính toán từ server');
    }

    const data = json.data;
    cachedSellerDepositCalculation = data;

    const deposit = data.sellerDepositAmount;
    if (amountEl) {
      amountEl.textContent = formatVND(deposit);
      amountEl.style.color = '#0284c7';
    }

    if (formulaEl && data.formulaText) {
      formulaEl.textContent = data.formulaText;
    }

    if (breakdownEl && data.breakdownText) {
      breakdownEl.innerHTML = data.breakdownText;
    }

    if (available >= deposit) {
      if (badgeEl) {
        badgeEl.textContent = '✅ Đủ số dư ký quỹ';
        badgeEl.style.background = '#22c55e22';
        badgeEl.style.color = '#16a34a';
      }
      if (checkTextEl) {
        checkTextEl.innerHTML = `Số dư khả dụng: <strong style="color:#0f172a;">${formatVND(available)}</strong> (Đủ điều kiện ký quỹ)`;
      }
    } else {
      const deficit = deposit - available;
      if (badgeEl) {
        badgeEl.textContent = '❌ Thiếu số dư cọc';
        badgeEl.style.background = '#ef444422';
        badgeEl.style.color = '#dc2626';
      }
      if (checkTextEl) {
        checkTextEl.innerHTML = `Số dư khả dụng: <strong style="color:#0f172a;">${formatVND(available)}</strong> (Thiếu <span style="color:#dc2626;font-weight:600;">${formatVND(deficit)}</span> - <a href="javascript:void(0)" onclick="openSmartTopup(${deficit})" style="color:#0284c7;font-weight:600;text-decoration:underline;">Nạp ngay</a>)`;
      }
    }
    return deposit;
  } catch (err) {
    console.warn('[SellerDeposit] Lỗi fetch tính cọc từ server:', err);
    // Fallback nếu mất mạng
    const fallbackDeposit = Math.round(parsedPrice * 0.10);
    if (amountEl) amountEl.textContent = formatVND(fallbackDeposit);
    return fallbackDeposit;
  }
}

let startingPriceDebounceTimer = null;

function onStartingPriceChanged() {
  const rawInput = document.getElementById('modalStartingPrice')?.value;
  const startPrice = parseNumberInput(rawInput);

  // Cập nhật ngay dòng nhận diện số tiền bên dưới ô nhập
  const hintEl = document.getElementById('startingPriceReadableHint');
  if (hintEl) {
    if (startPrice > 0) {
      hintEl.textContent = `💵 Nhận diện: ${formatVND(startPrice)} (${formatVietnameseCurrencyText(startPrice)})`;
      hintEl.style.display = 'block';
    } else {
      hintEl.textContent = '💵 Vui lòng nhập số tiền hợp lệ';
      hintEl.style.display = 'block';
    }
  }

  const breakdownEl = document.getElementById('sellerDepositBreakdownText');
  if (breakdownEl) {
    breakdownEl.innerHTML = `<span style="opacity:0.7; color: #0284c7;">⏳ Đang gửi yêu cầu tính cọc lên server...</span>`;
  }

  // Debounce 300ms chống spam HTTP request khi gõ phím
  if (startingPriceDebounceTimer) {
    clearTimeout(startingPriceDebounceTimer);
  }
  startingPriceDebounceTimer = setTimeout(async () => {
    await fetchCalculatedSellerDeposit(startPrice);
    updateDutchPreview();
  }, 300);
}

function onStartingPriceBlur() {
  if (startingPriceDebounceTimer) {
    clearTimeout(startingPriceDebounceTimer);
  }
  const rawInput = document.getElementById('modalStartingPrice')?.value;
  const startPrice = parseNumberInput(rawInput);
  fetchCalculatedSellerDeposit(startPrice);
  updateDutchPreview();
}

// ─── State Management for User's Owned SIMs Filter & Pagination ───
const simOwnershipState = {
  pageIndex: 1,
  pageSize: 20,
  keyword: '',
  sortBy: 'CreatedDate',
  sortOrder: 'Desc',
  totalPages: 1,
  totalCount: 0,
  items: [],
  selectedSim: null,
  debounceTimer: null
};

function debounceFilterSims() {
  const searchInput = document.getElementById('modalSimSearchInput');
  const clearBtn = document.getElementById('modalSimClearSearch');
  if (searchInput && clearBtn) {
    clearBtn.style.display = searchInput.value ? 'block' : 'none';
  }
  clearTimeout(simOwnershipState.debounceTimer);
  simOwnershipState.debounceTimer = setTimeout(() => {
    simOwnershipState.keyword = (searchInput ? searchInput.value : '').trim();
    loadUserOwnedSims(1);
  }, 350);
}

function clearModalSimSearch() {
  const searchInput = document.getElementById('modalSimSearchInput');
  const clearBtn = document.getElementById('modalSimClearSearch');
  if (searchInput) searchInput.value = '';
  if (clearBtn) clearBtn.style.display = 'none';
  simOwnershipState.keyword = '';
  loadUserOwnedSims(1);
}

function onModalSimSortChange() {
  const sortSelect = document.getElementById('modalSimSortSelect');
  if (!sortSelect) return;
  const val = sortSelect.value;
  const parts = val.split('_');
  simOwnershipState.sortBy = parts[0] || 'CreatedDate';
  simOwnershipState.sortOrder = parts[1] || 'Desc';
  loadUserOwnedSims(1);
}

function onModalSimPageSizeChange() {
  const sizeSelect = document.getElementById('modalSimPageSizeSelect');
  if (!sizeSelect) return;
  simOwnershipState.pageSize = Number(sizeSelect.value) || 20;
  loadUserOwnedSims(1);
}

function changeSimPage(delta) {
  const newPage = simOwnershipState.pageIndex + delta;
  if (newPage >= 1 && newPage <= simOwnershipState.totalPages) {
    loadUserOwnedSims(newPage);
  }
}

async function loadUserOwnedSims(pageIndex = 1) {
  const select = document.getElementById('modalSimSelect');
  const previewCard = document.getElementById('modalSelectedSimPreviewCard');
  if (!select) return;

  if (!state.currentUser?.token) {
    select.innerHTML = `<option value="">⚠️ Vui lòng đăng nhập tài khoản Seller (seller_phuquy hoặc seller_daiphat)</option>`;
    if (previewCard) previewCard.style.display = 'none';
    updateSimPaginationControls(0, 0, 1, 1);
    return;
  }

  simOwnershipState.pageIndex = pageIndex;
  select.innerHTML = `<option value="">⏳ Đang tải danh sách SIM sở hữu (Trang ${pageIndex})...</option>`;

  try {
    const params = new URLSearchParams();
    params.set('pageIndex', String(simOwnershipState.pageIndex));
    params.set('pageSize', String(simOwnershipState.pageSize));
    if (simOwnershipState.keyword) {
      params.set('keyword', simOwnershipState.keyword);
    }
    if (simOwnershipState.sortBy) {
      params.set('sortBy', simOwnershipState.sortBy);
    }
    if (simOwnershipState.sortOrder) {
      params.set('sortOrder', simOwnershipState.sortOrder);
    }

    const res = await fetch(`${API_BASE_URL}/api/sims/my-ownerships?${params.toString()}`, {
      headers: { 'Authorization': `Bearer ${state.currentUser.token}` }
    });
    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Lỗi khi tải danh sách SIM sở hữu');
    }

    const paged = data.data;
    const items = paged?.items || (Array.isArray(paged) ? paged : []);
    simOwnershipState.items = items;
    simOwnershipState.pageIndex = paged?.pageIndex || pageIndex;
    simOwnershipState.pageSize = paged?.pageSize || simOwnershipState.pageSize;
    simOwnershipState.totalCount = typeof paged?.totalCount === 'number' ? paged.totalCount : items.length;
    simOwnershipState.totalPages = typeof paged?.totalPages === 'number' ? Math.max(1, paged.totalPages) : 1;

    updateSimPaginationControls(
      simOwnershipState.totalCount,
      items.length,
      simOwnershipState.pageIndex,
      simOwnershipState.totalPages
    );

    if (items.length === 0) {
      const msg = simOwnershipState.keyword 
        ? `Không tìm thấy SIM nào khớp từ khóa "${simOwnershipState.keyword}"`
        : 'Bạn chưa có SIM nào trong tài khoản hoặc chưa sở hữu SIM hợp lệ';
      select.innerHTML = `<option value="">${msg}</option>`;
      if (previewCard) previewCard.style.display = 'none';
      return;
    }

    let defaultSelectValue = '';
    select.innerHTML = `<option value="">-- Chọn một số SIM để đưa vào phiên đấu giá (${items.length} SIM) --</option>` + 
      items.map(o => {
        const phone = formatSimNumber(o.phoneNumber || o.simNumber?.phoneNumber);
        const carrier = o.carrierName || o.carrierCode || 'Viễn thông';
        const price = o.price || o.simNumber?.price || 0;
        const status = o.simStatus || o.simNumber?.simStatus || 'AVAILABLE';
        const isAvail = status === 'AVAILABLE' || status === 1;
        const statusTag = isAvail ? 'Khả dụng' : `Đang ${status}`;
        const rhythm = o.formatRhythmName ? ` [${o.formatRhythmName}]` : '';

        if (isAvail && !defaultSelectValue) {
          defaultSelectValue = o.simNumberId;
        }

        const disabledAttr = isAvail ? '' : 'disabled';
        const prefixIcon = isAvail ? '📱' : '⛔';

        return `<option value="${o.simNumberId}" ${disabledAttr}>${prefixIcon} ${phone} (${carrier}${rhythm}) - ${formatVND(price)} [${statusTag}]</option>`;
      }).join('');

    if (defaultSelectValue) {
      select.value = defaultSelectValue;
    }

    onModalSimSelected();
  } catch (err) {
    select.innerHTML = `<option value="">❌ Lỗi tải SIM: ${err.message}</option>`;
    if (previewCard) previewCard.style.display = 'none';
  }
}

function updateSimPaginationControls(totalCount, currentCount, pageIndex, totalPages) {
  const badge = document.getElementById('modalSimTotalBadge');
  if (badge) badge.textContent = `${totalCount} SIM`;

  const info = document.getElementById('modalSimPageInfo');
  if (info) {
    info.textContent = `Trang ${pageIndex} / ${totalPages} (${totalCount} SIM)`;
  }

  const prevBtn = document.getElementById('btnPrevSimPage');
  if (prevBtn) prevBtn.disabled = pageIndex <= 1;

  const nextBtn = document.getElementById('btnNextSimPage');
  if (nextBtn) nextBtn.disabled = pageIndex >= totalPages;
}

function onModalSimSelected() {
  const select = document.getElementById('modalSimSelect');
  const previewCard = document.getElementById('modalSelectedSimPreviewCard');
  if (!select || !previewCard) return;

  const simId = select.value;
  if (!simId) {
    previewCard.style.display = 'none';
    simOwnershipState.selectedSim = null;
    return;
  }

  const found = simOwnershipState.items.find(i => (i.simNumberId || i.id) === simId);
  simOwnershipState.selectedSim = found;

  if (found) {
    previewCard.style.display = 'block';
    const phone = formatSimNumber(found.phoneNumber || found.simNumber?.phoneNumber);
    const carrier = found.carrierName || found.carrierCode || 'Viễn thông';
    const price = found.price || found.simNumber?.price || 0;
    const simStatus = found.simStatus || found.simNumber?.simStatus || 'AVAILABLE';
    const isAvail = simStatus === 'AVAILABLE' || simStatus === 1;

    const phoneEl = document.getElementById('previewSimNumberText');
    if (phoneEl) phoneEl.textContent = phone;

    const carrierBadge = document.getElementById('previewSimCarrierBadge');
    if (carrierBadge) {
      carrierBadge.textContent = carrier;
      carrierBadge.className = `carrier-pill ${getCarrierClass(carrier)}`;
    }

    const statusBadge = document.getElementById('previewSimStatusBadge');
    if (statusBadge) {
      statusBadge.textContent = isAvail ? 'AVAILABLE' : simStatus;
      statusBadge.style.background = isAvail ? 'rgba(34, 197, 94, 0.15)' : 'rgba(239, 68, 68, 0.15)';
      statusBadge.style.color = isAvail ? '#16a34a' : '#dc2626';
    }

    const priceText = document.getElementById('previewSimPriceText');
    if (priceText) {
      priceText.textContent = formatVND(price);
    }
  } else {
    previewCard.style.display = 'none';
  }
}

function applySimPriceToStartingPrice() {
  if (!simOwnershipState.selectedSim) return;
  const price = simOwnershipState.selectedSim.price || simOwnershipState.selectedSim.simNumber?.price;
  if (price && price > 0) {
    const startInput = document.getElementById('modalStartingPrice');
    if (startInput) {
      startInput.value = formatVND(price).replace('₫', '').trim();
      onStartingPriceChanged();
      onStartingPriceBlur();
      showToast(`Đã áp dụng giá SIM ${formatVND(price)} vào Giá Khởi Điểm!`, 'success');
    }
  }
}

async function openCreateAuctionModal() {
  const modal = document.getElementById('createAuctionModal');
  modal.style.display = 'flex';

  const startingPriceVal = parseNumberInput(document.getElementById('modalStartingPrice')?.value) || 50000000;
  await fetchUserWallet();
  toggleAuctionTypeFields();
  await fetchCalculatedSellerDeposit(startingPriceVal);

  // Load user's owned SIMs via new filter and pagination API
  await loadUserOwnedSims(1);
}

function closeCreateAuctionModal() {
  document.getElementById('createAuctionModal').style.display = 'none';
}

function toggleAuctionTypeFields() {
  const type = document.getElementById('modalAuctionType').value;
  const isEnglish = type === 'ENGLISH';
  document.getElementById('englishFieldsRow').style.display = isEnglish ? 'flex' : 'none';
  const advDetails = document.getElementById('advancedRulesDetails');
  if (advDetails) advDetails.style.display = isEnglish ? 'block' : 'none';
  document.getElementById('dutchFieldsRow').style.display = isEnglish ? 'none' : 'flex';

  // Reserve Price: Hidden for English, shown for Dutch
  const reserveGroup = document.getElementById('reservePriceGroup');
  if (reserveGroup) {
    reserveGroup.style.display = isEnglish ? 'none' : 'block';
  }
  if (isEnglish) {
    const reserveInput = document.getElementById('modalReservePrice');
    if (reserveInput) reserveInput.value = '0';
  } else {
    const reserveInput = document.getElementById('modalReservePrice');
    const startPrice = parseNumberInput(document.getElementById('modalStartingPrice')?.value) || 50000000;
    if (reserveInput && (!reserveInput.value || parseNumberInput(reserveInput.value) <= 0)) {
      reserveInput.value = formatVND(Math.round(startPrice * 0.8)).replace('₫', '').trim();
    }
  }

  updateDutchPreview();
  const startPrice = parseNumberInput(document.getElementById('modalStartingPrice')?.value) || 50000000;
  fetchCalculatedSellerDeposit(startPrice);
}

function updateDutchPreview() {
  const type = document.getElementById('modalAuctionType').value;
  const badge = document.getElementById('dutchPreviewBadge');
  if (type !== 'DUTCH') {
    if (badge) badge.style.display = 'none';
    return;
  }

  const startPrice = parseNumberInput(document.getElementById('modalStartingPrice')?.value);
  const reservePrice = parseNumberInput(document.getElementById('modalReservePrice')?.value);
  const dropAmount = parseNumberInput(document.getElementById('modalDutchDecreaseAmount')?.value);
  const interval = parseNumberInput(document.getElementById('modalDutchInterval')?.value);

  // Update readable hint for Reserve Price
  const reserveHintEl = document.getElementById('reservePriceReadableHint');
  if (reserveHintEl) {
    if (reservePrice > 0) {
      reserveHintEl.textContent = `💵 Nhận diện: ${formatVND(reservePrice)} (${formatVietnameseCurrencyText(reservePrice)})`;
      reserveHintEl.style.display = 'block';
    } else {
      reserveHintEl.style.display = 'none';
    }
  }

  if (startPrice > 0 && reservePrice > 0 && dropAmount > 0 && interval > 0 && startPrice >= reservePrice) {
    const gap = startPrice - reservePrice;
    const steps = Math.ceil(gap / dropAmount);
    const totalSec = steps * interval;
    const minutes = Math.floor(totalSec / 60);
    const seconds = totalSec % 60;
    const durationStr = minutes > 0 ? `${minutes} phút ${seconds > 0 ? seconds + ' giây' : ''}` : `${seconds} giây`;

    if (badge) {
      badge.style.display = 'flex';
      badge.textContent = `💡 Dự kiến: Tự động kết thúc sau ${durationStr} (${steps} nấc giảm ${formatVND(dropAmount)} từ ${formatVND(startPrice)} về sàn ${formatVND(reservePrice)}).`;
    }
    document.getElementById('modalDurationMinutes').value = Math.max(1, Math.ceil(totalSec / 60));
  } else {
    if (badge) badge.style.display = 'none';
  }
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
  const graceMinutes = Number(document.getElementById('modalGracePeriodMinutes')?.value || 1440);
  const extThreshold = Number(document.getElementById('modalExtensionThreshold')?.value || 30);
  const extDuration = Number(document.getElementById('modalExtensionDuration')?.value || 30);
  const maxSnipe = Number(document.getElementById('modalMaxSnipePerParticipant')?.value || 3);
  const maxExt = Number(document.getElementById('modalMaxExtensions')?.value || 5);

  const marginRate = Number(document.getElementById('modalMarginRate')?.value || 0.10);
  const maxGapRatio = Number(document.getElementById('modalMaxPriceGap')?.value || 1.50);
  const minUniqueBidders = Number(document.getElementById('modalMinUniqueBidders')?.value || 2);
  const reqSellerApprove = document.getElementById('modalRequireSellerApproval')?.checked !== false;

  const startingPrice = parseNumberInput(document.getElementById('modalStartingPrice').value);
  const isEnglish = auctionType === 'ENGLISH';
  const reservePrice = isEnglish ? 0 : parseNumberInput(document.getElementById('modalReservePrice').value);

  if (!isEnglish && reservePrice >= startingPrice) {
    alert('Trong đấu giá Hà Lan, Giá Sàn / Dự Trữ phải nhỏ hơn Giá Khởi Điểm!');
    return;
  }

  let sellerDeposit = cachedSellerDepositCalculation?.sellerDepositAmount;
  if (!sellerDeposit) {
    sellerDeposit = await fetchCalculatedSellerDeposit(startingPrice);
  }
  const userAvailable = state.currentUser?.available || 0;
  if (userAvailable < sellerDeposit) {
    const deficit = sellerDeposit - userAvailable;
    if (confirm(`Số dư khả dụng của bạn (${formatVND(userAvailable)}) không đủ để ký quỹ niêm yết SIM (${formatVND(sellerDeposit)}). Thiếu ${formatVND(deficit)}.\n\nBạn có muốn mở cửa sổ Nạp Tiền ngay không?`)) {
      openSmartTopup(deficit);
    }
    return;
  }

  const body = {
    simNumberId: simNumberId,
    auctionType: auctionType,
    startingPrice: startingPrice,
    reservePrice: reservePrice,
    startAt: startAt,
    endAt: endAt,
    registrationStartAt: new Date(Date.now() - 60000).toISOString(),
    registrationEndAt: startAt,
    depositAmount: parseNumberInput(document.getElementById('modalDepositAmount').value),
    sellerDepositAmount: sellerDeposit,
    sellerDepositRate: cachedSellerDepositCalculation?.depositRate || 0.10,
    maxStepMultiplier: Number(document.getElementById('modalMaxStepMultiplier')?.value || 10),
    maxSnipeExtensionsPerParticipant: maxSnipe,
    maximumExtensionCount: maxExt,
    paymentGracePeriodMinutes: graceMinutes,
    paymentGracePeriodHours: Math.max(1, Math.ceil(graceMinutes / 60)),
    minimumBidIncrement: parseNumberInput(document.getElementById('modalMinIncrement')?.value || 5000000),
    dutchDecreaseAmount: parseNumberInput(document.getElementById('modalDutchDecreaseAmount')?.value || 2000000),
    dutchDecreaseIntervalSeconds: Number(document.getElementById('modalDutchInterval')?.value || 60),
    extensionThresholdSeconds: extThreshold,
    extensionDurationSeconds: extDuration,
    marginPercentage: marginRate,
    maxPriceGapRatioForRunnerUp: maxGapRatio,
    minimumUniqueBiddersForRunnerUp: minUniqueBidders,
    requireSellerApprovalForRunnerUp: reqSellerApprove
  };

  try {
    addLog('SYSTEM', `Đang tạo phiên đấu giá mới... (Cọc người bán: ${formatVND(sellerDeposit)})`);
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

    addLog('SYSTEM', `🎉 ĐÃ TẠO PHIÊN ${data.data.auctionCode}! Đã ký quỹ cọc người bán ${formatVND(sellerDeposit)} & đẩy vào Delay Queue.`);
    alert(`Tạo phiên thành công! Mã: ${data.data.auctionCode}\nTiền cọc người bán đã khóa: ${formatVND(sellerDeposit)}`);
    closeCreateAuctionModal();
    await fetchUserWallet();
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

// ─── 11. Notification Center & Toast Engine ───
let userNotifications = [];

async function loadMyNotifications(showToastOnUnread = false) {
  if (!state.currentUser?.token) return;

  try {
    const res = await fetch(`${API_BASE_URL}/api/notifications?pageSize=30`, {
      headers: { 'Authorization': `Bearer ${state.currentUser.token}` }
    });

    if (!res.ok) return;

    const data = await res.json();
    if (data.success && data.data) {
      userNotifications = data.data.items || [];
      const unreadCount = userNotifications.filter(n => !n.isRead).length;
      updateNotificationBadge(unreadCount);

      if (showToastOnUnread && unreadCount > 0) {
        // Hiển thị tối đa 2 toast thông báo chưa đọc mới nhất khi switch account
        const unreadItems = userNotifications.filter(n => !n.isRead);
        unreadItems.slice(0, 2).forEach((n, idx) => {
          setTimeout(() => showToastNotification(n), idx * 400);
        });
      }
    }
  } catch (err) {
    console.warn('loadMyNotifications error:', err);
  }
}

function updateNotificationBadge(unreadCount) {
  const badge = document.getElementById('headerNotifBadge');
  if (!badge) return;

  if (unreadCount > 0) {
    badge.textContent = unreadCount > 99 ? '99+' : unreadCount;
    badge.style.display = 'inline-block';
  } else {
    badge.style.display = 'none';
  }
}

function showToastNotification(notif) {
  const container = document.getElementById('toastContainer');
  if (!container) return;

  const toast = document.createElement('div');
  toast.className = 'toast-item';

  const type = notif.notificationType || 'INFO';
  let icon = '📢';
  if (type === 'AUCTION_WON' || type === 'AUCTION_CONFIRMED') icon = '🎉';
  else if (type === 'AUCTION_FAILED' || type === 'AUCTION_FORFEITED') icon = '💥';
  else if (type === 'RUNNER_UP_OFFER' || type === 'SELLER_APPROVAL_REQUIRED') icon = '🤝';
  else if (type === 'AUCTION_DEPOSIT_REFUNDED' || type === 'WALLET_TOPUP') icon = '💰';
  else if (type === 'PAYMENT_DEADLINE_WARNING') icon = '⚠️';

  toast.innerHTML = `
    <span class="toast-icon">${icon}</span>
    <div class="toast-content" onclick="handleNotificationClick('${notif.id}', '${notif.actionUrl || ''}', '${notif.referenceId || ''}')" style="cursor: pointer;">
      <div class="toast-title">${notif.title || 'Thông báo mới'}</div>
      <div class="toast-desc">${notif.content || ''}</div>
    </div>
    <button class="toast-close" onclick="this.parentElement.remove()">✕</button>
  `;

  container.appendChild(toast);

  // Auto remove toast after 6 seconds
  setTimeout(() => {
    if (toast.parentElement) {
      toast.classList.add('leaving');
      setTimeout(() => toast.remove(), 300);
    }
  }, 6000);
}

function toggleNotificationModal() {
  const modal = document.getElementById('notificationModalBackdrop');
  if (!modal) return;

  if (modal.style.display === 'none' || !modal.style.display) {
    modal.style.display = 'flex';
    renderNotificationsList();
  } else {
    modal.style.display = 'none';
  }
}

function closeNotificationModal() {
  const modal = document.getElementById('notificationModalBackdrop');
  if (modal) modal.style.display = 'none';
}

function renderNotificationsList() {
  const container = document.getElementById('notificationsListContainer');
  if (!container) return;

  if (userNotifications.length === 0) {
    container.innerHTML = `<div class="empty-state">Bạn không có thông báo nào.</div>`;
    return;
  }

  container.innerHTML = userNotifications.map(n => {
    const isUnread = !n.isRead;
    const timeStr = n.createdDate ? new Date(n.createdDate).toLocaleString('vi-VN') : 'Vừa xong';
    
    let icon = '📢';
    const type = n.notificationType || 'INFO';
    if (type === 'AUCTION_WON' || type === 'AUCTION_CONFIRMED') icon = '🎉';
    else if (type === 'AUCTION_FAILED' || type === 'AUCTION_FORFEITED') icon = '💥';
    else if (type === 'RUNNER_UP_OFFER' || type === 'SELLER_APPROVAL_REQUIRED') icon = '🤝';
    else if (type === 'AUCTION_DEPOSIT_REFUNDED' || type === 'WALLET_TOPUP') icon = '💰';
    else if (type === 'PAYMENT_DEADLINE_WARNING') icon = '⚠️';

    return `
      <div class="notif-card-item ${isUnread ? 'unread' : ''}" onclick="handleNotificationClick('${n.id}', '${n.actionUrl || ''}', '${n.referenceId || ''}')">
        <span class="notif-icon-box">${icon}</span>
        <div class="notif-body-box">
          <div class="notif-item-title">${n.title || 'Thông báo'}</div>
          <div class="notif-item-content">${n.content || ''}</div>
          <div class="notif-meta-row">
            <span>🕒 ${timeStr}</span>
            ${isUnread ? '<span class="notif-unread-dot" title="Chưa đọc"></span>' : '<span style="color: var(--text-muted);">Đã đọc</span>'}
          </div>
        </div>
      </div>
    `;
  }).join('');
}

async function handleNotificationClick(notifId, actionUrl, referenceId) {
  // Mark as read in backend
  await markNotificationAsRead(notifId);

  // If action relates to an auction, auto-select it
  if (referenceId && referenceId !== 'null' && referenceId !== 'undefined') {
    closeNotificationModal();
    await selectAuction(referenceId);
  } else if (actionUrl && actionUrl.includes('/auctions/')) {
    const parts = actionUrl.split('/');
    const aucId = parts[parts.length - 1];
    if (aucId) {
      closeNotificationModal();
      await selectAuction(aucId);
    }
  }
}

async function markNotificationAsRead(notifId) {
  if (!state.currentUser?.token || !notifId) return;

  try {
    const res = await fetch(`${API_BASE_URL}/api/notifications/${notifId}/read`, {
      method: 'PATCH',
      headers: { 'Authorization': `Bearer ${state.currentUser.token}` }
    });

    if (res.ok) {
      const item = userNotifications.find(n => n.id === notifId);
      if (item) item.isRead = true;
      const unreadCount = userNotifications.filter(n => !n.isRead).length;
      updateNotificationBadge(unreadCount);
      renderNotificationsList();
    }
  } catch (e) {
    console.warn('markNotificationAsRead error:', e);
  }
}

async function markAllNotificationsAsRead() {
  if (!state.currentUser?.token) return;

  const unreadItems = userNotifications.filter(n => !n.isRead);
  for (const item of unreadItems) {
    await markNotificationAsRead(item.id);
  }

  updateNotificationBadge(0);
  renderNotificationsList();
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

// ─── 8. Deposit Intent & SePay Webhook Simulation ───
let currentDepositIdempotencyKey = 'DEP-' + (typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : ('uuid-' + Math.random().toString(36).substring(2, 12)));
let lastDepositTxCode = '';
let lastDepositAmount = 50000000;

function openDepositModal() {
  document.getElementById('depositModalBackdrop').style.display = 'flex';
  generateNewDepositKey();
  document.getElementById('depositResultBox').style.display = 'none';
}

function closeDepositModal() {
  document.getElementById('depositModalBackdrop').style.display = 'none';
}

function handleBackdropClick(event, backdropId) {
  if (event.target && event.target.id === backdropId) {
    document.getElementById(backdropId).style.display = 'none';
  }
}

function setDepositAmount(val) {
  document.getElementById('depositAmountInput').value = val;
}

function generateNewDepositKey() {
  currentDepositIdempotencyKey = 'DEP-' + (typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : ('uuid-' + Math.random().toString(36).substring(2, 12)));
  const input = document.getElementById('depositIdempotencyInput');
  if (input) input.value = currentDepositIdempotencyKey;
}

async function createDepositIntent() {
  if (!state.currentUser?.token) {
    alert('Vui lòng đăng nhập để nạp tiền.');
    return;
  }

  const amount = parseFloat(document.getElementById('depositAmountInput').value);
  if (!amount || amount <= 0) {
    alert('Vui lòng nhập số tiền nạp hợp lệ.');
    return;
  }
  lastDepositAmount = amount;

  let idempotencyKey = (document.getElementById('depositIdempotencyInput').value || '').trim();
  if (!idempotencyKey) {
    idempotencyKey = currentDepositIdempotencyKey;
    document.getElementById('depositIdempotencyInput').value = idempotencyKey;
  }

  try {
    addLog('SYSTEM', `💳 Đang khởi tạo Deposit Intent: ${formatVND(amount)}...`);
    const res = await fetch(`${API_BASE_URL}/api/wallets/deposit`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${state.currentUser.token}`
      },
      body: JSON.stringify({ amount, idempotencyKey })
    });

    const data = await res.json();
    if (!res.ok || !data.success) {
      throw new Error(data.message || 'Tạo yêu cầu nạp tiền thất bại.');
    }

    const intent = data.data;
    const txCode = intent.transactionCode 
      || intent.transferContent 
      || (intent.intentCode ? intent.intentCode.replace('PI-', '') : '')
      || ('TS' + Date.now());
    lastDepositTxCode = txCode;

    // Render result box
    document.getElementById('depositQrImage').src = intent.qrCodeUrl || 'https://qr.sepay.vn/assets/img/mock-qr.png';
    document.getElementById('depositDisplayAmount').textContent = formatVND(intent.amount || amount);
    document.getElementById('depositDisplaySyntax').textContent = txCode;
    document.getElementById('depositResultBox').style.display = 'block';

    addLog('SYSTEM', `✅ Đã sinh mã VietQR SePay cho mã giao dịch: ${txCode}`);
  } catch (err) {
    addLog('SYSTEM', `❌ Lỗi tạo Deposit Intent: ${err.message}`);
    alert(`Lỗi nạp tiền: ${err.message}`);
  }
}

async function simulateSepayWebhook() {
  // Nếu chưa khởi tạo Deposit Intent, tự động gọi tạo trước để sinh WalletTransaction (PENDING) trong DB
  if (!lastDepositTxCode) {
    addLog('SYSTEM', `⚡ Tự động tạo Intent nạp tiền trước khi gọi Webhook...`);
    await createDepositIntent();
    if (!lastDepositTxCode) {
      alert('Không thể tạo mã giao dịch nạp tiền. Vui lòng thử lại.');
      return;
    }
  }

  const txCode = lastDepositTxCode;
  const amount = lastDepositAmount || 50000000;

  try {
    addLog('SYSTEM', `⚡ Đang gửi SePay Webhook giả lập cho mã: ${txCode} (${formatVND(amount)})...`);
    const payload = {
      id: Math.floor(Math.random() * 900000) + 100000,
      gateway: "Sacombank",
      transactionDate: new Date().toISOString(),
      accountNumber: "070131881841",
      code: txCode,
      content: `${txCode} nap tien vao vi TrustSim`,
      transferType: "in",
      transferAmount: amount,
      accumulated: 1050000000,
      subAccount: null,
      referenceCode: "REF-" + Math.floor(Math.random() * 900000)
    };

    const res = await fetch(`${API_BASE_URL}/api/wallets/sepay-webhook`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Apikey AQGX357TXGZJGR59L3R0A6CNFUKILN4DGCYV2FBHBOXQPVQQEDWTYXMHTK1T4DJC'
      },
      body: JSON.stringify(payload)
    });

    const data = await res.json();
    if (res.ok && (data.success || data.isSuccess)) {
      addLog('SYSTEM', `🎉 Nạp tiền thành công qua SePay Webhook! Đã ghi nhận Sổ cái Kép Nợ 1010 / Có 2010 (${formatVND(amount)})`);
      alert(`🎉 NẠP TIỀN THÀNH CÔNG!\nSố tiền: ${formatVND(amount)}\nMã giao dịch: ${txCode}\nĐã ghi sổ cái kép và cộng tiền vào ví.`);
      generateNewDepositKey();
      lastDepositTxCode = '';
      await fetchUserWallet();
      closeDepositModal();
    } else {
      throw new Error(data.message || 'Lỗi xử lý SePay Webhook.');
    }
  } catch (err) {
    addLog('SYSTEM', `❌ Lỗi gọi SePay Webhook: ${err.message}`);
    alert(`Lỗi Webhook: ${err.message}`);
  }
}

async function pingServer() {
  const isSignalRConnected = state.hubConnection && state.hubConnection.state === signalR.HubConnectionState.Connected;
  const statusStr = isSignalRConnected ? '🟢 CONNECTED' : '🔴 DISCONNECTED';
  addLog('SYSTEM', `📡 [PING] Trạng thái SignalR: ${statusStr} (API: ${API_BASE_URL}) | Active Room: ${state.activeRoomId || 'None'}`);

  try {
    const start = performance.now();
    const res = await fetch(`${API_BASE_URL}/api/auctions?pageSize=1`);
    const latency = Math.round(performance.now() - start);
    if (res.ok) {
      addLog('SYSTEM', `⚡ [PING API]: Phản hồi HTTP 200 OK (${latency}ms)`);
      alert(`📡 KẾT NỐI HỆ THỐNG:\n\n- SignalR WebSocket: ${statusStr}\n- REST API Endpoint: 🟢 HTTP 200 OK (${latency}ms)\n- URL Backend: ${API_BASE_URL}\n- Đang theo dõi Room: ${state.activeRoomId ? 'auction_' + state.activeRoomId.substring(0, 8) : 'Listing Feed'}`);
    } else {
      throw new Error(`HTTP ${res.status}`);
    }
  } catch (e) {
    addLog('SYSTEM', `❌ [PING API] Lỗi kết nối HTTP: ${e.message}`);
    alert(`⚠️ LỖI KẾT NỐI BACKEND:\nKhông thể kết nối API tại ${API_BASE_URL} (${e.message}). Vui lòng kiểm tra backend đã chạy chưa.`);
  }
}


