using MediatR;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.Queries;

public class CalculateSellerDepositQueryHandler : IRequestHandler<CalculateSellerDepositQuery, CalculateSellerDepositResultDto>
{
    private readonly SellerDepositSettings _settings;

    public CalculateSellerDepositQueryHandler(IOptions<SellerDepositSettings> options)
    {
        _settings = options.Value;
    }

    public Task<CalculateSellerDepositResultDto> Handle(CalculateSellerDepositQuery request, CancellationToken cancellationToken)
    {
        var startingPrice = Math.Max(0m, request.StartingPrice);
        var rate = _settings.DepositRate;
        var rawDeposit = Math.Round(startingPrice * rate);
        var min = _settings.MinDepositAmount;
        var max = _settings.MaxDepositAmount;
        var calculated = Math.Clamp(rawDeposit, min, max);

        var isMinClamped = rawDeposit < min && startingPrice > 0;
        var isMaxClamped = rawDeposit > max;

        var percentText = $"{(rate * 100):0.#}%";
        var formulaText = $"Công thức: Clamp({percentText} × Giá khởi điểm, {min:N0} ₫, {max:N0} ₫)";
        string breakdownText;

        if (startingPrice <= 0)
        {
            breakdownText = "Chưa có giá khởi điểm hợp lệ để tính toán cọc.";
        }
        else if (isMaxClamped)
        {
            breakdownText = $"⚠️ {percentText} gốc là <strong>{rawDeposit:N0} ₫</strong> → <span style=\"color:#0284c7;font-weight:600;\">Chạm mức trần tối đa {max:N0} ₫</span> theo cấu hình Server.";
        }
        else if (isMinClamped)
        {
            breakdownText = $"⚠️ {percentText} gốc là <strong>{rawDeposit:N0} ₫</strong> → <span style=\"color:#0284c7;font-weight:600;\">Chạm mức sàn tối thiểu {min:N0} ₫</span> theo cấu hình Server.";
        }
        else
        {
            breakdownText = $"✅ Bằng đúng <strong>{percentText}</strong> của giá khởi điểm ({calculated:N0} ₫).";
        }

        var result = new CalculateSellerDepositResultDto(
            StartingPrice: startingPrice,
            DepositRate: rate,
            RawDeposit: rawDeposit,
            SellerDepositAmount: calculated,
            MinDepositAmount: min,
            MaxDepositAmount: max,
            IsMinClamped: isMinClamped,
            IsMaxClamped: isMaxClamped,
            FormulaText: formulaText,
            BreakdownText: breakdownText
        );

        return Task.FromResult(result);
    }
}
