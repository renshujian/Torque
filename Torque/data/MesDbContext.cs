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
            var tool = modelBuilder.Entity<Tool>().ToTable("screwdriver_CMK");
            tool.Property(t => t.Id).HasColumnName("screwdriver");
            tool.Property(t => t.SetTorque).HasColumnName("XYNJ").HasConversion<string>();
            tool.HasData(
                new Tool { Id = "03308K9290100000411307010-B720/B*000", SetTorque = 720.8 },
                new Tool { Id = "03308L9080100001181307010-C289*000", SetTorque = 289 },
                new Tool { Id = "720289", SetTorque = 0.13});

            var test = modelBuilder.Entity<Test>().ToTable("real_torque_of_screwdriver");
            test.Property(t => t.ToolId).HasColumnName("screwdriver");
            test.Property(t => t.SetTorque).HasColumnName("set_torque").HasConversion<string>();
            test.Property(t => t.RealTorque).HasColumnName("real_torque").HasConversion<string>();
            test.Property(t => t.Diviation).HasColumnName("diviation").HasConversion<string>();
            test.Property(t => t.TestTime).HasColumnName("test_time");
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
