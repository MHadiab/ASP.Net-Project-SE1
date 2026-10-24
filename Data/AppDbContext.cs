using System.Reflection;
using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ledger_and_Inventory_Service.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options):DbContext(options)
{
    public DbSet<Hold> Holds => Set<Hold>();




    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

}