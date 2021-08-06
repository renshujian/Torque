using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Torque
{
    class MesDbContext : DbContext
    {
        public DbSet<Tool> Tools { get; set; } = default!;
        public DbSet<Test> Tests { get; set; } = default!;

        public MesDbContext(DbContextOptions<MesDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var tool = modelBuilder.Entity<Tool>().ToView("SCREWDRIVER_CMK");
            tool.Property(t => t.Id).HasColumnName("SCREWDRIVER");
            tool.Property(t => t.SetTorque).HasColumnName("XYNJ").HasConversion<string>();
            tool.HasData(
                new Tool { Id = "50mppmu1N0vovmnmmmmmqnnpmtmnmj1E0toml1E0gmmm", SetTorque = 50},
                new Tool { Id = "90ehhem1G0nemefeeeeffmfhelefebxgmnYeee", SetTorque = 9},
                new Tool { Id = "03308K9290100000411307010-B720/B*000", SetTorque = 720.8 },
                new Tool { Id = "03308L9080100001181307010-C289*000", SetTorque = 289 },
                new Tool { Id = "720289", SetTorque = 0.13});

            var test = modelBuilder.Entity<Test>().ToTable("REAL_TORQUE_OF_SCREWDRIVER");
            test.Property(t => t.ToolId).HasColumnName("SCREWDRIVER");
            test.Property(t => t.SetTorque).HasColumnName("SET_TORQUE").HasConversion<string>();
            test.Property(t => t.RealTorque).HasColumnName("REAL_TORQUE").HasConversion<string>();
            test.Property(t => t.Diviation).HasColumnName("DIVIATION").HasConversion<string>();
            test.Property(t => t.TestTime).HasColumnName("TEST_TIME");
            test.HasKey(t => t.TestTime);
        }
    }

    class MesDbContextFactory : IDesignTimeDbContextFactory<MesDbContext>
    {
        public MesDbContext CreateDbContext(string[] args)
        {
            var o = new DbContextOptionsBuilder<MesDbContext>().UseOracle("Password=mes_public;Persist Security Info=True;User ID=mes_public;Data Source=(DESCRIPTION =(ADDRESS = (PROTOCOL = TCP)(HOST = localhost)(PORT = 1521))(CONNECT_DATA =(SERVER = DEDICATED)(SERVICE_NAME = orcl.mshome.net)))", o => o.UseOracleSQLCompatibility("11"));
            return new(o.Options);
        }
    }
}
