using UncoveringGreatnessCRM.Domain;
using UncoveringGreatnessCRM.Security;
using UncoveringGreatnessCRM.Services;
using Xunit;

namespace UncoveringGreatnessCRM.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("short1!A")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoNumbersHere!!")]
    [InlineData("NoSymbols12345A")]
    public void Weak_passwords_are_rejected(string pw) => Assert.NotEmpty(PasswordService.Validate(pw));

    [Fact]
    public void Strong_password_is_accepted() => Assert.Empty(PasswordService.Validate("Tr1cky!Horse#Staple"));

    [Fact]
    public void Password_may_not_contain_the_persons_name_or_email()
    {
        Assert.NotEmpty(PasswordService.Validate("Johnson!2024xyz", "jane@example.com", "Peter Johnson"));
        Assert.NotEmpty(PasswordService.Validate("Xjane.doe!2024A", "jane.doe@example.com", "J D"));
    }

    [Fact]
    public void Generated_temporary_passwords_always_meet_the_policy()
    {
        for (var i = 0; i < 200; i++) Assert.Empty(PasswordService.Validate(PasswordService.GenerateTemporary()));
    }

    [Fact]
    public void Hashes_verify_and_are_not_the_plain_password()
    {
        var svc = new PasswordService();
        var user = new User { FullName = "T", Email = "t@example.com" };
        user.PasswordHash = svc.Hash(user, "Tr1cky!Horse#Staple");
        Assert.NotEqual("Tr1cky!Horse#Staple", user.PasswordHash);
        Assert.True(svc.Verify(user, "Tr1cky!Horse#Staple", out _));
        Assert.False(svc.Verify(user, "wrong-Password1!", out _));
    }
}

public class ResultTests
{
    [Fact]
    public void Results_carry_status_message_and_field()
    {
        Assert.True(Result.Success().Succeeded);
        var r = Result.Invalid("Email", "bad");
        Assert.Equal(ResultStatus.Invalid, r.Status);
        Assert.Equal("bad", r.Errors!["Email"][0]);
        Assert.Equal(ResultStatus.NotFound, Result.NotFound("Lead").Status);
        Assert.Equal(ResultStatus.Forbidden, Result.Forbidden().Status);
        Assert.Equal(ResultStatus.Conflict, Result.Conflict("x").Status);
    }

    [Fact]
    public void Typed_results_keep_the_failure_of_the_inner_result()
    {
        var typed = Result<int>.From(Result.Forbidden("no"));
        Assert.False(typed.Succeeded);
        Assert.Equal("no", typed.Error);
    }
}

/// <summary>Regression tests for the audit log, including the filter that used to hide every entry.</summary>
public class AuditLogTests
{
    [Fact]
    public async Task Entries_are_stored_with_who_what_and_where_and_can_be_filtered()
    {
        using var w = new TestWorld();
        var audit = new AuditService(w.Db, w.Me);
        await audit.LogAsync("UserCreated", "User", 7, "a@b.c");
        await audit.LogAsync("UserDeleted", "User", 8, "d@e.f");
        await audit.LogAsync("LeadCreated", "Lead", 1, "Anna");

        var all = await audit.SearchAsync(null, null, 1, 50);
        Assert.Equal(3, all.TotalCount);
        Assert.Equal("Ada Admin", all.Items[0].UserName);
        Assert.Equal("127.0.0.1", all.Items[0].IpAddress);

        var deleted = await audit.SearchAsync(null, "UserDeleted", 1, 50);
        Assert.Single(deleted.Items);
        Assert.Equal("8", deleted.Items[0].EntityId);

        var text = await audit.SearchAsync("Anna", null, 1, 50);
        Assert.Single(text.Items);
        Assert.Equal(new[] { "LeadCreated", "UserCreated", "UserDeleted" }, await audit.ActionsAsync());
    }
}
