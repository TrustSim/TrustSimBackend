namespace TrustSimBackend.Domain.Interfaces;

public interface IHashGenerator
{
    // Tầng Infrastructure sẽ dùng thư viện SHA256 hoặc BCrypt để viết code thực tế cho hàm này
    string GenerateDocumentHash(string documentContent);
}