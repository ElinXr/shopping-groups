using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoppingGroups.Domain;

namespace ShoppingGroups.Api;

public record RegisterInput(string Email, string Password, string DisplayName);
public record LoginInput(string Email, string Password);
public record EmailInput(string Email);
public record TokenInput(string Email, string Token);
public record ResetInput(string Email, string Token, string Password);
public record ProfileInput(string DisplayName, bool EmailNotifications);

public static class AuthEndpoints
{
    static string Email(string email)
    {
        var value = Rules.Text(email, "Имейл", 254).ToLowerInvariant();
        if (!MailAddress.TryCreate(value, out var address) || address.Address != value)
            throw new RuleException(400, "validation", "Въведете валиден имейл адрес.");
        return value;
    }

    static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new RuleException(400, "validation", string.Join(" ", result.Errors.Select(x => x.Description)));
    }

    static void QueueToken(AppDb db, IConfiguration config, AppUser user, string token, string action)
    {
        var url = $"{config["PublicUrl"]!.TrimEnd('/')}/?{action}={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";
        db.Emails.Add(new EmailMessage { To = user.Email!, Subject = action == "confirm" ? "Потвърдете имейла си" : "Нова парола",
            Body = $"Shopping Groups\n\nОтворете линка:\n{url}\n\nАко не сте поискали това действие, игнорирайте съобщението." });
    }

    public static void MapAuth(this WebApplication app)
    {
        app.MapGet("/api/security/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken })).AllowAnonymous();

        var auth = app.MapGroup("/api/auth").RequireRateLimiting("auth");
        auth.MapPost("/register", async (RegisterInput input, UserManager<AppUser> users, AppDb db, IConfiguration config) =>
        {
            var email = Email(input.Email);
            var user = new AppUser { Email = email, UserName = email, DisplayName = Rules.Text(input.DisplayName, "Име", 80) };
            Check(await users.CreateAsync(user, input.Password));
            QueueToken(db, config, user, await users.GenerateEmailConfirmationTokenAsync(user), "confirm");
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Профилът е създаден. Можете да влезете. Изпратихме линк за потвърждение." });
        }).AllowAnonymous();

        auth.MapPost("/login", async (LoginInput input, UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
        {
            var user = await users.FindByEmailAsync(Email(input.Email));
            if (user == null || user.Disabled) return Results.Problem(statusCode: 401, title: "Грешен имейл или парола.");
            var result = await signIn.PasswordSignInAsync(user, input.Password, true, lockoutOnFailure: true);
            return result.Succeeded ? Results.Ok() : Results.Problem(statusCode: result.IsLockedOut ? 429 : 401, title: "Грешен имейл или парола, или временно заключен профил.");
        }).AllowAnonymous();

        auth.MapPost("/logout", async (SignInManager<AppUser> signIn, HttpContext context, AppDb db) =>
        {
            if (Guid.TryParse(context.Request.Headers["X-Push-Device"].ToString(), out var deviceId))
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                await db.PushDevices.Where(x => x.Id == deviceId && x.UserId == userId).ExecuteDeleteAsync();
            }
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<AppUser> users) =>
        {
            var user = (await users.GetUserAsync(principal))!;
            return Results.Ok(new { user.Id, user.Email, user.DisplayName, user.EmailConfirmed, user.EmailNotifications, isAdmin = await users.IsInRoleAsync(user, "Admin") });
        }).RequireAuthorization();

        app.MapPut("/api/auth/profile", async (ProfileInput input, ClaimsPrincipal principal, UserManager<AppUser> users) =>
        {
            var user = (await users.GetUserAsync(principal))!;
            user.DisplayName = Rules.Text(input.DisplayName, "Име", 80);
            user.EmailNotifications = input.EmailNotifications;
            Check(await users.UpdateAsync(user));
            return Results.NoContent();
        }).RequireAuthorization();

        auth.MapPost("/confirm", async (TokenInput input, UserManager<AppUser> users) =>
        {
            var user = await users.FindByEmailAsync(Email(input.Email)) ?? throw new RuleException(400, "invalid_token", "Невалиден линк.");
            Check(await users.ConfirmEmailAsync(user, input.Token));
            return Results.Ok();
        }).AllowAnonymous();

        auth.MapPost("/resend-confirmation", async (EmailInput input, AppDb db, UserManager<AppUser> users, IConfiguration config) =>
        {
            var user = await users.FindByEmailAsync(Email(input.Email));
            if (user is { EmailConfirmed: false, Disabled: false })
            {
                QueueToken(db, config, user, await users.GenerateEmailConfirmationTokenAsync(user), "confirm");
                await db.SaveChangesAsync();
            }
            return Results.Ok(new { message = "Ако профилът съществува, ще получите имейл." });
        }).AllowAnonymous();

        auth.MapPost("/forgot-password", async (EmailInput input, AppDb db, UserManager<AppUser> users, IConfiguration config) =>
        {
            var user = await users.FindByEmailAsync(Email(input.Email));
            if (user is { EmailConfirmed: true, Disabled: false })
            {
                QueueToken(db, config, user, await users.GeneratePasswordResetTokenAsync(user), "reset");
                await db.SaveChangesAsync();
            }
            return Results.Ok(new { message = "Ако профилът съществува и е потвърден, ще получите линк." });
        }).AllowAnonymous();

        auth.MapPost("/reset-password", async (ResetInput input, UserManager<AppUser> users) =>
        {
            var user = await users.FindByEmailAsync(Email(input.Email)) ?? throw new RuleException(400, "invalid_token", "Невалиден линк.");
            Check(await users.ResetPasswordAsync(user, input.Token, input.Password));
            return Results.Ok();
        }).AllowAnonymous();
    }
}
