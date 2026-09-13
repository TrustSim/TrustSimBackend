namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Loại tài liệu tri thức phục vụ cho RAG và AI
/// </summary>
public enum DocumentType
{
    /// <summary>Báo cáo khảo sát, nghiên cứu thị trường SIM số đẹp</summary>
    MARKET_RESEARCH,

    /// <summary>Tài liệu quy tắc phong thủy, ngũ hành, quẻ dịch</summary>
    FENG_SHUI_RULE,

    /// <summary>Hướng dẫn và chính sách định giá SIM</summary>
    PRICING_GUIDELINE,

    /// <summary>Tài liệu điều khoản, chính sách hoặc văn bản chung</summary>
    GENERAL
}
