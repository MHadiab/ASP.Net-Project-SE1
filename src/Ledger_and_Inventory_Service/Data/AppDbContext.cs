using System.Reflection;
using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ledger_and_Inventory_Service.Data;
public class AppDbContext(DbContextOptions<AppDbContext> options) :DbContext(options)
{
    public DbSet<Hold> Holds => Set<Hold>();



    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<AccountBalance> AccountBalances => Set<AccountBalance>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

}