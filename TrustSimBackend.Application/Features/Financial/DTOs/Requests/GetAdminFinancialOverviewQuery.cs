using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Query MediatR lấy báo cáo tổng quan tài chính sàn, nợ khách hàng và lợi nhuận khả dụng dành cho Admin.
/// </summary>
public record GetAdminFinancialOverviewQuery() : IRequest<AdminFinancialOverviewDto>;
