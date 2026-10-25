using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ShoppingGroups.Api;

public class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<ShoppingList> Lists => Set<ShoppingList>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ListArchive> Archives => Set<ListArchive>();
    public DbSet<ShoppingSession> Sessions => Set<ShoppingSession>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailMessage> Emails => Set<EmailMessage>();
    public DbSet<AdminAudit> AdminAudits => Set<AdminAudit>();
    public DbSet<ProtectionKey> ProtectionKeys => Set<ProtectionKey>();
    public DbSet<PushApplicationKey> PushApplicationKeys => Set<PushApplicationKey>();
    public DbSet<PushDevice> PushDevices => Set<PushDevice>();
    public DbSet<PushDelivery> PushDeliveries => Set<PushDelivery>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<AppUser>().Property(x => x.DisplayName).HasMaxLength(80);
        b.Entity<Group>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<Group>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Member>().HasKey(x => new { x.GroupId, x.UserId });
        b.Entity<Member>().HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<Member>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.Entity<ShoppingList>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<ShoppingList>().HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<ShoppingList>().HasIndex(x => new { x.GroupId, x.DeletedAt });
        b.Entity<Product>().HasOne(x => x.List).WithMany(x => x.Items).HasForeignKey(x => x.ListId);
        b.Entity<Product>().Property(x => x.Text).HasMaxLength(200);
        b.Entity<Product>().Property(x => x.NormalizedText).HasMaxLength(64);
        b.Entity<Product>().Property(x => x.Quantity).HasPrecision(9, 3);
        b.Entity<Product>().Property(x => x.Unit).HasMaxLength(10);
        b.Entity<Product>().HasIndex(x => new { x.ListId, x.NormalizedText });
        b.Entity<Product>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<ListArchive>().HasKey(x => new { x.ListId, x.UserId });
        b.Entity<ListArchive>().HasOne(x => x.List).WithMany().HasForeignKey(x => x.ListId);
        b.Entity<ShoppingSession>().HasOne(x => x.List).WithMany().HasForeignKey(x => x.ListId);
        b.Entity<ShoppingSession>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShoppingSession>().HasIndex(x => new { x.ListId, x.UserId }).IsUnique().HasFilter("\"EndedAt\" IS NULL");
        b.Entity<Invitation>().HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<Invitation>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<Activity>().HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<Activity>().HasIndex(x => new { x.GroupId, x.Id });
        b.Entity<Activity>().HasIndex(x => new { x.ListId, x.Id });
        b.Entity<Notification>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.Entity<Notification>().HasIndex(x => new { x.UserId, x.ReadAt });
        b.Entity<EmailMessage>().HasIndex(x => new { x.SentAt, x.AvailableAt });
        b.Entity<PushDevice>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PushDevice>().Property(x => x.EndpointHash).HasMaxLength(64);
        b.Entity<PushDevice>().HasIndex(x => x.EndpointHash).IsUnique();
        b.Entity<PushDevice>().HasIndex(x => x.UserId);
        b.Entity<PushDelivery>().HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PushDelivery>().HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PushDelivery>().HasIndex(x => x.AvailableAt);
        b.Entity<PushDelivery>().HasIndex(x => new { x.NotificationId, x.DeviceId }).IsUnique();
    }
}
