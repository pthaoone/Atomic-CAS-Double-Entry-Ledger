using FinTechEngine.Api.Domain.Entities;
using FinTechEngine.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinTechEngine.Api.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public const string SystemVaultAccountId = "SYSTEM_CASH_VAULT";

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<TransactionRecord> Transactions => Set<TransactionRecord>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId);
            entity.Property(e => e.AccountId).HasMaxLength(64);
            entity.Property(e => e.AccountName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.CurrentBalance).HasPrecision(18, 4);
            entity.Property(e => e.Version).IsConcurrencyToken();

            entity.HasIndex(e => e.Type);
        });

        // TransactionRecord configuration
        modelBuilder.Entity<TransactionRecord>(entity =>
        {
            entity.HasKey(e => e.TxId);
            entity.Property(e => e.TxId).HasMaxLength(64);
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.SourceAccountId).HasMaxLength(64);
            entity.Property(e => e.DestinationAccountId).HasMaxLength(64);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Status);
        });

        // LedgerEntry configuration
        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.HasKey(e => e.EntryId);
            entity.Property(e => e.EntryId).HasMaxLength(64);
            entity.Property(e => e.TxId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.AccountId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Direction).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.BalanceAfter).HasPrecision(18, 4);

            entity.HasIndex(e => e.TxId);
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Transaction)
                  .WithMany(t => t.LedgerEntries)
                  .HasForeignKey(e => e.TxId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Account)
                  .WithMany()
                  .HasForeignKey(e => e.AccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public static async Task SeedInitialDataAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (!await context.Accounts.AnyAsync(a => a.AccountId == SystemVaultAccountId))
        {
            context.Accounts.Add(new Account
            {
                AccountId = SystemVaultAccountId,
                AccountName = "System Central Cash Vault / Clearing",
                Type = AccountType.SystemVault,
                CurrentBalance = 10_000_000_000m,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (!await context.Accounts.AnyAsync(a => a.AccountId == "ACC_A"))
        {
            context.Accounts.Add(new Account
            {
                AccountId = "ACC_A",
                AccountName = "Nguyen Van A (Primary Demo)",
                Type = AccountType.Customer,
                CurrentBalance = 1000m, // Exactly 1000 VND for test scenario
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (!await context.Accounts.AnyAsync(a => a.AccountId == "ACC_B"))
        {
            context.Accounts.Add(new Account
            {
                AccountId = "ACC_B",
                AccountName = "Tran Thi B (Transfer Target)",
                Type = AccountType.Customer,
                CurrentBalance = 500m,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }
}
