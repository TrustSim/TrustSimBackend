using FluentValidation;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.Validators;

public class CreateAuctionCommandValidator : AbstractValidator<CreateAuctionCommand>
{
    public CreateAuctionCommandValidator()
    {
        RuleFor(v => v.SimNumberId)
            .NotEmpty().WithMessage("SimNumberId không được để trống.");

        RuleFor(v => v.StartingPrice)
            .GreaterThan(0).WithMessage("Giá khởi điểm phải lớn hơn 0.");

        RuleFor(v => v.ReservePrice)
            .GreaterThan(0).WithMessage("Trong đấu giá kiểu Hà Lan, giá sàn (ReservePrice) phải lớn hơn 0.")
            .LessThanOrEqualTo(v => v.StartingPrice).WithMessage("Trong đấu giá kiểu Hà Lan, giá sàn (ReservePrice) phải nhỏ hơn hoặc bằng giá khởi điểm.")
            .When(v => v.AuctionType == AuctionType.DUTCH);

        RuleFor(v => v.RegistrationStartAt)
            .GreaterThanOrEqualTo(DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Thời gian bắt đầu đăng ký không được ở quá khứ.");

        RuleFor(v => v.RegistrationEndAt)
            .GreaterThan(v => v.RegistrationStartAt)
            .WithMessage("Thời gian kết thúc đăng ký phải sau thời gian bắt đầu đăng ký.");

        RuleFor(v => v.StartAt)
            .GreaterThanOrEqualTo(v => v.RegistrationEndAt)
            .WithMessage("Thời gian bắt đầu đấu giá phải sau hoặc bằng thời gian kết thúc đăng ký.");

        RuleFor(v => v.EndAt)
            .GreaterThan(v => v.StartAt)
            .When(v => v.AuctionType == AuctionType.ENGLISH)
            .WithMessage("Thời gian kết thúc đấu giá phải sau thời gian bắt đầu.");

        // --- Cấu hình Auction Rules chung ---
        RuleFor(v => v.DepositAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền đặt cọc không được là số âm.");

        RuleFor(v => v.PaymentGracePeriodHours)
            .GreaterThanOrEqualTo(1).WithMessage("Thời hạn ân hạn thanh toán tối thiểu là 1 giờ.");

        RuleFor(v => v.MaximumBidCountPerParticipant)
            .GreaterThan(0).WithMessage("Số lượt trả giá tối đa của mỗi người tham gia phải lớn hơn 0.");

        RuleFor(v => v.MaximumBidCountPerMinute)
            .GreaterThan(0).WithMessage("Số lượt trả giá tối đa mỗi phút phải lớn hơn 0.");

        RuleFor(v => v.MinimumBidIntervalSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Khoảng thời gian tối thiểu giữa 2 lần đặt giá không được âm.");

        // --- Cấu hình riêng cho Đấu giá kiểu Anh (English Auction) ---
        RuleFor(v => v.MinimumBidIncrement)
            .GreaterThan(0)
            .When(v => v.AuctionType == AuctionType.ENGLISH)
            .WithMessage("Đấu giá kiểu English yêu cầu bước giá tối thiểu (MinimumBidIncrement) phải lớn hơn 0.");

        RuleFor(v => v.MaxStepMultiplier)
            .GreaterThanOrEqualTo(1)
            .When(v => v.AuctionType == AuctionType.ENGLISH)
            .WithMessage("Hệ số bước giá tối đa (MaxStepMultiplier) phải lớn hơn hoặc bằng 1.");

        RuleFor(v => v.ExtensionThresholdSeconds)
            .GreaterThanOrEqualTo(0)
            .When(v => v.AuctionType == AuctionType.ENGLISH)
            .WithMessage("Ngưỡng thời gian Anti-Sniping không được là số âm.");

        RuleFor(v => v.ExtensionDurationSeconds)
            .GreaterThanOrEqualTo(0)
            .When(v => v.AuctionType == AuctionType.ENGLISH)
            .WithMessage("Thời gian gia hạn Anti-Sniping không được là số âm.");

        RuleFor(v => v.MaximumExtensionCount)
            .GreaterThanOrEqualTo(0)
            .When(v => v.AuctionType == AuctionType.ENGLISH)
            .WithMessage("Số lần gia hạn tối đa của phiên không được là số âm.");

        RuleFor(v => v.MaxSnipeExtensionsPerParticipant)
            .GreaterThanOrEqualTo(0)
            .When(v => v.AuctionType == AuctionType.ENGLISH)
            .WithMessage("Số lần gia hạn tối đa của mỗi người chơi không được là số âm.");

        // --- Cấu hình riêng cho Đấu giá kiểu Hà Lan (Dutch Auction) ---
        RuleFor(v => v.DutchDecreaseAmount)
            .GreaterThan(0)
            .When(v => v.AuctionType == AuctionType.DUTCH)
            .WithMessage("Đấu giá kiểu Hà Lan yêu cầu mức giảm giá mỗi chu kỳ (DutchDecreaseAmount) phải lớn hơn 0.");

        RuleFor(v => v.DutchDecreaseIntervalSeconds)
            .GreaterThan(0)
            .When(v => v.AuctionType == AuctionType.DUTCH)
            .WithMessage("Đấu giá kiểu Hà Lan yêu cầu chu kỳ giảm giá (DutchDecreaseIntervalSeconds) phải lớn hơn 0 giây.");
    }
}
