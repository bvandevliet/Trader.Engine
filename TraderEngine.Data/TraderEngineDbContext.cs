using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TraderEngine.Data.Entities;

namespace TraderEngine.Data;

/// <summary>
/// Deliberately does not implement <c>IDataProtectionKeyContext</c> — the Data Protection key
/// ring is persisted to a filesystem volume instead (see <c>Program.cs</c>), kept out of this
/// database so a single database compromise can't also hand over the keys that protect
/// <see cref="ExchangeApiCredential"/> ciphertext.
/// </summary>
public class TraderEngineDbContext(DbContextOptions<TraderEngineDbContext> options)
  : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
  public DbSet<RebalancingConfiguration> RebalancingConfigurations => Set<RebalancingConfiguration>();

  public DbSet<ExchangeApiCredential> ExchangeApiCredentials => Set<ExchangeApiCredential>();

  public DbSet<MarketCapMetric> MarketCapMetrics => Set<MarketCapMetric>();

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    var stringListConverter = new ValueConverter<List<string>, string>(
      list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
      json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new());

    var stringListComparer = new ValueComparer<List<string>>(
      (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
      list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
      list => list.ToList());

    var doubleDictionaryConverter = new ValueConverter<Dictionary<string, double>, string>(
      dict => JsonSerializer.Serialize(dict, (JsonSerializerOptions?)null),
      json => JsonSerializer.Deserialize<Dictionary<string, double>>(json, (JsonSerializerOptions?)null) ?? new());

    var doubleDictionaryComparer = new ValueComparer<Dictionary<string, double>>(
      (a, b) => (a ?? new()).OrderBy(kv => kv.Key).SequenceEqual((b ?? new()).OrderBy(kv => kv.Key)),
      dict => dict.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
      dict => dict.ToDictionary(kv => kv.Key, kv => kv.Value));

    builder.Entity<RebalancingConfiguration>(entity =>
    {
      entity.HasKey(c => c.UserId);

      entity.HasOne(c => c.User)
        .WithOne()
        .HasForeignKey<RebalancingConfiguration>(c => c.UserId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.Property(c => c.AltWeightingFactors)
        .HasConversion(doubleDictionaryConverter, doubleDictionaryComparer)
        .HasColumnType("jsonb");

      entity.Property(c => c.TagsToInclude)
        .HasConversion(stringListConverter, stringListComparer)
        .HasColumnType("jsonb");

      entity.Property(c => c.TagsToIgnore)
        .HasConversion(stringListConverter, stringListComparer)
        .HasColumnType("jsonb");
    });

    builder.Entity<ExchangeApiCredential>(entity =>
    {
      entity.HasOne(c => c.User)
        .WithMany()
        .HasForeignKey(c => c.UserId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.HasIndex(c => new { c.UserId, c.ExchangeName }).IsUnique();
    });

    builder.Entity<MarketCapMetric>(entity =>
    {
      // The time column (Updated) must be part of the key on a TimescaleDB hypertable.
      entity.HasKey(m => new { m.QuoteSymbol, m.BaseSymbol, m.Updated });

      entity.Property(m => m.Tags)
        .HasConversion(stringListConverter, stringListComparer)
        .HasColumnType("jsonb");
    });
  }
}
