using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TraderEngine.Common.Extensions;
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

  public DbSet<PortfolioManagerGrant> PortfolioManagerGrants => Set<PortfolioManagerGrant>();

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    // Phone number is not used anywhere in this app; drop the inherited IdentityUser columns.
    builder.Entity<AppUser>(entity =>
    {
      entity.Ignore(u => u.PhoneNumber);
      entity.Ignore(u => u.PhoneNumberConfirmed);

      // Enforce email uniqueness at the DB level too, not just via IdentityOptions.User.RequireUniqueEmail
      // (app-level check only) — matches the unique constraint Identity already puts on NormalizedUserName.
      entity.HasIndex(u => u.NormalizedEmail).IsUnique();
    });

    var stringListConverter = new ValueConverter<List<string>, string>(
      list => AppJsonSerializer.Serialize(list),
      json => AppJsonSerializer.Deserialize<List<string>>(json) ?? new());

    var stringListComparer = new ValueComparer<List<string>>(
      (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
      list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
      list => list.ToList());

    var doubleDictionaryConverter = new ValueConverter<Dictionary<string, double>, string>(
      dict => AppJsonSerializer.Serialize(dict),
      json => AppJsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new());

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

      entity.Property(c => c.WeightingOverrides)
        .HasConversion(doubleDictionaryConverter, doubleDictionaryComparer)
        .HasColumnType("jsonb");

      entity.Property(c => c.TagsToInclude)
        .HasConversion(stringListConverter, stringListComparer)
        .HasColumnType("jsonb");

      entity.Property(c => c.TagsToIgnore)
        .HasConversion(stringListConverter, stringListComparer)
        .HasColumnType("jsonb");

      entity.HasIndex(c => c.LastRebalance)
        .IsDescending();
    });

    builder.Entity<ExchangeApiCredential>(entity =>
    {
      entity.HasOne(c => c.User)
        .WithMany()
        .HasForeignKey(c => c.UserId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.HasIndex(c => new
      {
        c.UserId,
        c.ExchangeName
      }).IsUnique();
    });

    builder.Entity<PortfolioManagerGrant>(entity =>
    {
      entity.HasOne(g => g.Manager)
        .WithMany()
        .HasForeignKey(g => g.ManagerId)
        .OnDelete(DeleteBehavior.Cascade);

      entity.HasOne(g => g.Client)
        .WithMany()
        .HasForeignKey(g => g.ClientId)
        .OnDelete(DeleteBehavior.Cascade);

      // One row per (manager, client) pair, ever — re-granting after a revoke reuses this row
      // rather than inserting a new one (see PortfolioManagerGrant's doc comment).
      entity.HasIndex(g => new
      {
        g.ManagerId,
        g.ClientId
      }).IsUnique();

      // The composite index above only serves manager-first lookups efficiently; a client's "who
      // manages me" query needs its own index on the leading ClientId column.
      entity.HasIndex(g => g.ClientId);

      // Belt-and-suspenders alongside the app-level self-grant rejection in the Delegation page —
      // a user granting themselves access would be a meaningless no-op row.
      entity.ToTable(t => t.HasCheckConstraint("ck_portfolio_manager_grant_no_self_grant", "manager_id <> client_id"));
    });

    builder.Entity<MarketCapMetric>(entity =>
    {
      // The time column (Updated) must be part of the key on a TimescaleDB hypertable.
      entity.HasKey(m => new
      {
        m.QuoteSymbol,
        m.BaseSymbol,
        m.Updated
      });

      entity.HasIndex(m => m.Updated)
        .IsDescending();

      entity.Property(m => m.Tags)
        .HasConversion(stringListConverter, stringListComparer)
        .HasColumnType("jsonb");
    });
  }
}