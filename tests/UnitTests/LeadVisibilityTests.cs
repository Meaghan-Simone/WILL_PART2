using UncoveringGreatnessCRM.Domain;
using UncoveringGreatnessCRM.Models;
using UncoveringGreatnessCRM.Services;
using Xunit;

namespace UncoveringGreatnessCRM.Tests;

/// <summary>Business rule: only admins see every lead; everyone else only sees the leads assigned to them.</summary>
public class LeadVisibilityTests
{
    private static TestWorld Seeded(out Lead aLead, out Lead bLead, out Lead unassigned)
    {
        var w = new TestWorld();
        aLead = w.AddLead("Anna", "Alpha", w.RepA);
        w.AddLead("Aaron", "Alpha", w.RepA);
        bLead = w.AddLead("Bruno", "Beta", w.RepB);
        unassigned = w.AddLead("Una", "Nobody", null);
        return w;
    }

    [Fact]
    public async Task Admin_sees_every_lead()
    {
        using var w = Seeded(out _, out _, out _);
        w.SignInAs(w.Admin);
        var page = await w.Leads().SearchAsync(new LeadQuery(PageSize: 100));
        Assert.Equal(4, page.TotalCount);
    }

    [Fact]
    public async Task Rep_only_sees_their_own_leads()
    {
        using var w = Seeded(out _, out _, out _);
        w.SignInAs(w.RepA);
        var page = await w.Leads().SearchAsync(new LeadQuery(PageSize: 100));
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, l => Assert.Equal(w.RepA.Id, l.AssignedToId));
    }

    [Fact]
    public async Task Rep_cannot_widen_the_list_with_filters()
    {
        using var w = Seeded(out _, out _, out _);
        w.SignInAs(w.RepA);
        var page = await w.Leads().SearchAsync(new LeadQuery(AssignedToId: w.RepB.Id, PageSize: 100));
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Staff_see_no_leads_and_unassigned_leads_are_admin_only()
    {
        using var w = Seeded(out _, out _, out var unassigned);
        w.SignInAs(w.StaffUser);
        Assert.Equal(0, (await w.Leads().SearchAsync(new LeadQuery(PageSize: 100))).TotalCount);
        w.SignInAs(w.RepA);
        Assert.Equal(ResultStatus.NotFound, (await w.Leads().GetAsync(unassigned.Id)).Status);
    }

    [Fact]
    public async Task Rep_opening_someone_elses_lead_gets_not_found()
    {
        using var w = Seeded(out _, out var bLead, out _);
        w.SignInAs(w.RepA);
        var r = await w.Leads().GetAsync(bLead.Id);
        Assert.Equal(ResultStatus.NotFound, r.Status);
    }

    [Fact]
    public async Task Rep_can_open_and_edit_their_own_lead()
    {
        using var w = Seeded(out var aLead, out _, out _);
        w.SignInAs(w.RepA);
        var r = await w.Leads().GetAsync(aLead.Id);
        Assert.True(r.Succeeded);
        Assert.True(r.Value!.CanEdit);
    }

    [Fact]
    public async Task Rep_cannot_change_stage_note_or_delete_someone_elses_lead()
    {
        using var w = Seeded(out _, out var bLead, out _);
        w.SignInAs(w.RepA);
        var svc = w.Leads();
        Assert.False((await svc.ChangeStageAsync(bLead.Id, LeadStage.Qualified)).Succeeded);
        Assert.False((await svc.AddNoteAsync(bLead.Id, "hello")).Succeeded);
        Assert.False((await svc.DeleteAsync(bLead.Id)).Succeeded);
        Assert.True(w.Db.Leads.Any(l => l.Id == bLead.Id));
        Assert.Equal(LeadStage.Lead, w.Db.Leads.Single(l => l.Id == bLead.Id).Stage);
    }

    [Fact]
    public async Task Only_admins_can_delete_leads()
    {
        using var w = Seeded(out var aLead, out _, out _);
        w.SignInAs(w.RepA);
        Assert.Equal(ResultStatus.Forbidden, (await w.Leads().DeleteAsync(aLead.Id)).Status);
        w.SignInAs(w.Admin);
        Assert.True((await w.Leads().DeleteAsync(aLead.Id)).Succeeded);
        Assert.False(w.Db.Leads.Any(l => l.Id == aLead.Id));
    }

    [Fact]
    public async Task A_rep_creating_a_lead_always_owns_it()
    {
        using var w = Seeded(out _, out _, out _);
        w.SignInAs(w.RepA);
        var r = await w.Leads().CreateAsync(new LeadInput { FirstName = "New", Surname = "Person", Email = "new.person@client.test", AssignedToId = w.RepB.Id });
        Assert.True(r.Succeeded);
        Assert.Equal(w.RepA.Id, w.Db.Leads.Single(l => l.Id == r.Value).AssignedToId);
    }

    [Fact]
    public async Task A_rep_cannot_leave_a_lead_unassigned_but_an_admin_can()
    {
        using var w = Seeded(out var aLead, out _, out _);
        w.SignInAs(w.RepA);
        var r = await w.Leads().AssignAsync(aLead.Id, null);
        Assert.Equal(ResultStatus.Invalid, r.Status);
        w.SignInAs(w.Admin);
        Assert.True((await w.Leads().AssignAsync(aLead.Id, null)).Succeeded);
    }

    [Fact]
    public async Task A_rep_handing_over_a_lead_loses_access_to_it()
    {
        using var w = Seeded(out var aLead, out _, out _);
        w.SignInAs(w.RepA);
        Assert.True((await w.Leads().AssignAsync(aLead.Id, w.RepB.Id)).Succeeded);
        Assert.Equal(ResultStatus.NotFound, (await w.Leads().GetAsync(aLead.Id)).Status);
        w.SignInAs(w.RepB);
        Assert.True((await w.Leads().GetAsync(aLead.Id)).Succeeded);
    }

    [Fact]
    public async Task Pipeline_and_tab_counts_respect_ownership()
    {
        using var w = Seeded(out _, out _, out _);
        w.SignInAs(w.RepA);
        var pipeline = await w.Leads().PipelineAsync(null, null, null);
        Assert.Equal(2, pipeline.Columns.Sum(c => c.Count));
        var tabs = await w.Leads().TabCountsAsync();
        Assert.Equal(2, tabs.All);
        Assert.Equal(2, tabs.Mine);
    }

    [Fact]
    public async Task Csv_export_only_contains_visible_leads()
    {
        using var w = Seeded(out _, out _, out _);
        w.SignInAs(w.RepA);
        var csv = System.Text.Encoding.UTF8.GetString(await w.Leads().ExportCsvAsync(new LeadQuery()));
        Assert.Contains("Anna", csv);
        Assert.DoesNotContain("Bruno", csv);
    }

    [Fact]
    public async Task Duplicate_emails_are_rejected()
    {
        using var w = Seeded(out _, out _, out _);
        var r = await w.Leads().CreateAsync(new LeadInput { FirstName = "Dup", Surname = "Licate", Email = "ANNA.alpha@client.test" });
        Assert.Equal(ResultStatus.Conflict, r.Status);
    }
}
