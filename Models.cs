using Microsoft.AspNetCore.Identity;

namespace ShoppingGroups.Api;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public bool Disabled { get; set; }
    public bool EmailNotifications { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid Version { get; set; } = Guid.NewGuid();
    public bool Disabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Member
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public string UserId { get; set; } = "";
    public AppUser User { get; set; } = null!;
    public string Role { get; set; } = "member";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class ShoppingList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public string Name { get; set; } = "";
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long LastSummaryActivityId { get; set; }
    public bool RunStarted { get; set; }
    public List<Product> Items { get; set; } = [];
}

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListId { get; set; }
    public ShoppingList List { get; set; } = null!;
    public string Text { get; set; } = "";
    public string NormalizedText { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public string Unit { get; set; } = "pcs";
    public Guid Version { get; set; } = Guid.NewGuid();
    public string AddedBy { get; set; } = "";
    public bool Purchased { get; set; }
    public string? PurchasedBy { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ListArchive
{
    public Guid ListId { get; set; }
    public ShoppingList List { get; set; } = null!;
    public string UserId { get; set; } = "";
}

public class ShoppingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListId { get; set; }
    public ShoppingList List { get; set; } = null!;
    public string UserId { get; set; } = "";
    public AppUser User { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}

public class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public string? Email { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int MaxUses { get; set; } = 1;
    public int Uses { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Activity
{
    public long Id { get; set; }
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid? ListId { get; set; }
    public string ActorId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public string Title { get; set; } = "";
    public string SenderName { get; set; } = "";
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public AppUser User { get; set; } = null!;
    public Guid GroupId { get; set; }
    public Guid? ListId { get; set; }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public class EmailMessage
{
    public long Id { get; set; }
    public Guid? GroupId { get; set; }
    public string? UserId { get; set; }
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public int Attempts { get; set; }
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
}

public class AdminAudit
{
    public long Id { get; set; }
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
