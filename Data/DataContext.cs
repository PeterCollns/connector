using MetaAdsConnector.Entities;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsConnector.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        public DbSet<Lead> Leads { get; set; }
        public DbSet<LeadField> LeadFields { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Lead>()
                .HasIndex(l => l.Uuid)
                .IsUnique();

            modelBuilder.Entity<LeadField>()
                .HasIndex(f => new { f.LeadId, f.FieldName })
                .IsUnique();
        }
    }
}