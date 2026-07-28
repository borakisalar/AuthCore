using Microsoft.EntityFrameworkCore;

namespace StajBackendProject.Models
{
    public class AdventureWorksContext : DbContext
    {
        public AdventureWorksContext(DbContextOptions<AdventureWorksContext> options) : base(options) { }

        public DbSet<SalesOrderDetail> SalesOrderDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SalesOrderDetail>()
                .ToTable("SalesOrderDetail", "Sales")
                .HasKey(s => new { s.SalesOrderID, s.SalesOrderDetailID });
        }
    }

    public class SalesOrderDetail
    {
        public int SalesOrderID { get; set; }
        public int SalesOrderDetailID { get; set; }
        public string? CarrierTrackingNumber { get; set; }
        public short OrderQty { get; set; }
        public int ProductID { get; set; }
        public int SpecialOfferID { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitPriceDiscount { get; set; }
        public decimal LineTotal { get; set; }
        public Guid rowguid { get; set; }
        public DateTime ModifiedDate { get; set; }
    }
}