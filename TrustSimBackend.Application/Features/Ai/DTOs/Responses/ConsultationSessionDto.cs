using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Responses;

public class ConsultationSessionDto
{
    public Guid Id { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public decimal? TargetBudgetMin { get; set; }
    public decimal? TargetBudgetMax { get; set; }
    public int? UserBirthYear { get; set; }
    public string? UserCareer { get; set; }
    public FengShuiElement? FengShuiElement { get; set; }
    public DateTime CreatedDate { get; set; }
    public int MessageCount { get; set; }
}
