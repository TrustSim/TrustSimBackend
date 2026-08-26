namespace TrustSimBackend.Domain.Repositories;

public interface IUnitOfWork
{
    // Lưu các thay đổi vào DB và trả về số dòng bị ảnh hưởng
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}