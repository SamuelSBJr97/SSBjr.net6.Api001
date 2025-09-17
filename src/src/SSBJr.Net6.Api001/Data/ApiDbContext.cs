using Microsoft.EntityFrameworkCore;
using SSBJr.Net6.Api001.Models;

namespace SSBJr.Net6.Api001.Data
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
    }
}
