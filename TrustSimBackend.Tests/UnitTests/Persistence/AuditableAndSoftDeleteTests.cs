using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;
using TrustSimBackend.Infrastructure.Persistence.Interceptors;
using TrustSimBackend.Tests.Common;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Persistence;

public class AuditableAndSoftDeleteTests
{
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Guid _testUserId = Guid.NewGuid();
    private const string TestUserName = "audit_tester";

    public AuditableAndSoftDeleteTests()
    {
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCurrentUserService.Setup(s => s.UserId).Returns(_testUserId);
        _mockCurrentUserService.Setup(s => s.UserName).Returns(TestUserName);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockHttpContextAccessor.Setup(h => h.HttpContext).Returns(httpContext);
    }

    private TrustSimDbContext CreateDbContextWithInterceptor()
    {
        var interceptor = new AuditableEntityInterceptor(
            _mockCurrentUserService.Object,
            _mockHttpContextAccessor.Object);

        var options = new DbContextOptionsBuilder<TrustSimDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(interceptor)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestTrustSimDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task AddEntity_AutomaticallySetsAuditFields_AndGeneratesAuditLog()
    {
        // Arrange
        using var context = CreateDbContextWithInterceptor();
        var sim = new SimNumber
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0988111222",
            Price = 50_000_000m,
            SimStatus = SimStatus.AVAILABLE
        };

        // Act
        await context.SimNumbers.AddAsync(sim);
        await context.SaveChangesAsync();

        // Assert 1: Audit Fields
        sim.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        sim.CreatedBy.Should().Be(TestUserName);
        sim.UpdatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        sim.UpdatedBy.Should().Be(TestUserName);

        // Assert 2: AuditLog record
        var auditLogs = await context.AuditLogs.Where(l => l.EntityId == sim.Id).ToListAsync();
        auditLogs.Should().HaveCount(1);
        var createLog = auditLogs.First();
        createLog.ActionType.Should().Be(ActionType.CREATE);
        createLog.EntityName.Should().Be(nameof(SimNumber));
        createLog.ActorId.Should().Be(_testUserId);
        createLog.IPAddress.Should().Be("192.168.1.100");
        createLog.NewData.Should().Contain("0988111222");
    }

    [Fact]
    public async Task ModifyEntity_UpdatesUpdatedDate_AndGeneratesUpdateAuditLog()
    {
        // Arrange
        using var context = CreateDbContextWithInterceptor();
        var sim = new SimNumber
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0988333444",
            Price = 10_000_000m,
            SimStatus = SimStatus.AVAILABLE
        };
        await context.SimNumbers.AddAsync(sim);
        await context.SaveChangesAsync();

        // Act
        sim.Price = 15_000_000m;
        await context.SaveChangesAsync();

        // Assert: AuditLog has UPDATE
        var updateLogs = await context.AuditLogs
            .Where(l => l.EntityId == sim.Id && l.ActionType == ActionType.UPDATE)
            .ToListAsync();

        updateLogs.Should().HaveCount(1);
        var log = updateLogs.First();
        log.OldData.Should().Contain("10000000");
        log.NewData.Should().Contain("15000000");
        sim.UpdatedBy.Should().Be(TestUserName);
    }

    [Fact]
    public async Task DeleteEntity_AutomaticallySoftDeletes_AndFiltersOutFromNormalQueries()
    {
        // Arrange
        using var context = CreateDbContextWithInterceptor();
        var simId = Guid.NewGuid();
        var sim = new SimNumber
        {
            Id = simId,
            PhoneNumber = "0988555666",
            Price = 20_000_000m,
            SimStatus = SimStatus.AVAILABLE
        };
        await context.SimNumbers.AddAsync(sim);
        await context.SaveChangesAsync();

        // Act: Gọi Remove() thông thường
        context.SimNumbers.Remove(sim);
        await context.SaveChangesAsync();

        // Assert 1: Thực thể không bị xóa vật lý mà được đánh dấu IsDeleted = true
        sim.IsDeleted.Should().BeTrue();
        sim.DeletedDate.Should().NotBeNull();
        sim.DeletedBy.Should().Be(TestUserName);

        // Assert 2: Truy vấn thông thường KHÔNG thấy bản ghi (Global Query Filter hoạt động)
        var queriedSim = await context.SimNumbers.FirstOrDefaultAsync(s => s.Id == simId);
        queriedSim.Should().BeNull();

        // Assert 3: Truy vấn với IgnoreQueryFilters() vẫn thấy bản ghi đã xóa mềm
        var ignoredSim = await context.SimNumbers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == simId);
        ignoredSim.Should().NotBeNull();
        ignoredSim!.Id.Should().Be(simId);
        ignoredSim.IsDeleted.Should().BeTrue();
    }
}

