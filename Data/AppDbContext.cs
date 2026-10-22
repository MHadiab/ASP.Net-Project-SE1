using Microsoft.EntityFrameworkCore;

namespace Ledger_and_Inventory_Service.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options):DbContext(options)
{
    
}