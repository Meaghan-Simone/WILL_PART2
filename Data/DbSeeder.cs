using Microsoft.EntityFrameworkCore;
using UncoveringGreatnessCRM.Domain;
using UncoveringGreatnessCRM.Helpers;
using UncoveringGreatnessCRM.Security;
using UncoveringGreatnessCRM.Services;

namespace UncoveringGreatnessCRM.Data;

public class SeedUser
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "SalesRep";
    public string Password { get; set; } = "";
}

public static class DbSeeder
{
    public static async Task InitialiseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var cfg = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");
        var passwords = sp.GetRequiredService<PasswordService>();

        if (cfg.GetValue("Database:UseMigrations", false)) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();

        if (!await db.Users.AnyAsync())
        {
            var mustChange = cfg.GetValue("Seed:MustChangePassword", false);
            var configured = cfg.GetSection("Seed:Users").Get<List<SeedUser>>() ?? new List<SeedUser>();

            if (configured.Count == 0)
            {
                // Nothing configured: create one admin with a random one-time password that is written to the log.
                var email = AuthService.NormalizeEmail(cfg["Seed:AdminEmail"] ?? "admin@uncoveringgreatness.co.za");
                var pw = PasswordService.GenerateTemporary();
                var admin = new User { FullName = "System Administrator", Email = email, Role = UserRole.Admin, MustChangePassword = true };
                admin.PasswordHash = passwords.Hash(admin, pw);
                db.Users.Add(admin);
                await db.SaveChangesAsync();
                log.LogWarning("=== FIRST RUN === Admin account created: {Email} | one-time password: {Password} | you must change it at first sign-in.", email, pw);
            }
            else
            {
                foreach (var s in configured)
                {
                    if (string.IsNullOrWhiteSpace(s.Email) || string.IsNullOrWhiteSpace(s.FullName))
                        throw new InvalidOperationException("Every Seed:Users entry needs a FullName and Email.");
                    var problems = PasswordService.Validate(s.Password, s.Email, s.FullName);
                    if (problems.Count > 0)
                        throw new InvalidOperationException($"Seed password for {s.Email} is not acceptable: {string.Join(" ", problems)}");
                    if (!Enum.TryParse<UserRole>(s.Role, true, out var role))
                        throw new InvalidOperationException($"Seed user {s.Email} has an unknown role '{s.Role}' (use Admin, SalesRep or Staff).");
                    var u = new User { FullName = s.FullName.Trim(), Email = AuthService.NormalizeEmail(s.Email), Role = role, MustChangePassword = mustChange };
                    u.PasswordHash = passwords.Hash(u, s.Password);
                    db.Users.Add(u);
                }
                await db.SaveChangesAsync();
                log.LogWarning("Seeded {Count} user(s) from configuration{Note}. Change these passwords before going live.", configured.Count,
                    mustChange ? " (password change required at first sign-in)" : "");
            }
        }

        // Optional test data: per-user leads, tasks, calendar, notifications, campaigns and a form connection.
        if (cfg.GetValue("Seed:DemoData", false))
            await DemoDataSeeder.SeedAsync(db, passwords, log);
    }
}
