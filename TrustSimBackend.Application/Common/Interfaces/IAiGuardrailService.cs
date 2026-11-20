using System.Text.Json;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Input, tool-argument, and output safety guardrails (rate limit, prompt injection defense, secret redaction, hallucination check).
/// </summary>
public interface IAiGuardrailService
{
    Task<AiInputGuardrailResult> EvaluateInputAsync(string message, AiUserContext userContext, CancellationToken cancellationToken = default);
    (bool IsValid, string? ErrorMessage) ValidateToolArguments(string toolName, JsonElement arguments);
    AiOutputGuardrailResult EvaluateAndSanitizeOutput(string content, string userMessage, IReadOnlyList<ConsultationSimSuggestion> toolSims);
}
