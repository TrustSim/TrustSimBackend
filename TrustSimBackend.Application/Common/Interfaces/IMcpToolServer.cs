using System.Text.Json;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Internal Model Context Protocol (MCP) tool server providing schema discovery, authorization gates, and typed execution.
/// </summary>
public interface IMcpToolServer
{
    IReadOnlyList<McpToolDescriptor> GetAllToolDescriptors();
    IReadOnlyList<McpToolDescriptor> ListAuthorizedTools(AiUserContext userContext, IReadOnlyCollection<string>? nameFilter = null);
    Task<McpToolExecutionResult> CallToolAsync(string toolName, JsonElement arguments, AiUserContext userContext, CancellationToken cancellationToken = default);
    Task<McpJsonRpcResponse> HandleJsonRpcAsync(McpJsonRpcRequest request, AiUserContext userContext, CancellationToken cancellationToken = default);
}
