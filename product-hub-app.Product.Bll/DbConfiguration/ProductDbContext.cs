using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace product_hub_app.Product.Bll.DbConfiguration
{
    public class ProductDbContext : DbContext
    {
        public DbSet<Contracts.Models.Product> Products { get; set; }

        public ProductDbContext(DbContextOptions<ProductDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Contracts.Models.Product>()
                .HasKey(p => p.ProductId);


            base.OnModelCreating(builder);
        }
    }
}
