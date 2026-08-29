using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository quản lý dữ liệu danh mục nền (Master Data) của SIM:
/// Nhà mạng (Carrier), Đầu số (PhonePrefix), Danh mục số đẹp (Category), Nhịp định dạng (FormatRhythm).
/// </summary>
public interface ISimMasterDataRepository
{
    // --- Nhà mạng (SimCarrier) ---
    Task<List<SimCarrier>> GetCarriersAsync(CancellationToken cancellationToken = default);
    Task<SimCarrier?> GetCarrierByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimCarrier?> GetCarrierByCodeAsync(string carrierCode, CancellationToken cancellationToken = default);
    Task AddCarrierAsync(SimCarrier carrier, CancellationToken cancellationToken = default);
    Task<bool> ExistsCarrierCodeAsync(string carrierCode, CancellationToken cancellationToken = default);

    // --- Đầu số (PhonePrefix) ---
    Task<List<PhonePrefix>> GetPhonePrefixesAsync(Guid? carrierId = null, CancellationToken cancellationToken = default);
    Task<PhonePrefix?> GetPhonePrefixByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PhonePrefix?> GetPhonePrefixByValueAsync(string prefixValue, CancellationToken cancellationToken = default);
    Task AddPhonePrefixAsync(PhonePrefix prefix, CancellationToken cancellationToken = default);

    // --- Danh mục SIM (SimCategory) ---
    Task<List<SimCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<SimCategory?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimCategory?> GetCategoryByCodeAsync(string categoryCode, CancellationToken cancellationToken = default);
    Task AddCategoryAsync(SimCategory category, CancellationToken cancellationToken = default);
    Task<bool> ExistsCategoryCodeAsync(string categoryCode, CancellationToken cancellationToken = default);

    // --- Nhịp định dạng (FormatRhythm) ---
    Task<List<FormatRhythm>> GetFormatRhythmsAsync(CancellationToken cancellationToken = default);
    Task<FormatRhythm?> GetFormatRhythmByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddFormatRhythmAsync(FormatRhythm formatRhythm, CancellationToken cancellationToken = default);

    // --- Seeding & Checks ---
    Task<bool> HasAnyCarrierAsync(CancellationToken cancellationToken = default);
}
