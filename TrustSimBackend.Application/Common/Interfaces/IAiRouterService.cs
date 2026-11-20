using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Classifies user intent, selects the optimal LLM tier, narrows MCP tools, and determines cache eligibility.
/// </summary>
public interface IAiRouterService
{
    AiRoutingDecision Route(
        string userMessage,
        string conversationHistory,
        int? userBirthYear,
        decimal? budgetMin,
        decimal? budgetMax,
        IReadOnlyList<McpToolDescriptor> availableTools);
}
