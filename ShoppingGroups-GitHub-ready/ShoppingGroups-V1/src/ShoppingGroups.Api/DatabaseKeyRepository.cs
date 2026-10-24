using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ShoppingGroups.Api;

public class ProtectionKey
{
    public long Id { get; set; }
    public string FriendlyName { get; set; } = "";
    public string Xml { get; set; } = "";
}

// Key-ring storage survives replacement of an ephemeral web container.
// A scope per operation avoids retaining a request DbContext in the singleton repository.
public sealed class DatabaseKeyRepository(IServiceScopeFactory scopes) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return db.ProtectionKeys.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Xml).ToList()
            .Select(XElement.Parse).ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.ProtectionKeys.Add(new ProtectionKey { FriendlyName = friendlyName, Xml = element.ToString(SaveOptions.DisableFormatting) });
        db.SaveChanges();
    }
}
