using AgriStream.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppRole>().ToTable("Roles");
        b.Entity<AppRole>().Property(r => r.Id).HasColumnName("RoleId");
        b.Entity<AppRole>().Property(r => r.Name).HasColumnName("RoleName");
        b.Entity<AppUser>().ToTable("Users");
        b.Entity<AppUser>().Property(u => u.Id).HasColumnName("UserId");
        b.Entity<AppUser>().Property(u => u.CreatedUtc).HasColumnName("CreatedAt");

        b.Entity<AppRole>().HasIndex(r => r.Name).IsUnique();
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();

        b.Entity<AppUser>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
