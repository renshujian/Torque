using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Torque
{
    public class AppDbContext : IdentityDbContext<User>
    {
        public DbSet<Test> Tests { get; set; } = default!;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Test>().HasKey(t => t.TestTime);
            builder.Entity<User>().HasData(
                new User("查看") { NormalizedUserName = "查看", Id = "3f2b0240-bafb-4788-bc3e-e913b50b6564", PasswordHash = "AQAAAAEAACcQAAAAEESTP1NCOm16LcUTzHkXrLVmnYUoH6Pu/bGgjLZTfxrGXCsJ4e6HLU7h/CILPlmEeg==" },
                new User("操作员") { NormalizedUserName = "操作员", Id = "d2b7766c-c245-4ebd-bde3-78b8a6dd134d", PasswordHash = "AQAAAAEAACcQAAAAECPdh958USpp1F04Gqz4SV0YQlpCuvAYRtBvWZVj2FuI1En/bUYZYy7aOT1EwrZtRQ==" },
                new User("管理员") { NormalizedUserName = "管理员", Id = "8d5c1911-a3e9-4304-8893-5dae34c01121", PasswordHash = "AQAAAAEAACcQAAAAEAeg9cpDyvRF2kpgCA/9RjfM06dvdrPRnzu7fywjqlKmMFrofN+G2b/yLgIsLreesQ==" }
                );
        }
    }

    class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var o = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("DataSource=app.db;Cache=Shared");
            return new(o.Options);
        }
    }
}
