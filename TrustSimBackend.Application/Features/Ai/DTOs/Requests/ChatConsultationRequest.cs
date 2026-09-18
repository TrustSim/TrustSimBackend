using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public class ChatConsultationRequest
{
    public Guid? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? UserBirthYear { get; set; }
    public string? UserCareer { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public FengShuiElement? FengShuiElement { get; set; }
}
