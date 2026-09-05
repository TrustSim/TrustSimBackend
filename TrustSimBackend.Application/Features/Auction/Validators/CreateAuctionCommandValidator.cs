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
            .GreaterThanOrEqualTo(v => v.StartingPrice)
            .WithMessage("Giá bảo lưu phải lớn hơn hoặc bằng giá khởi điểm.");

        RuleFor(v => v.RegistrationStartAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("Thời gian bắt đầu đăng ký phải ở tương lai.");

        RuleFor(v => v.RegistrationEndAt)
            .GreaterThan(v => v.RegistrationStartAt)
            .WithMessage("Thời gian kết thúc đăng ký phải sau thời gian bắt đầu đăng ký.");

        RuleFor(v => v.StartAt)
            .GreaterThanOrEqualTo(v => v.RegistrationEndAt)
            .WithMessage("Thời gian bắt đầu đấu giá phải sau hoặc bằng thời gian kết thúc đăng ký.");

        RuleFor(v => v.EndAt)
            .GreaterThan(v => v.StartAt)
            .WithMessage("Thời gian kết thúc đấu giá phải sau thời gian bắt đầu.");
    }
}
