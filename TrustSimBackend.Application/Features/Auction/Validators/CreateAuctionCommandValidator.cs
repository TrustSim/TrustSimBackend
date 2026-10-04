using FluentValidation;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;

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
            .GreaterThan(0).WithMessage("Giá bảo lưu phải lớn hơn 0.")
            .LessThanOrEqualTo(v => v.StartingPrice)
            .When(v => v.AuctionType == Domain.Enums.AuctionType.DUTCH)
            .WithMessage("Trong đấu giá kiểu Hà Lan, giá sàn (ReservePrice) phải nhỏ hơn hoặc bằng giá khởi điểm.");

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
            .When(v => v.AuctionType == Domain.Enums.AuctionType.ENGLISH)
            .WithMessage("Thời gian kết thúc đấu giá phải sau thời gian bắt đầu.");
    }
}
