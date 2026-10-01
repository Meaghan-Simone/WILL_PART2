using Microsoft.EntityFrameworkCore;
using UncoveringGreatnessCRM.Domain;
using UncoveringGreatnessCRM.Models;
using UncoveringGreatnessCRM.Services;
using Xunit;

namespace UncoveringGreatnessCRM.Tests;

/// <summary>Create / read / update / delete rules for user accounts.</summary>
public class UserManagementTests
{
    private static UserCreateInput NewUser(string email = "new.person@example.com", string pw = "Str0ng!Passw0rd#") =>
        new() { FullName = "New Person", Email = email, Role = UserRole.SalesRep, TemporaryPassword = pw };

    [Fact]
    public async Task Admin_can_create_a_user_who_must_change_password()
    {
        using var w = new TestWorld();
        var r = await w.Users().CreateAsync(NewUser());
        Assert.True(r.Succeeded);
        var u = await w.Db.Users.SingleAsync(x => x.Email == "new.person@example.com");
        Assert.True(u.MustChangePassword);
        Assert.NotEqual("Str0ng!Passw0rd#", u.PasswordHash); // never stored in clear text
        Assert.Contains("UserCreated", w.Audit.Actions);
    }

    [Fact]
    public async Task Duplicate_email_and_weak_password_are_rejected()
    {
        using var w = new TestWorld();
        Assert.Equal(ResultStatus.Conflict, (await w.Users().CreateAsync(NewUser("RITA@example.com"))).Status);
        Assert.Equal(ResultStatus.Invalid, (await w.Users().CreateAsync(NewUser("x@example.com", "short"))).Status);
    }

    [Fact]
    public async Task Only_admins_manage_users()
    {
        using var w = new TestWorld();
        w.SignInAs(w.RepA);
        var svc = w.Users();
        Assert.Equal(ResultStatus.Forbidden, (await svc.CreateAsync(NewUser())).Status);
        Assert.Equal(ResultStatus.Forbidden, (await svc.ListAsync()).Status);
        Assert.Equal(ResultStatus.Forbidden, (await svc.DeleteAsync(w.RepB.Id, w.RepA.Id)).Status);
    }

    [Fact]
    public async Task Update_changes_details_and_protects_the_last_admin_and_yourself()
    {
        using var w = new TestWorld();
        var svc = w.Users();
        var ok = await svc.UpdateAsync(w.RepA.Id, new UserUpdateInput { FullName = "Rita R.", Email = "rita@example.com", Role = UserRole.SalesRep, IsActive = true });
        Assert.True(ok.Succeeded);
        Assert.Equal("Rita R.", (await w.Db.Users.AsNoTracking().SingleAsync(u => u.Id == w.RepA.Id)).FullName);

        // an admin cannot demote themselves
        var self = await svc.UpdateAsync(w.Admin.Id, new UserUpdateInput { FullName = "Ada", Email = "ada@example.com", Role = UserRole.Staff, IsActive = true });
        Assert.Equal(ResultStatus.Conflict, self.Status);
    }

    [Fact]
    public async Task User_without_leads_or_tasks_is_deleted_without_choosing_a_new_owner()
    {
        using var w = new TestWorld();
        var r = await w.Users().DeleteAsync(w.StaffUser.Id, null);
        Assert.True(r.Succeeded, r.Error);
        Assert.False(await w.Db.Users.AnyAsync(u => u.Id == w.StaffUser.Id));
        Assert.Contains("UserDeleted", w.Audit.Actions);
    }

    [Fact]
    public async Task User_with_leads_needs_a_new_owner_and_is_kept_until_one_is_chosen()
    {
        using var w = new TestWorld();
        w.AddLead("Anna", "Alpha", w.RepA);
        var refused = await w.Users().DeleteAsync(w.RepA.Id, null);
        Assert.Equal(ResultStatus.Invalid, refused.Status);
        Assert.True(await w.Db.Users.AnyAsync(u => u.Id == w.RepA.Id));
    }

    [Fact]
    public async Task Deleting_a_user_hands_their_leads_and_tasks_to_the_new_owner_and_removes_personal_data()
    {
        using var w = new TestWorld();
        var lead = w.AddLead("Anna", "Alpha", w.RepA);
        w.Db.Tasks.Add(new CrmTask { Title = "Call Anna", AssignedToId = w.RepA.Id, CreatedById = w.RepA.Id });
        w.Db.Notifications.Add(new Notification { UserId = w.RepA.Id, Title = "Hi" });
        w.Db.CalendarEvents.Add(new CalendarEvent { Title = "Lunch", OwnerId = w.RepA.Id, StartsAt = DateTime.UtcNow });
        w.Db.LeadNotes.Add(new LeadNote { LeadId = lead.Id, AuthorId = w.RepA.Id, Body = "note" });
        await w.Db.SaveChangesAsync();

        var r = await w.Users().DeleteAsync(w.RepA.Id, w.RepB.Id);
        Assert.True(r.Succeeded, r.Error);

        w.Db.ChangeTracker.Clear();
        Assert.False(await w.Db.Users.AnyAsync(u => u.Id == w.RepA.Id));
        Assert.Equal(w.RepB.Id, (await w.Db.Leads.SingleAsync(l => l.Id == lead.Id)).AssignedToId);
        Assert.Equal(w.RepB.Id, (await w.Db.Tasks.SingleAsync()).AssignedToId);
        Assert.Empty(w.Db.Notifications.Where(n => n.UserId == w.RepA.Id));
        Assert.Empty(w.Db.CalendarEvents);
        Assert.Null((await w.Db.LeadNotes.SingleAsync()).AuthorId); // history is kept, author is cleared
    }

    [Fact]
    public async Task Invalid_new_owner_is_refused()
    {
        using var w = new TestWorld();
        w.AddLead("Anna", "Alpha", w.RepA);
        var r = await w.Users().DeleteAsync(w.RepA.Id, w.StaffUser.Id); // staff cannot own leads
        Assert.Equal(ResultStatus.Invalid, r.Status);
        Assert.True(await w.Db.Users.AnyAsync(u => u.Id == w.RepA.Id));
    }

    [Fact]
    public async Task You_cannot_delete_yourself()
    {
        using var w = new TestWorld();
        var r = await w.Users().DeleteAsync(w.Admin.Id, w.AdminTwo.Id);
        Assert.Equal(ResultStatus.Conflict, r.Status);
        Assert.True(await w.Db.Users.AnyAsync(u => u.Id == w.Admin.Id));
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_deleted()
    {
        using var w = new TestWorld();
        w.AdminTwo.IsActive = false;           // Ada is now the only active admin
        await w.Db.SaveChangesAsync();
        w.SignInAs(w.AdminTwo);                // the (now inactive) second admin tries to delete the only active admin
        var r = await w.Users().DeleteAsync(w.Admin.Id, w.RepA.Id);
        Assert.Equal(ResultStatus.Conflict, r.Status);
    }
}
