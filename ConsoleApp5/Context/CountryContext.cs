using System.Data.Entity;
using ConsoleApp5.Entity;

namespace ConsoleApp5
{
    internal class CountryContext : DbContext
    {
        public CountryContext() : base("DbConnection")
        {
        }

        public DbSet<Country> Countries { get; set; }
        public DbSet<Coin> Coins { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Country>()
                .HasMany(c => c.Neighbors)
                .WithMany()
                .Map(m =>
                {
                    m.MapLeftKey("CountryId");
                    m.MapRightKey("NeighborId");
                    m.ToTable("CountryNeighbor");
                });
        }
    }
}