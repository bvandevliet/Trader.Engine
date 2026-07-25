using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data.Entities;

namespace TraderEngine.Data.Repositories;

public class EfApiCredentialsRepository : IApiCredentialsRepository
{
  private readonly TraderEngineDbContext _db;
  private readonly IDataProtector _protector;

  public EfApiCredentialsRepository(TraderEngineDbContext db, IDataProtectionProvider dataProtectionProvider)
  {
    _db = db;
    _protector = dataProtectionProvider.CreateProtector("ExchangeApiCredential.v1");
  }

  public async Task<ApiCredReqDto> GetApiCred(Guid userId, string exchangeName)
  {
    var entity = await _db.ExchangeApiCredentials
      .AsNoTracking()
      .FirstOrDefaultAsync(c => c.UserId == userId && c.ExchangeName == exchangeName);

    if (entity == null)
    {
      return new ApiCredReqDto
      {
        ApiKey = string.Empty,
        ApiSecret = string.Empty,
      };
    }

    return new ApiCredReqDto
    {
      ApiKey = _protector.Unprotect(entity.ProtectedApiKey),
      ApiSecret = _protector.Unprotect(entity.ProtectedApiSecret),
    };
  }

  public async Task<ApiCredentialStatus?> GetApiCredStatus(Guid userId, string exchangeName)
  {
    var updatedAt = await _db.ExchangeApiCredentials
      .AsNoTracking()
      .Where(c => c.UserId == userId && c.ExchangeName == exchangeName)
      .Select(c => (DateTimeOffset?)c.UpdatedAt)
      .FirstOrDefaultAsync();

    return updatedAt == null ? null : new ApiCredentialStatus(updatedAt.Value);
  }

  public async Task SaveApiCred(Guid userId, string exchangeName, ApiCredReqDto apiCred)
  {
    var entity = await _db.ExchangeApiCredentials
      .FirstOrDefaultAsync(c => c.UserId == userId && c.ExchangeName == exchangeName);

    if (entity == null)
    {
      entity = new ExchangeApiCredential
      {
        Id = Guid.NewGuid(),
        UserId = userId,
        ExchangeName = exchangeName,
      };
      _db.ExchangeApiCredentials.Add(entity);
    }

    entity.ProtectedApiKey = _protector.Protect(apiCred.ApiKey);
    entity.ProtectedApiSecret = _protector.Protect(apiCred.ApiSecret);
    entity.UpdatedAt = DateTimeOffset.UtcNow;

    _ = await _db.SaveChangesAsync();
  }
}
