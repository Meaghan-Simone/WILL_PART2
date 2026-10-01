using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UncoveringGreatnessCRM.Data;
using UncoveringGreatnessCRM.Domain;
using UncoveringGreatnessCRM.Models;
using UncoveringGreatnessCRM.Security;
using UncoveringGreatnessCRM.Services;

namespace UncoveringGreatnessCRM.Tests;

/// <summary>The signed-in person for a test. Mirrors the rules of the real CurrentUser.</summary>
public class FakeUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;
    public int Id { get; set; }
    public string Name { get; set; } = "Test User";
    public string Email { get; set; } = "test@example.com";
    public UserRole Role { get; set; } = UserRole.Staff;
    public bool IsAdmin => IsAuthenticated && Role == UserRole.Admin;
    public bool CanWrite => IsAuthenticated && (Role == UserRole.Admin || Role == UserRole.SalesRep);
    public string? IpAddress => "127.0.0.1";
}

public class FakeAudit : IAuditService
{
    public List<string> Actions { get; } = new();
    public Task LogAsync(string action, string? entity = null, object? entityId = null, string? details = null, int? userId = null, string? userName = null)
    { Actions.Add(action); return Task.CompletedTask; }
    public Task<PagedResult<AuditItem>> SearchAsync(string? q, string? action, int page, int pageSize) =>
        Task.FromResult(new PagedResult<AuditItem>(new List<AuditItem>(), page, pageSize, 0));
    public Task<List<string>> ActionsAsync() => Task.FromResult(Actions.Distinct().ToList());
}

public class FakeNotify : INotificationService
{
    public List<string> Titles { get; } = new();
    public Task NotifyAsync(int userId, NotificationType type, string title, string? body, string? link, string? dedupeKey = null)
    { Titles.Add(title); return Task.CompletedTask; }
    public Task NotifyManyAsync(IEnumerable<int> userIds, NotificationType type, string title, string? body, string? link, string? dedupeKey = null)
    { Titles.Add(title); return Task.CompletedTask; }
    public Task<PagedResult<NotificationDto>> ListAsync(string? filter, int page, int pageSize) =>
        Task.FromResult(new PagedResult<NotificationDto>(new List<NotificationDto>(), page, pageSize, 0));
    public Task<(int Unread, List<NotificationDto> Recent)> BellAsync() => Task.FromResult((0, new List<NotificationDto>()));
    public Task<Result> MarkReadAsync(int id) => Task.FromResult(Result.Success());
    public Task<int> MarkAllReadAsync() => Task.FromResult(0);
}

public class FakeStates : IUserStateCache
{
    public Task<UserState?> GetAsync(int userId) => Task.FromResult<UserState?>(new UserState(true, "stamp"));
    public void Invalidate(int userId) { }
}

public class FakeEmail : IEmailSender
{
    public bool IsSimulated => true;
    public Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// A throw-away in-memory SQLite database built from the real model, with foreign keys switched on,
/// so delete/relationship rules are tested exactly as they behave in the application.
/// </summary>
public sealed class TestWorld : IDisposable
{
    private readonly SqliteConnection _conn;
    public AppDbContext Db { get; }
    public FakeUser Me { get; } = new();
    public FakeAudit Audit { get; } = new();
    public FakeNotify Notify { get; } = new();

    public User Admin { get; private set; } = null!;
    public User AdminTwo { get; private set; } = null!;
    public User RepA { get; private set; } = null!;
    public User RepB { get; private set; } = null!;
    public User StaffUser { get; private set; } = null!;

    public TestWorld()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();

        Admin = AddUser("Ada Admin", "ada@example.com", UserRole.Admin);
        AdminTwo = AddUser("Alan Admin", "alan@example.com", UserRole.Admin);
        RepA = AddUser("Rita Rep", "rita@example.com", UserRole.SalesRep);
        RepB = AddUser("Ravi Rep", "ravi@example.com", UserRole.SalesRep);
        StaffUser = AddUser("Sam Staff", "sam@example.com", UserRole.Staff);
        Db.SaveChanges();
        SignInAs(Admin);
    }

    private User AddUser(string name, string email, UserRole role)
    {
        var u = new User { FullName = name, Email = email, Role = role, PasswordHash = "x" };
        Db.Users.Add(u);
        return u;
    }

    public void SignInAs(User u) { Me.Id = u.Id; Me.Name = u.FullName; Me.Email = u.Email; Me.Role = u.Role; }

    public Lead AddLead(string first, string surname, User? owner, LeadStage stage = LeadStage.Lead)
    {
        var l = new Lead { FirstName = first, Surname = surname, Email = $"{first}.{surname}@client.test".ToLowerInvariant(), AssignedToId = owner?.Id, Stage = stage };
        Db.Leads.Add(l);
        Db.SaveChanges();
        return l;
    }

    public LeadService Leads() => new(Db, Me, Audit, Notify);

    public UserService Users()
    {
        var auth = new AuthService(Db, new PasswordService(), Audit, new FakeStates(), new EphemeralDataProtectionProvider(),
            new FakeEmail(), new AppUrls(new ConfigurationBuilder().Build(), new HttpContextAccessor()), NullLogger<AuthService>.Instance);
        return new UserService(Db, Me, auth, Audit, new FakeStates(), Notify);
    }

    public void Dispose() { Db.Dispose(); _conn.Dispose(); }
}
