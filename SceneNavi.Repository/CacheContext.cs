using Microsoft.EntityFrameworkCore;

namespace SceneNavi.Repository
{
    public class CacheContext : DbContext
    {

        public CacheContext(DbContextOptions options)
            : base(options)
        {
            
            
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}