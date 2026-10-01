using Microsoft.EntityFrameworkCore;
using UncoveringGreatnessCRM.Domain;
using UncoveringGreatnessCRM.Helpers;
using UncoveringGreatnessCRM.Security;
using UncoveringGreatnessCRM.Services;

namespace UncoveringGreatnessCRM.Data;

/// <summary>
/// Loads a realistic, per-user test data set so every screen, role and workflow can be exercised straight away.
/// Enabled with <c>Seed:DemoData = true</c>. It only fills an EMPTY CRM (no leads, companies or events), so it never
/// touches real data, and all dates are relative to "today" so the demo always looks current.
/// Every demo account uses the password <see cref="DemoPassword"/>. Turn this off before going live.
/// </summary>
public static class DemoDataSeeder
{
    public const string DemoPassword = "P@ssword4321";

    // Rep keys used below: 0 = unassigned, 1 = Michaela, 2 = Sule, 3 = Barry
    private sealed record LeadSeed(string First, string Sur, string Title, int Co, int Ev, LeadSource Src, LeadStage Stage,
        decimal Value, int Rep, int? Follow, string City, string Prov, string Industry);

    private sealed record TaskSeed(string Title, string? Desc, int? Due, TaskPriority Pri, bool Done, int? Slot);

    private sealed record CalSeed(string Title, int Day, string Time, CalendarItemType Type, CalendarVisibility Vis,
        string[] With, string? Notes, int? Slot);

    private static readonly (string Name, string Industry, string Size, string Domain, string Street, string City, string Prov, string Contact)[] CompanyData =
    {
        ("Summit Peak Consulting",      "Management Consulting",  "51-200",   "summitpeak.example",      "12 Rivonia Road",        "Sandton",        "Gauteng",       "Thandiwe Nkosi"),
        ("Karoo Logistics",             "Transport & Logistics",  "201-500",  "karoologistics.example",  "88 Voortrekker Street",  "Bloemfontein",   "Free State",    "Gerhard Muller"),
        ("Baobab Financial Services",   "Financial Services",     "201-500",  "baobabfinancial.example", "5 Fox Street",           "Johannesburg",   "Gauteng",       "Sipho Dlamini"),
        ("Ubuntu Health Group",         "Healthcare",             "501-1000", "ubuntuhealth.example",    "30 Florida Road",        "Durban",         "KwaZulu-Natal", "Thabiso Moloi"),
        ("Table Bay Technologies",      "Information Technology", "51-200",   "tablebaytech.example",    "101 Bree Street",        "Cape Town",      "Western Cape",  "Megan Fourie"),
        ("Highveld Mining Supplies",    "Mining",                 "201-500",  "highveldmining.example",  "7 Mine Road",            "eMalahleni",     "Mpumalanga",    "Willem Kruger"),
        ("Protea Retail Holdings",      "Retail",                 "1000+",    "protearetail.example",    "250 Church Street",      "Pretoria",       "Gauteng",       "Jaco van der Merwe"),
        ("Drakensberg Hospitality",     "Hospitality",            "51-200",   "drakensberghosp.example", "14 Chief Albert Luthuli", "Pietermaritzburg", "KwaZulu-Natal", "Naledi Baloyi"),
        ("Cradle Education Trust",      "Education",              "11-50",    "cradleeducation.example", "3 Grand Central Blvd",   "Midrand",        "Gauteng",       "Karabo Sithole"),
        ("Kalahari Renewable Energy",   "Energy",                 "51-200",   "kalaharienergy.example",  "22 Schröder Street",     "Upington",       "Northern Cape", "Elaine Cronje"),
    };

    private static readonly LeadSeed[] LeadData =
    {
        // ---- Michaela (consulting, finance, technology)
        new("Thandiwe", "Nkosi",     "Chief Executive Officer", 0, 3, LeadSource.Referral, LeadStage.Qualified,   85000, 1,  3, "Sandton",       "Gauteng",       "Management Consulting"),
        new("Johan",    "Pretorius", "Managing Director",       0, 0, LeadSource.Event,    LeadStage.ClosedWon, 120000, 1, null, "Sandton",       "Gauteng",       "Management Consulting"),
        new("Ayesha",   "Patel",     "Head of People",          2, 3, LeadSource.WebForm,  LeadStage.Lead,       15000, 1,  1, "Johannesburg",  "Gauteng",       "Financial Services"),
        new("Sipho",    "Dlamini",   "Chief Financial Officer", 2, 1, LeadSource.Event,    LeadStage.Proposal,  150000, 1,  5, "Johannesburg",  "Gauteng",       "Financial Services"),
        new("Megan",    "Fourie",    "Chief Technology Officer",4, 3, LeadSource.Website,  LeadStage.Negotiation,210000, 1, 2, "Cape Town",     "Western Cape",  "Information Technology"),
        new("Lwazi",    "Mthembu",   "Innovation Lead",         4, 2, LeadSource.Social,   LeadStage.Qualified,   60000, 1, -2, "Cape Town",     "Western Cape",  "Information Technology"),
        new("Craig",    "Naidoo",    "Head of Sales",           4, 0, LeadSource.ColdCall, LeadStage.ClosedLost,  40000, 1, null, "Cape Town",     "Western Cape",  "Information Technology"),
        new("Zanele",   "Mahlangu",  "Director",                0, 1, LeadSource.Referral, LeadStage.Proposal,    95000, 1,  8, "Sandton",       "Gauteng",       "Management Consulting"),
        new("Pieter",   "Botha",     "Partner",                 0, 0, LeadSource.Event,    LeadStage.ClosedWon, 180000, 1, null, "Sandton",       "Gauteng",       "Management Consulting"),
        new("Nandi",    "Zulu",      "Operations Director",     2,-1, LeadSource.Website,  LeadStage.Lead,       25000, 1,  0, "Johannesburg",  "Gauteng",       "Financial Services"),
        new("Riaan",    "Steyn",     "Business Analyst",        4, 4, LeadSource.Social,   LeadStage.Negotiation, 70000, 1,  4, "Stellenbosch",  "Western Cape",  "Information Technology"),

        // ---- Sule (healthcare, education hospitality)
        new("Thabiso",  "Moloi",     "Medical Director",        3, 3, LeadSource.Referral, LeadStage.Qualified,   90000, 2,  2, "Durban",        "KwaZulu-Natal", "Healthcare"),
        new("Fatima",   "Cassim",    "HR Manager",              3, 1, LeadSource.Event,    LeadStage.Proposal,    65000, 2,  6, "Durban",        "KwaZulu-Natal", "Healthcare"),
        new("Bongani",  "Cele",      "Operations Manager",      3, 2, LeadSource.Website,  LeadStage.Lead,       20000, 2, -1, "Durban",        "KwaZulu-Natal", "Healthcare"),
        new("Karabo",   "Sithole",   "Programme Director",      8, 3, LeadSource.WebForm,  LeadStage.Qualified,   45000, 2,  3, "Midrand",       "Gauteng",       "Education"),
        new("Annelise", "Joubert",   "Principal",               8, 0, LeadSource.Event,    LeadStage.ClosedWon,   55000, 2, null, "Midrand",       "Gauteng",       "Education"),
        new("Mpho",     "Ledwaba",   "Fundraising Lead",        8, 1, LeadSource.Referral, LeadStage.Negotiation, 80000, 2,  1, "Midrand",       "Gauteng",       "Education"),
        new("Hendrik",  "Visser",    "Facilities Manager",      7, 4, LeadSource.Website,  LeadStage.Lead,       18000, 2,  9, "Pietermaritzburg","KwaZulu-Natal","Hospitality"),
        new("Naledi",   "Baloyi",    "General Manager",         7, 3, LeadSource.Referral, LeadStage.Proposal,   130000, 2, -3, "Pietermaritzburg","KwaZulu-Natal","Hospitality"),
        new("Yusuf",    "Ebrahim",   "Regional Director",       3, 0, LeadSource.Event,    LeadStage.ClosedWon,  110000, 2, null, "Durban",        "KwaZulu-Natal", "Healthcare"),
        new("Tshepo",   "Maseko",    "Training Coordinator",    8, 2, LeadSource.Social,   LeadStage.ClosedLost,  22000, 2, null, "Midrand",       "Gauteng",       "Education"),
        new("Candice",  "Pillay",    "HR Business Partner",     7, 1, LeadSource.ColdCall, LeadStage.Negotiation, 75000, 2,  7, "Pietermaritzburg","KwaZulu-Natal","Hospitality"),

        // ---- Barry (mining, retail, logistics, energy)
        new("Willem",   "Kruger",    "Procurement Manager",     5, 3, LeadSource.ColdCall, LeadStage.Qualified,  160000, 3,  4, "eMalahleni",    "Mpumalanga",    "Mining"),
        new("Busisiwe", "Ngcobo",    "Safety Officer",          5, 1, LeadSource.Event,    LeadStage.Proposal,    98000, 3,  3, "eMalahleni",    "Mpumalanga",    "Mining"),
        new("Gerhard",  "Muller",    "Logistics Director",      1, 3, LeadSource.Referral, LeadStage.Negotiation,190000, 3,  1, "Bloemfontein",  "Free State",    "Transport & Logistics"),
        new("Ntombi",   "Radebe",    "Fleet Manager",           1, 0, LeadSource.Event,    LeadStage.ClosedWon,  140000, 3, null, "Bloemfontein",  "Free State",    "Transport & Logistics"),
        new("Jaco",     "van der Merwe","Store Operations Head",6, 3, LeadSource.Website,  LeadStage.Lead,       30000, 3, -4, "Pretoria",       "Gauteng",       "Retail"),
        new("Refilwe",  "Mokoena",   "Marketing Manager",       6, 2, LeadSource.Social,   LeadStage.Qualified,   52000, 3,  5, "Pretoria",       "Gauteng",       "Retail"),
        new("Samantha", "Govender",  "Regional HR Manager",     6, 0, LeadSource.Event,    LeadStage.ClosedWon,   88000, 3, null, "Pretoria",       "Gauteng",       "Retail"),
        new("Dumisani", "Khoza",     "Plant Manager",           5, 4, LeadSource.Referral, LeadStage.Lead,       35000, 3, 10, "eMalahleni",    "Mpumalanga",    "Mining"),
        new("Elaine",   "Cronje",    "Sustainability Lead",     9, 3, LeadSource.Website,  LeadStage.Proposal,   175000, 3,  2, "Upington",       "Northern Cape", "Energy"),
        new("Mandla",   "Ntuli",     "Project Director",        9, 1, LeadSource.Referral, LeadStage.ClosedLost, 120000, 3, null, "Upington",       "Northern Cape", "Energy"),
        new("Chantelle","Adams",     "Transport Planner",       1, 2, LeadSource.WebForm,  LeadStage.Qualified,   48000, 3,  6, "Bloemfontein",  "Free State",    "Transport & Logistics"),

        // ---- Unassigned (use these to test assigning leads)
        new("Kagiso",   "Tau",       "Founder",                -1, 3, LeadSource.Website,  LeadStage.Lead,           0, 0, null, "Johannesburg",  "Gauteng",       "Start-ups"),
        new("Alicia",   "Roux",      "Events Coordinator",      7, 3, LeadSource.Website,  LeadStage.Lead,       12000, 0, null, "Pietermaritzburg","KwaZulu-Natal","Hospitality"),
        new("Vusi",     "Maphumulo", "Chief Operating Officer", 1, 3, LeadSource.Website,  LeadStage.Lead,           0, 0, null, "Bloemfontein",  "Free State",    "Transport & Logistics"),
    };

    private static readonly HashSet<string> OptedOutNames = new() { "Craig Naidoo", "Tshepo Maseko" };

    private static readonly string[] NoteTexts =
    {
        "Met at the event and was keen on the leadership programme for their senior team.",
        "Requested a proposal for about 20 delegates. Budget approval is due at month end.",
        "Asked for references from similar-sized organisations. Sent two case studies.",
        "Decision-maker is the MD. Prefers WhatsApp over email.",
        "Worried about timing around year-end. Revisit next quarter.",
        "Very positive call. Next step is a short presentation to their leadership team.",
        "Compared us with another provider; price is the main sticking point.",
        "Introduced by an existing client. Warm lead, move quickly."
    };

    private static readonly string[] CallTexts =
    {
        "Intro call, confirmed budget holder and timeline.",
        "Walked through the programme outline; they will discuss it internally.",
        "Left a voicemail and followed up with an email.",
        "Discussed pricing options and delegate numbers.",
        "Answered questions about venue and dates."
    };

    public static async Task SeedAsync(AppDbContext db, PasswordService passwords, ILogger log)
    {
        // Only ever fill an empty CRM. Real data is never touched and no demo accounts are added to it.
        if (await db.Leads.AnyAsync() || await db.Companies.AnyAsync() || await db.Events.AnyAsync())
        {
            log.LogInformation("Seed:DemoData is on but the CRM already contains data, so demo data was not loaded.");
            return;
        }

        // ---------------------------------------------------------------- accounts (created if missing, so a database from an earlier build works)
        var now = DateTime.UtcNow;
        var today = Clock.LocalToday;

        var admin   = await EnsureUserAsync(db, passwords, "admin@uncoveringgreatness.co.za",   "System Administrator", UserRole.Admin,    "CRM Administrator",       "+27 11 555 0100");
        var michaela = await EnsureUserAsync(db, passwords, "michaela@uncoveringgreatness.co.za","Michaela",           UserRole.SalesRep, "Senior Sales Representative", "+27 82 555 0101");
        var sule    = await EnsureUserAsync(db, passwords, "sule@uncoveringgreatness.co.za",    "Sule",                UserRole.SalesRep, "Sales Representative",    "+27 82 555 0102");
        var barry   = await EnsureUserAsync(db, passwords, "barry@uncoveringgreatness.co.za",   "Barry",               UserRole.SalesRep, "Sales Representative",    "+27 82 555 0103");
        var thabo   = await EnsureUserAsync(db, passwords, "thabo@uncoveringgreatness.co.za",   "Thabo Mokoena",       UserRole.Staff,    "Operations Coordinator (read-only)", "+27 82 555 0104");
        var lerato  = await EnsureUserAsync(db, passwords, "lerato@uncoveringgreatness.co.za",  "Lerato Dlamini",      UserRole.SalesRep, "Junior Sales Rep (new hire)", "+27 82 555 0105",
            u => u.MustChangePassword = true);
        var pieter  = await EnsureUserAsync(db, passwords, "pieter@uncoveringgreatness.co.za",  "Pieter van Wyk",      UserRole.SalesRep, "Sales Representative (locked out)", "+27 82 555 0106",
            u => u.LockoutEndUtc = DateTime.UtcNow.AddDays(30));
        var nomsa   = await EnsureUserAsync(db, passwords, "nomsa@uncoveringgreatness.co.za",   "Nomsa Khumalo",       UserRole.SalesRep, "Former Sales Representative (deactivated)", "+27 82 555 0107",
            u => u.IsActive = false);

        admin.LastLoginUtc ??= now.AddHours(-3);
        michaela.LastLoginUtc ??= now.AddHours(-6);
        sule.LastLoginUtc ??= now.AddDays(-1);
        barry.LastLoginUtc ??= now.AddDays(-2);
        await db.SaveChangesAsync();

        var rep = new Dictionary<int, User> { [1] = michaela, [2] = sule, [3] = barry };
        var byKey = new Dictionary<string, User>
        {
            ["admin"] = admin, ["michaela"] = michaela, ["sule"] = sule, ["barry"] = barry, ["thabo"] = thabo
        };

        await using var tx = await db.Database.BeginTransactionAsync();
        var rnd = new Random(2026);

        // ---------------------------------------------------------------- companies
        var companies = new List<Company>();
        for (var i = 0; i < CompanyData.Length; i++)
        {
            var c = CompanyData[i];
            companies.Add(new Company
            {
                Name = c.Name, Industry = c.Industry, Size = c.Size, Website = "https://" + c.Domain, AddressLine = c.Street,
                City = c.City, Province = c.Prov, ContactPerson = c.Contact, ContactPhone = $"+27 11 555 {1000 + i * 37}",
                ContactEmail = "info@" + c.Domain, Notes = $"Demo company {i + 1}. Key account in {c.Industry}.",
                CreatedUtc = now.AddDays(-rnd.Next(60, 200))
            });
        }
        db.Companies.AddRange(companies);

        // ---------------------------------------------------------------- events (2 past, 1 recent, 2 upcoming)
        var events = new List<MarketingEvent>
        {
            new() { Name = "Leadership Summit 2025", Type = "Conference", StartDate = today.AddDays(-330), EndDate = today.AddDays(-329), Location = "Sandton Convention Centre, Johannesburg", Description = "Our flagship two-day leadership conference (previous edition)." },
            new() { Name = "Greatness Breakfast Series: Q2", Type = "Breakfast", StartDate = today.AddDays(-75), Location = "Century City, Cape Town", Description = "Invitation-only networking breakfast for senior leaders." },
            new() { Name = "Women in Leadership Webinar", Type = "Webinar", StartDate = today.AddDays(-20), Location = "Online (Zoom)", Description = "A 90-minute live webinar with Q&A." },
            new() { Name = "Leadership Summit 2026", Type = "Conference", StartDate = today.AddDays(45), EndDate = today.AddDays(46), Location = "Sandton Convention Centre, Johannesburg", Description = "Two days of keynotes, workshops and networking. Early-bird registration is open." },
            new() { Name = "Executive Retreat: Magaliesberg", Type = "Retreat", StartDate = today.AddDays(90), EndDate = today.AddDays(92), Location = "Magaliesberg, North West", Description = "A three-day residential retreat for executive teams." },
        };
        foreach (var e in events) { e.Year = e.StartDate.Year; e.CreatedUtc = now.AddDays(-200); }
        db.Events.AddRange(events);
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------- leads
        var leads = new List<Lead>();
        var perRep = new Dictionary<int, List<Lead>> { [0] = new(), [1] = new(), [2] = new(), [3] = new() };
        foreach (var s in LeadData)
        {
            var domain = s.Co >= 0 ? CompanyData[s.Co].Domain : "independent.example";
            var local = (s.First + "." + s.Sur).ToLowerInvariant().Replace(" ", "");
            var created = now.AddDays(-rnd.Next(5, 120)).AddHours(-rnd.Next(0, 20));
            var closed = s.Stage is LeadStage.ClosedWon or LeadStage.ClosedLost;
            var tags = new List<string>();
            if (s.Src == LeadSource.WebForm) tags.Add("web-form");
            if (s.Ev == 3) tags.Add("summit-2026");
            if (s.Value >= 150000) tags.Add("enterprise");
            var lead = new Lead
            {
                FirstName = s.First, Surname = s.Sur, JobTitle = s.Title, Industry = s.Industry,
                Email = AuthService.NormalizeEmail($"{local}@{domain}"),
                Phone = $"+27 8{rnd.Next(1, 5)} 555 {rnd.Next(1000, 9999)}",
                City = s.City, Province = s.Prov,
                LinkedInUrl = $"https://www.linkedin.com/in/{local.Replace('.', '-')}",
                PreferredContact = new[] { "Email", "Phone", "WhatsApp" }[rnd.Next(3)],
                CompanyId = s.Co >= 0 ? companies[s.Co].Id : null,
                EventId = s.Ev >= 0 ? events[s.Ev].Id : null,
                RegistrationDate = Clock.ToLocal(created).Date,
                Source = s.Src, Stage = s.Stage, DealValue = s.Value,
                AssignedToId = s.Rep == 0 ? null : rep[s.Rep].Id,
                NextFollowUp = s.Follow is int f ? today.AddDays(f) : null,
                LastContactedUtc = s.Stage >= LeadStage.Qualified ? now.AddDays(-rnd.Next(1, 14)) : null,
                Tags = tags.Count == 0 ? null : string.Join(", ", tags),
                OptedOut = OptedOutNames.Contains(s.First + " " + s.Sur),
                CreatedById = s.Rep == 0 ? admin.Id : rep[s.Rep].Id,
                CreatedUtc = created, UpdatedUtc = created.AddDays(rnd.Next(0, 4))
            };
            if (closed)
            {
                var end = created.AddDays(rnd.Next(7, 40));
                lead.ClosedUtc = end > now ? now.AddDays(-1) : end;
                lead.UpdatedUtc = lead.ClosedUtc.Value;
            }
            leads.Add(lead);
            perRep[s.Rep].Add(lead);
        }
        db.Leads.AddRange(leads);
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------- activity, notes, stars
        var activities = new List<LeadActivity>();
        var notes = new List<LeadNote>();
        for (var i = 0; i < leads.Count; i++)
        {
            var l = leads[i];
            var ownerId = l.AssignedToId ?? admin.Id;
            var ownerName = l.AssignedToId is int oid ? byKey.Values.First(u => u.Id == oid).FullName : admin.FullName;
            activities.Add(new LeadActivity
            {
                LeadId = l.Id, UserId = l.CreatedById, Type = ActivityType.Created, Title = "Lead created",
                Detail = $"Source: {l.Source.Label()}", CreatedUtc = l.CreatedUtc
            });
            if (l.Stage != LeadStage.Lead)
            {
                activities.Add(new LeadActivity
                {
                    LeadId = l.Id, UserId = ownerId, Type = ActivityType.StageChanged, Title = "Stage changed",
                    Detail = $"{LeadStage.Lead.Label()} → {l.Stage.Label()}, updated by {ownerName}",
                    CreatedUtc = l.CreatedUtc.AddDays(2)
                });
                notes.Add(new LeadNote
                {
                    LeadId = l.Id, AuthorId = ownerId, Body = NoteTexts[i % NoteTexts.Length], CreatedUtc = l.CreatedUtc.AddDays(2).AddHours(1)
                });
                activities.Add(new LeadActivity
                {
                    LeadId = l.Id, UserId = ownerId, Type = ActivityType.NoteAdded, Title = "Note added",
                    CreatedUtc = l.CreatedUtc.AddDays(2).AddHours(1)
                });
            }
            if (i % 3 == 0 && l.AssignedToId != null)
            {
                activities.Add(new LeadActivity
                {
                    LeadId = l.Id, UserId = ownerId, Type = ActivityType.CallLogged, Title = "Call logged",
                    Detail = $"{5 + (i % 4) * 5}-minute call: {CallTexts[i % CallTexts.Length]}", CreatedUtc = l.CreatedUtc.AddDays(3)
                });
            }
            if (l.AssignedToId != null && l.CreatedById != l.AssignedToId)
            {
                activities.Add(new LeadActivity
                {
                    LeadId = l.Id, UserId = admin.Id, Type = ActivityType.Assigned, Title = "Assigned",
                    Detail = $"Lead assigned to {ownerName}", CreatedUtc = l.CreatedUtc.AddHours(1)
                });
            }
        }
        db.LeadActivities.AddRange(activities);
        db.LeadNotes.AddRange(notes);

        var stars = new HashSet<(int LeadId, int UserId)>();
        foreach (var r in new[] { 1, 2, 3 })
        {
            foreach (var slot in new[] { 0, 3, 4 }) stars.Add((perRep[r][slot].Id, rep[r].Id));
            stars.Add((perRep[r][4].Id, admin.Id));
        }
        stars.Add((perRep[2][7].Id, michaela.Id)); // a rep can also star someone else's lead
        db.LeadStars.AddRange(stars.Select(x => new LeadStar { LeadId = x.LeadId, UserId = x.UserId }));
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------- tasks (per user)
        var taskSets = new Dictionary<int, TaskSeed[]>
        {
            [0] = new TaskSeed[]
            {
                new("Review team pipeline before Monday stand-up", "Check stage movement and overdue follow-ups for each rep.", 1, TaskPriority.High, false, null),
                new("Approve Summit 2026 early-bird campaign draft", null, 2, TaskPriority.High, false, null),
                new("Check form integration submissions", "Make sure web-form leads are being assigned.", -1, TaskPriority.Medium, false, null),
                new("Audit user accounts and roles", "Deactivate anyone who has left.", 14, TaskPriority.Medium, false, null),
                new("Archive closed-lost leads from last year", null, null, TaskPriority.Low, false, null),
                new("Set up form integration for Summit registrations", null, -5, TaskPriority.Medium, true, null),
            },
            [1] = new TaskSeed[]
            {
                new("Call Thandiwe Nkosi to confirm summit attendance", null, 0, TaskPriority.High, false, 0),
                new("Prepare Baobab Financial proposal", "Include the 20-delegate package and optional workshop.", 2, TaskPriority.High, false, 3),
                new("Follow up on Table Bay negotiation terms", null, 1, TaskPriority.Medium, false, 4),
                new("Reply to Lwazi Mthembu's pricing question", null, -2, TaskPriority.High, false, 5),
                new("Update CRM notes from last week's calls", null, 5, TaskPriority.Low, false, null),
                new("Send thank-you note to Summit Peak (deal won)", null, -3, TaskPriority.Medium, true, 1),
            },
            [2] = new TaskSeed[]
            {
                new("Negotiation call with the Cradle fundraising lead", null, 0, TaskPriority.High, false, 5),
                new("Send healthcare case studies to Dr Moloi", null, 1, TaskPriority.Medium, false, 0),
                new("Book discovery call with Cradle Education Trust", null, 3, TaskPriority.Medium, false, 3),
                new("Chase Drakensberg Hospitality proposal", "Overdue: they asked for revised pricing.", -3, TaskPriority.High, false, 7),
                new("Prepare webinar follow-up list", null, 7, TaskPriority.Low, false, null),
                new("Log notes from principal meeting", null, -4, TaskPriority.Low, true, 4),
            },
            [3] = new TaskSeed[]
            {
                new("Send revised quote to Karoo Logistics", null, 1, TaskPriority.High, false, 2),
                new("Draft renewable energy proposal", "Cover solar maintenance training for the Upington team.", 2, TaskPriority.High, false, 8),
                new("Schedule site visit: Highveld Mining", null, 4, TaskPriority.Medium, false, 0),
                new("Re-engage Protea Retail store operations", null, -4, TaskPriority.High, false, 4),
                new("Update pipeline forecast", null, 6, TaskPriority.Low, false, null),
                new("Hand over onboarding documents to Karoo Logistics", null, -2, TaskPriority.Medium, true, 3),
            },
        };
        var ownersForTasks = new Dictionary<int, User> { [0] = admin, [1] = michaela, [2] = sule, [3] = barry };
        foreach (var (key, set) in taskSets)
        {
            var owner = ownersForTasks[key];
            foreach (var t in set)
            {
                var lead = t.Slot is int slot && key != 0 ? perRep[key][slot] : null;
                db.Tasks.Add(new CrmTask
                {
                    Title = t.Title, Description = t.Desc, DueAt = t.Due is int d ? today.AddDays(d).AddHours(17) : null,
                    Priority = t.Pri, IsCompleted = t.Done, CompletedUtc = t.Done ? now.AddDays(-1) : null,
                    AssignedToId = owner.Id, LeadId = lead?.Id, CreatedById = key == 0 ? admin.Id : owner.Id,
                    CreatedUtc = now.AddDays(-7)
                });
            }
        }
        // an admin-assigned task so reps see work delegated to them
        db.Tasks.Add(new CrmTask
        {
            Title = "Prepare your top-5 deals for the weekly review", Description = "Assigned by the admin. Bring stage and next step for each.",
            DueAt = today.AddDays(1).AddHours(9), Priority = TaskPriority.Medium, AssignedToId = sule.Id, CreatedById = admin.Id, CreatedUtc = now.AddDays(-1)
        });

        // ---------------------------------------------------------------- calendar (per user)
        var calSets = new Dictionary<int, CalSeed[]>
        {
            [0] = new CalSeed[]
            {
                new("Weekly pipeline review", 1, "09:00", CalendarItemType.Meeting, CalendarVisibility.Team, Array.Empty<string>(), "Whole sales team.", null),
                new("Summit 2026 planning", 3, "14:00", CalendarItemType.Meeting, CalendarVisibility.Shared, new[] { "michaela", "sule", "barry", "thabo" }, "Venue, speakers and early-bird deadline.", null),
                new("1:1 with Michaela", -2, "10:00", CalendarItemType.Meeting, CalendarVisibility.Shared, new[] { "michaela" }, null, null),
                new("Month-end reporting", 10, "16:00", CalendarItemType.Other, CalendarVisibility.Private, Array.Empty<string>(), "Private reminder.", null),
            },
            [1] = new CalSeed[]
            {
                new("Call: Thandiwe Nkosi", 0, "11:00", CalendarItemType.Call, CalendarVisibility.Private, Array.Empty<string>(), "Confirm summit attendance.", 0),
                new("Proposal presentation: Table Bay Technologies", 2, "13:30", CalendarItemType.Meeting, CalendarVisibility.Shared, new[] { "admin" }, "Bring the final pricing sheet.", 4),
                new("Follow-up: Sipho Dlamini", 5, "09:30", CalendarItemType.FollowUp, CalendarVisibility.Private, Array.Empty<string>(), null, 3),
                new("Networking breakfast", 8, "07:30", CalendarItemType.Other, CalendarVisibility.Team, Array.Empty<string>(), "Open to the whole team.", null),
            },
            [2] = new CalSeed[]
            {
                new("Discovery call: Cradle Education", 3, "10:00", CalendarItemType.Call, CalendarVisibility.Private, Array.Empty<string>(), null, 3),
                new("Lunch with Dr Moloi", 4, "12:30", CalendarItemType.Meeting, CalendarVisibility.Shared, new[] { "admin" }, null, 0),
                new("Healthcare webinar prep", 6, "15:00", CalendarItemType.Other, CalendarVisibility.Shared, new[] { "michaela", "barry" }, null, null),
                new("Follow-up: Naledi Baloyi", 1, "09:00", CalendarItemType.FollowUp, CalendarVisibility.Private, Array.Empty<string>(), "Overdue proposal.", 7),
            },
            [3] = new CalSeed[]
            {
                new("Site visit: Highveld Mining", 4, "08:00", CalendarItemType.Meeting, CalendarVisibility.Shared, new[] { "admin" }, "Travel to eMalahleni.", 0),
                new("Quote review with Karoo Logistics", 1, "14:00", CalendarItemType.Call, CalendarVisibility.Private, Array.Empty<string>(), null, 2),
                new("Retail trends talk", 9, "17:00", CalendarItemType.Other, CalendarVisibility.Team, Array.Empty<string>(), null, null),
                new("Follow-up: Elaine Cronje", 2, "11:00", CalendarItemType.FollowUp, CalendarVisibility.Private, Array.Empty<string>(), null, 8),
            },
        };
        foreach (var (key, set) in calSets)
        {
            var owner = ownersForTasks[key];
            foreach (var c in set)
            {
                var shares = c.Vis == CalendarVisibility.Shared
                    ? c.With.Select(k => new CalendarShare { UserId = byKey[k].Id }).Where(sh => sh.UserId != owner.Id).ToList()
                    : new List<CalendarShare>();
                db.CalendarEvents.Add(new CalendarEvent
                {
                    Title = c.Title, StartsAt = today.AddDays(c.Day) + TimeSpan.Parse(c.Time), Type = c.Type, Notes = c.Notes,
                    Visibility = c.Vis, OwnerId = owner.Id,
                    LinkedLeadId = c.Slot is int slot && key != 0 ? perRep[key][slot].Id : null,
                    Shares = shares, CreatedUtc = now.AddDays(-3)
                });
            }
        }
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------- notifications (per user, mixed read / unread)
        var topWon = perRep[1][8];
        var notifications = new List<Notification>();
        void Notify(User u, NotificationType type, string title, string? body, string? link, bool read, double hoursAgo) =>
            notifications.Add(new Notification
            {
                UserId = u.Id, Type = type, Title = title, Body = body, LinkUrl = link, IsRead = read,
                DedupeKey = $"demo-{notifications.Count + 1}", CreatedUtc = now.AddHours(-hoursAgo)
            });

        Notify(admin, NotificationType.DealWon, "Deal moved to Closed won", $"{topWon.FullName}: {UiHelpers.Money(topWon.DealValue)}", $"/Leads/Details/{topWon.Id}", false, 5);
        Notify(admin, NotificationType.DealWon, "Deal moved to Closed won", $"{perRep[3][3].FullName}: {UiHelpers.Money(perRep[3][3].DealValue)}", $"/Leads/Details/{perRep[3][3].Id}", true, 70);
        Notify(admin, NotificationType.System, "Welcome to the demo workspace", "Demo data was loaded so you can test every screen. Switch it off with Seed:DemoData.", "/", false, 1);

        Notify(michaela, NotificationType.LeadAssigned, "New lead assigned to you", $"{perRep[1][2].FullName} was assigned to you by {admin.FullName}.", $"/Leads/Details/{perRep[1][2].Id}", false, 2);
        Notify(michaela, NotificationType.FollowUp, "Follow-up overdue", $"{perRep[1][5].FullName} was due for a follow-up 2 days ago.", $"/Leads/Details/{perRep[1][5].Id}", false, 8);
        Notify(michaela, NotificationType.TaskDue, "Task overdue", "Reply to Lwazi Mthembu's pricing question", "/Tasks", false, 9);
        Notify(michaela, NotificationType.DealWon, "Deal moved to Closed won", $"{perRep[1][8].FullName}: {UiHelpers.Money(perRep[1][8].DealValue)}", $"/Leads/Details/{perRep[1][8].Id}", true, 96);

        Notify(sule, NotificationType.FollowUp, "Follow-up overdue", $"{perRep[2][7].FullName} was due for a follow-up 3 days ago.", $"/Leads/Details/{perRep[2][7].Id}", false, 6);
        Notify(sule, NotificationType.LeadAssigned, "New lead assigned to you", $"{perRep[2][3].FullName} was assigned to you by {admin.FullName}.", $"/Leads/Details/{perRep[2][3].Id}", true, 50);
        Notify(sule, NotificationType.TaskDue, "Task due tomorrow", "Prepare your top-5 deals for the weekly review", "/Tasks", false, 4);

        Notify(barry, NotificationType.FollowUp, "Follow-up overdue", $"{perRep[3][4].FullName} was due for a follow-up 4 days ago.", $"/Leads/Details/{perRep[3][4].Id}", false, 7);
        Notify(barry, NotificationType.DealWon, "Deal moved to Closed won", $"{perRep[3][6].FullName}: {UiHelpers.Money(perRep[3][6].DealValue)}", $"/Leads/Details/{perRep[3][6].Id}", true, 120);
        Notify(barry, NotificationType.System, "Calendar event shared with you", "System Administrator shared “Summit 2026 planning”.", "/Calendar", false, 12);

        Notify(thabo, NotificationType.System, "Calendar event shared with you", "System Administrator shared “Summit 2026 planning”.", "/Calendar", false, 12);
        Notify(thabo, NotificationType.System, "Welcome", "You have read-only access to leads, companies, events and the pipeline.", "/", true, 72);
        db.Notifications.AddRange(notifications);

        // ---------------------------------------------------------------- campaigns
        var sent = new Campaign
        {
            Name = "Welcome to the Uncovering Greatness community",
            Subject = "Welcome, {{FirstName}}: here's what is coming up",
            Body = "Hi {{FirstName}},\n\nThank you for connecting with Uncovering Greatness. Our next flagship event is the Leadership Summit 2026, and we would love to see {{Company}} there.\n\nKind regards,\nThe Uncovering Greatness team",
            Target = CampaignTarget.All, Status = CampaignStatus.Sent, CreatedById = admin.Id,
            CreatedUtc = now.AddDays(-14), SentUtc = now.AddDays(-12),
            RecipientCount = 12, DeliveredCount = 11, FailedCount = 1, OpenedCount = 6
        };
        var draftEvent = new Campaign
        {
            Name = "Summit 2026: early-bird invitation",
            Subject = "{{FirstName}}, early-bird tickets for Leadership Summit 2026",
            Body = "Hi {{FirstName}},\n\nEarly-bird registration for the Leadership Summit 2026 is open for a limited time. Secure places for your team at {{Company}} today.\n\nBest wishes,\nThe Uncovering Greatness team",
            Target = CampaignTarget.Event, TargetEventId = events[3].Id, Status = CampaignStatus.Draft, CreatedById = admin.Id, CreatedUtc = now.AddDays(-2)
        };
        var draftYear = new Campaign
        {
            Name = "This year's registrants: thank you and what's next",
            Subject = "Thank you, {{FirstName}}",
            Body = "Hi {{FirstName}},\n\nThank you for registering this year. Here is what you can look forward to next.\n\nRegards,\nThe Uncovering Greatness team",
            Target = CampaignTarget.Year, TargetYear = today.Year, Status = CampaignStatus.Draft, CreatedById = admin.Id, CreatedUtc = now.AddDays(-1)
        };
        db.Campaigns.AddRange(sent, draftEvent, draftYear);
        await db.SaveChangesAsync();

        var audience = leads.Where(l => !l.OptedOut).Take(12).ToList();
        for (var i = 0; i < audience.Count; i++)
        {
            var failed = i == audience.Count - 1;
            db.CampaignRecipients.Add(new CampaignRecipient
            {
                CampaignId = sent.Id, LeadId = audience[i].Id,
                Status = failed ? RecipientStatus.Failed : RecipientStatus.Delivered,
                Error = failed ? "Mailbox unavailable (demo)" : null,
                SentUtc = sent.SentUtc, OpenedUtc = i < 6 ? sent.SentUtc!.Value.AddHours(2 + i) : null
            });
        }

        // ---------------------------------------------------------------- form integration with sample submissions
        var conn = new FormConnection
        {
            Name = "Summit 2026 registration (Tally)", Provider = FormProvider.Tally, Token = FormSecrets.NewToken(), IsActive = true,
            EventId = events[3].Id, Assignment = AssignmentMode.RoundRobin, LastAssignedUserId = barry.Id, DefaultTags = "web-form, summit-2026",
            TotalReceived = 5, TotalCreated = 3, TotalDuplicate = 1, TotalFailed = 1, LastReceivedUtc = now.AddDays(-2),
            CreatedById = admin.Id, CreatedUtc = now.AddDays(-30)
        };
        db.FormConnections.Add(conn);
        await db.SaveChangesAsync();

        var web = leads.Where(l => l.Source == LeadSource.WebForm).ToList();
        for (var i = 0; i < web.Count; i++)
        {
            db.FormSubmissions.Add(new FormSubmission
            {
                ConnectionId = conn.Id, ExternalId = $"demo-response-{i + 1:000}", ReceivedUtc = now.AddDays(-10 + i), Status = SubmissionStatus.Created,
                LeadId = web[i].Id, Summary = $"{web[i].FullName} <{web[i].Email}>", IpAddress = $"203.0.113.{10 + i}"
            });
        }
        db.FormSubmissions.Add(new FormSubmission
        {
            ConnectionId = conn.Id, ExternalId = "demo-response-004", ReceivedUtc = now.AddDays(-2), Status = SubmissionStatus.Duplicate,
            LeadId = web[0].Id, Summary = $"{web[0].FullName} <{web[0].Email}>", IpAddress = "203.0.113.50"
        });
        db.FormSubmissions.Add(new FormSubmission
        {
            ConnectionId = conn.Id, ExternalId = "demo-response-005", ReceivedUtc = now.AddDays(-3), Status = SubmissionStatus.Rejected,
            Error = "No valid email address in the submission.", Summary = "Unknown <no email>", IpAddress = "203.0.113.77"
        });

        // ---------------------------------------------------------------- audit trail
        void Audit(User? u, string action, string? entity, object? id, string? details, double hoursAgo, string ip = "127.0.0.1") =>
            db.AuditLogs.Add(new AuditLog
            {
                UserId = u?.Id, UserName = u?.FullName, Action = action, Entity = entity, EntityId = id?.ToString(),
                Details = details, IpAddress = ip, CreatedUtc = now.AddHours(-hoursAgo)
            });
        Audit(admin, "LoginSuccess", "User", admin.Id, null, 3);
        Audit(michaela, "LoginSuccess", "User", michaela.Id, null, 6);
        Audit(sule, "LoginSuccess", "User", sule.Id, null, 26);
        Audit(barry, "LoginSuccess", "User", barry.Id, null, 50);
        Audit(admin, "UserCreated", "User", lerato.Id, lerato.Email, 90);
        Audit(admin, "UserUpdated", "User", nomsa.Id, "Deactivated (left the company)", 80);
        Audit(admin, "LeadCreated", "Lead", perRep[1][0].Id, perRep[1][0].FullName, 100);
        Audit(michaela, "LeadStageChanged", "Lead", perRep[1][8].Id, "Negotiation -> ClosedWon", 96);
        Audit(admin, "CampaignSent", "Campaign", sent.Id, sent.Name, 288);
        Audit(null, "LoginFailed", "User", null, "Unknown account 'intruder@example.com'", 30, "203.0.113.9");
        for (var i = 0; i < 5; i++) Audit(pieter, "LoginFailed", "User", pieter.Id, "Wrong password", 20 - i * 0.1, "203.0.113.20");
        Audit(pieter, "LoginBlocked", "User", pieter.Id, "Account temporarily locked", 19, "203.0.113.20");
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        log.LogWarning(
            "DEMO DATA LOADED: {Leads} leads, {Companies} companies, {Events} events, tasks, calendar entries, campaigns and a form connection. " +
            "Demo accounts all use password {Password}. Set Seed:DemoData to false before going live.",
            leads.Count, companies.Count, events.Count, DemoPassword);
    }

    private static async Task<User> EnsureUserAsync(AppDbContext db, PasswordService passwords, string email, string name, UserRole role,
        string title, string phone, Action<User>? configure = null)
    {
        email = AuthService.NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            user = new User { FullName = name, Email = email, Role = role, JobTitle = title, Phone = phone };
            user.PasswordHash = passwords.Hash(user, DemoPassword);
            configure?.Invoke(user);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        else
        {
            user.JobTitle ??= title;
            user.Phone ??= phone;
        }
        return user;
    }
}
