using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Cart.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SD.ProjectName.Modules.Cart.Infrastructure
{
    public class CartDbContext : DbContext
    {
        public CartDbContext(DbContextOptions<CartDbContext> options) : base(options)
        {
        }

        public DbSet<CartItemModel> CartItems { get; set; } = null!;
    }
}
