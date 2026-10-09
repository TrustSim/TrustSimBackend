-- ============================================================================
-- ATOMIC BIDDING ENGINE - TRUSTSIM HIGH-PERFORMANCE REDIS LUA SCRIPT
-- ============================================================================
-- KEYS[1]: auction:{auctionId}:state
-- KEYS[2]: auction:{auctionId}:leaderboard
-- KEYS[3]: auction:{auctionId}:participant:{participantId}
-- KEYS[4]: wallet:{userId}:balance
-- ARGV[1]: participantId
-- ARGV[2]: userId
-- ARGV[3]: bidAmount
-- ARGV[4]: currentTimeMs
-- ARGV[5]: bidId
-- ARGV[6]: auctionId

-- 1. KIỂM TRA TRẠNG THÁI PHIÊN
local status = redis.call('HGET', KEYS[1], 'status')
if not status or status ~= 'ACTIVE' then
    return cjson.encode({ success = false, errorCode = 'ERR_AUCTION_NOT_ACTIVE', message = 'Phiên đấu giá chưa mở hoặc đã kết thúc.' })
end

local endAtMs = tonumber(redis.call('HGET', KEYS[1], 'endAtMs')) or 0
local nowMs = tonumber(ARGV[4])
if nowMs >= endAtMs then
    return cjson.encode({ success = false, errorCode = 'ERR_AUCTION_ENDED', message = 'Đã hết thời gian đặt giá của phiên.' })
end

-- 2. KIỂM TRA QUYỀN HẠN NGƯỜI THAM GIA
local pStatus = redis.call('HGET', KEYS[3], 'status')
if not pStatus or pStatus == 'BANNED' then
    return cjson.encode({ success = false, errorCode = 'ERR_PARTICIPANT_BANNED', message = 'Bạn không có quyền tham gia trả giá trong phiên này.' })
end

-- 3. QUY TẮC NO SELF-OUTBID (Không được tự vượt giá chính mình)
local topBidderParticipantId = redis.call('HGET', KEYS[1], 'topBidderParticipantId')
if topBidderParticipantId == ARGV[1] then
    return cjson.encode({ success = false, errorCode = 'ERR_SELF_OUTBID', message = 'Bạn đang là người giữ mức giá cao nhất hiện tại.' })
end

-- 4. KIỂM TRA BƯỚC GIÁ & TRẦN BƯỚC GIÁ (MAX STEP MULTIPLIER)
local currentPrice = tonumber(redis.call('HGET', KEYS[1], 'currentPrice')) or 0
local minIncrement = tonumber(redis.call('HGET', KEYS[1], 'minIncrement')) or 100000
local maxMultiplier = tonumber(redis.call('HGET', KEYS[1], 'maxMultiplier')) or 10
local bidAmount = tonumber(ARGV[3])

local minRequired = currentPrice + minIncrement
local maxAllowed = currentPrice + (maxMultiplier * minIncrement)

if bidAmount < minRequired then
    return cjson.encode({ success = false, errorCode = 'ERR_BID_TOO_LOW', message = 'Giá đặt phải lớn hơn hoặc bằng ' .. tostring(minRequired) .. ' VNĐ.' })
end

if bidAmount > maxAllowed then
    return cjson.encode({ success = false, errorCode = 'ERR_BID_EXCEEDS_MAX_STEP', message = 'Giá đặt vượt quá mức tăng tối đa cho phép (' .. tostring(maxAllowed) .. ' VNĐ).' })
end

-- 5. KIỂM TRA KÝ QUỸ LŨY TIẾN: A = X * n%, Delta = A - InitialDeposit <= AvailableBalance
local marginRate = tonumber(redis.call('HGET', KEYS[1], 'marginRate')) or 0.10
local initialDeposit = tonumber(redis.call('HGET', KEYS[3], 'initialDeposit')) or 0
local requiredMarginA = bidAmount * marginRate
local deltaMargin = requiredMarginA - initialDeposit

local walletBalance = tonumber(redis.call('HGET', KEYS[4], 'availableBalance')) or 0
if deltaMargin > 0 and walletBalance < deltaMargin then
    return cjson.encode({ 
        success = false, 
        errorCode = 'ERR_INSUFFICIENT_MARGIN', 
        message = 'Số dư ví không đủ đáp ứng ký quỹ ' .. tostring(requiredMarginA) .. ' VNĐ. Cần bổ sung thêm ' .. tostring(deltaMargin) .. ' VNĐ.' 
    })
end

-- 6. KIỂM TRA CƠ CHẾ BÙ GIỜ ANTI-SNIPING
local wasExtended = 0
local extensionThresholdMs = tonumber(redis.call('HGET', KEYS[1], 'extensionThresholdMs')) or 30000
local extensionDurationMs = tonumber(redis.call('HGET', KEYS[1], 'extensionDurationMs')) or 30000
local maxSnipeExtensions = tonumber(redis.call('HGET', KEYS[1], 'maxSnipeExtensions')) or 3
local currentSnipeCount = tonumber(redis.call('HGET', KEYS[3], 'snipeExtensionCount')) or 0

local remainingMs = endAtMs - nowMs
if remainingMs > 0 and remainingMs <= extensionThresholdMs and currentSnipeCount < maxSnipeExtensions then
    endAtMs = endAtMs + extensionDurationMs
    redis.call('HSET', KEYS[1], 'endAtMs', endAtMs)
    redis.call('HINCRBY', KEYS[3], 'snipeExtensionCount', 1)
    wasExtended = 1
end

-- 7. CẬP NHẬT STATE & LEADERBOARD TRÊN RAM
redis.call('HSET', KEYS[1], 'currentPrice', bidAmount)
redis.call('HSET', KEYS[1], 'topBidderParticipantId', ARGV[1])
redis.call('HSET', KEYS[1], 'topBidderUserId', ARGV[2])
local newLockVersion = redis.call('HINCRBY', KEYS[1], 'lockVersion', 1)

local zsetMember = ARGV[1] .. ':' .. ARGV[5] .. ':' .. ARGV[4]
redis.call('ZADD', KEYS[2], bidAmount, zsetMember)
redis.call('HINCRBY', KEYS[3], 'acceptedBidCount', 1)
redis.call('HSET', KEYS[3], 'lastBidTimeMs', ARGV[4])

-- Đẩy bản ghi vào Redis Stream phục vụ Persistence Worker
local streamPayload = cjson.encode({
    bidId = ARGV[5],
    auctionId = ARGV[6],
    participantId = ARGV[1],
    userId = ARGV[2],
    bidAmount = bidAmount,
    placedAtMs = nowMs,
    isSnipeExtended = (wasExtended == 1),
    newEndAtMs = endAtMs,
    lockVersion = newLockVersion
})
redis.call('XADD', 'stream:auction:bids', '*', 'payload', streamPayload)

-- 8. TRẢ VỀ KẾT QUẢ THÀNH CÔNG (<1ms)
return cjson.encode({
    success = true,
    bidId = ARGV[5],
    auctionId = ARGV[6],
    participantId = ARGV[1],
    userId = ARGV[2],
    bidAmount = bidAmount,
    newCurrentPrice = bidAmount,
    newEndAtMs = endAtMs,
    wasExtended = (wasExtended == 1),
    lockVersion = newLockVersion
})
