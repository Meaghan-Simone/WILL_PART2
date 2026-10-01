<#
  API smoke test for Uncovering Greatness CRM.
  Run the app first (dotnet run) with Seed:DemoData = true on a fresh database, then:

      powershell -ExecutionPolicy Bypass -File tests\api-smoke-test.ps1
      powershell -ExecutionPolicy Bypass -File tests\api-smoke-test.ps1 -BaseUrl http://localhost:5080

  It signs in as every demo account and checks sign-in, lockout, forced password change, role permissions,
  record-level rules, token revocation and the main CRUD endpoints. It cleans up what it creates and puts the
  password of the "new hire" account back, so it can be run repeatedly.
  Note: the sign-in endpoint is rate limited (10/min per IP); the script pauses when it needs to.
#>
param(
  [string]$BaseUrl = "http://localhost:5080",
  [string]$Password = "P@ssword4321"
)
$ErrorActionPreference = "Stop"
$script:pass = 0
$script:fail = 0
$domain = "uncoveringgreatness.co.za"

function Call {
  param([string]$Method, [string]$Path, $Body = $null, [string]$Token = "", [int]$Expect = 200, [string]$Name = "")
  $headers = @{}
  if ($Token) { $headers["Authorization"] = "Bearer $Token" }
  $req = @{ Uri = "$BaseUrl$Path"; Method = $Method; Headers = $headers; UseBasicParsing = $true }
  if ($null -ne $Body) { $req.Body = ($Body | ConvertTo-Json -Depth 6); $req.ContentType = "application/json" }
  $status = 0; $content = ""
  try {
    $r = Invoke-WebRequest @req
    $status = [int]$r.StatusCode; $content = $r.Content
  } catch {
    if ($_.Exception.Response) {
      $status = [int]$_.Exception.Response.StatusCode
      if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $content = $_.ErrorDetails.Message }
      else { try { $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream()); $content = $sr.ReadToEnd() } catch { } }
    } else { throw }
  }
  $json = $null
  if ($content) { try { $json = $content | ConvertFrom-Json } catch { } }
  if (-not $Name) { $Name = "$Method $Path" }
  if ($status -eq $Expect) { $script:pass++; Write-Host ("  PASS  {0}  [{1}]" -f $Name, $status) -ForegroundColor Green }
  else { $script:fail++; Write-Host ("  FAIL  {0}  expected {1}, got {2}  {3}" -f $Name, $Expect, $status, $content) -ForegroundColor Red }
  return [pscustomobject]@{ Status = $status; Json = $json }
}

function Login([string]$user, [string]$pw = $Password, [int]$Expect = 200) {
  $r = Call POST "/api/v1/auth/token" @{ email = "$user@$domain"; password = $pw } -Expect $Expect -Name "Sign in as $user"
  if ($r.Json -and $r.Json.accessToken) { return $r.Json.accessToken }
  return ""
}

Write-Host "`n== Public / anonymous ==" -ForegroundColor Cyan
Call GET "/api/v1/health" -Name "Health check" | Out-Null
Call GET "/api/v1/leads" -Expect 401 -Name "Leads without a token is rejected" | Out-Null
Call GET "/api/v1/leads" -Token "not-a-real-token" -Expect 401 -Name "Garbage token is rejected" | Out-Null
Call POST "/api/v1/auth/token" @{ email = "admin@$domain"; password = "wrong" } -Expect 401 -Name "Wrong password is rejected" | Out-Null
Call POST "/api/v1/auth/forgot-password" @{ email = "nobody@$domain" } -Expect 202 -Name "Forgot password never reveals whether the account exists" | Out-Null

Write-Host "`n== Account states ==" -ForegroundColor Cyan
Login "nomsa" -Expect 401 | Out-Null          # deactivated
Login "pieter" -Expect 423 | Out-Null         # locked out
Start-Sleep -Seconds 1
$admin = Login "admin"
$michaela = Login "michaela"
$sule = Login "sule"
$barry = Login "barry"
$thabo = Login "thabo"
Write-Host "  (pausing 65s so the sign-in rate limit window resets)" -ForegroundColor DarkGray
Start-Sleep -Seconds 65

Write-Host "`n== Forced password change (new hire) ==" -ForegroundColor Cyan
$leratoLogin = Call POST "/api/v1/auth/token" @{ email = "lerato@$domain"; password = $Password } -Name "Sign in as lerato"
$lerato = $leratoLogin.Json.accessToken
if ($leratoLogin.Json.mustChangePassword -eq $true) { $script:pass++; Write-Host "  PASS  token response says mustChangePassword" -ForegroundColor Green } else { $script:fail++; Write-Host "  FAIL  mustChangePassword flag missing" -ForegroundColor Red }
Call GET "/api/v1/leads" -Token $lerato -Expect 403 -Name "Blocked until password is changed" | Out-Null
Call GET "/api/v1/auth/me" -Token $lerato -Name "Can still read own profile" | Out-Null
Call POST "/api/v1/auth/change-password" @{ currentPassword = $Password; newPassword = "short" } -Token $lerato -Expect 400 -Name "Weak new password is rejected" | Out-Null
$changed = Call POST "/api/v1/auth/change-password" @{ currentPassword = $Password; newPassword = "Zebra#Mountain2026" } -Token $lerato -Name "Change password"
Call GET "/api/v1/leads" -Token $lerato -Expect 401 -Name "Old token stops working after password change" | Out-Null
$lerato2 = $changed.Json.accessToken
Call GET "/api/v1/leads?pageSize=5" -Token $lerato2 -Name "New token works" | Out-Null

Write-Host "`n== Roles ==" -ForegroundColor Cyan
Call GET "/api/v1/auth/me" -Token $admin -Name "Admin profile" | Out-Null
$users = Call GET "/api/v1/users" -Token $admin -Name "Admin lists users"
Call GET "/api/v1/users" -Token $michaela -Expect 403 -Name "Sales rep cannot list users" | Out-Null
Call GET "/api/v1/audit" -Token $michaela -Expect 403 -Name "Sales rep cannot read the audit log" | Out-Null
Call GET "/api/v1/campaigns" -Token $barry -Expect 403 -Name "Sales rep cannot open campaigns" | Out-Null
Call GET "/api/v1/audit" -Token $admin -Name "Admin reads the audit log" | Out-Null
Call GET "/api/v1/leads?pageSize=5" -Token $thabo -Name "Staff can read leads" | Out-Null
Call POST "/api/v1/leads" @{ firstName = "No"; surname = "Access"; email = "noaccess@example.com" } -Token $thabo -Expect 403 -Name "Staff cannot create leads" | Out-Null

Write-Host "`n== Leads (Michaela) ==" -ForegroundColor Cyan
$mine = Call GET "/api/v1/leads?view=mine&pageSize=50" -Token $michaela -Name "My leads"
Write-Host ("        Michaela has {0} leads" -f $mine.Json.totalCount) -ForegroundColor DarkGray
Call GET "/api/v1/leads?stage=Qualified" -Token $michaela -Name "Filter by stage" | Out-Null
Call GET "/api/v1/leads?view=followup" -Token $michaela -Name "Follow-up view" | Out-Null
Call GET "/api/v1/leads/pipeline" -Token $michaela -Name "Pipeline" | Out-Null
$email = "smoke." + [guid]::NewGuid().ToString("N").Substring(0, 8) + "@example.com"
$new = Call POST "/api/v1/leads" @{ firstName = "Smoke"; surname = "Test"; email = $email; source = "Website"; stage = "Lead"; dealValue = 12345 } -Token $michaela -Expect 201 -Name "Create lead"
$id = $new.Json.id
Call POST "/api/v1/leads" @{ firstName = "Smoke"; surname = "Dupe"; email = $email } -Token $michaela -Expect 409 -Name "Duplicate email is rejected" | Out-Null
Call GET "/api/v1/leads/$id" -Token $michaela -Name "Get lead" | Out-Null
Call PUT "/api/v1/leads/$id" @{ firstName = "Smoke"; surname = "Updated"; email = $email; source = "Referral"; stage = "Lead"; dealValue = 20000 } -Token $michaela -Expect 204 -Name "Update lead" | Out-Null
Call PATCH "/api/v1/leads/$id/stage" @{ stage = "Qualified" } -Token $michaela -Expect 204 -Name "Move stage" | Out-Null
Call POST "/api/v1/leads/$id/notes" @{ body = "Smoke test note" } -Token $michaela -Name "Add note" | Out-Null
Call POST "/api/v1/leads/$id/calls" @{ durationMinutes = 5; summary = "Smoke test call" } -Token $michaela -Expect 204 -Name "Log call" | Out-Null
Call POST "/api/v1/leads/$id/star" -Token $michaela -Name "Star lead" | Out-Null
Call GET "/api/v1/leads/export" -Token $michaela -Name "Export CSV" | Out-Null
Write-Host "`n== Record-level rules ==" -ForegroundColor Cyan
$others = Call GET "/api/v1/leads?view=mine&pageSize=5" -Token $sule -Name "Sule's leads"
if ($others.Json.items.Count -gt 0) {
  $otherId = $others.Json.items[0].id
  # Another rep's lead is not visible at all: reads, moves and notes all come back "not found".
  Call GET "/api/v1/leads/$otherId" -Token $michaela -Expect 404 -Name "Rep cannot open another rep's lead" | Out-Null
  Call PATCH "/api/v1/leads/$otherId/stage" @{ stage = "Qualified" } -Token $michaela -Expect 404 -Name "Rep cannot move another rep's lead" | Out-Null
  Call POST "/api/v1/leads/$otherId/notes" @{ body = "x" } -Token $michaela -Expect 404 -Name "Rep cannot note another rep's lead" | Out-Null
  Call GET "/api/v1/leads/$otherId" -Token $admin -Name "Admin can open any lead" | Out-Null
  $michaelaAll = Call GET "/api/v1/leads?pageSize=200" -Token $michaela -Name "Rep's full lead list"
  if ($michaelaAll.Json.items | Where-Object { $_.id -eq $otherId }) { Write-Host "  FAIL  Rep's list contains another rep's lead" -ForegroundColor Red; $script:fail++ }
}
Call DELETE "/api/v1/leads/$id" -Token $michaela -Expect 403 -Name "Rep cannot delete leads" | Out-Null
Call DELETE "/api/v1/leads/$id" -Token $admin -Expect 204 -Name "Admin deletes the test lead" | Out-Null
Call GET "/api/v1/leads/$id" -Token $admin -Expect 404 -Name "Deleted lead is gone" | Out-Null

Write-Host "`n== Companies, events, tasks, calendar (Barry) ==" -ForegroundColor Cyan
Call GET "/api/v1/companies" -Token $barry -Name "List companies" | Out-Null
$co = Call POST "/api/v1/companies" @{ name = "Smoke Test Co " + [guid]::NewGuid().ToString("N").Substring(0, 6) } -Token $barry -Expect 201 -Name "Create company"
Call DELETE "/api/v1/companies/$($co.Json.id)" -Token $barry -Expect 403 -Name "Rep cannot delete companies" | Out-Null
Call DELETE "/api/v1/companies/$($co.Json.id)" -Token $admin -Expect 204 -Name "Admin deletes the test company" | Out-Null
Call GET "/api/v1/events" -Token $barry -Name "List events" | Out-Null
Call POST "/api/v1/events" @{ name = "x"; type = "Webinar"; year = 2027; startDate = "2027-01-15" } -Token $barry -Expect 403 -Name "Rep cannot create events" | Out-Null
Call GET "/api/v1/tasks" -Token $barry -Name "Task board" | Out-Null
$due = (Get-Date).AddDays(2).ToString("yyyy-MM-ddT17:00:00")
$task = Call POST "/api/v1/tasks" @{ title = "Smoke test task"; dueAt = $due; priority = "High" } -Token $barry -Expect 201 -Name "Create task"
Call PATCH "/api/v1/tasks/$($task.Json.id)/complete" @{ completed = $true } -Token $barry -Expect 200 -Name "Complete task" | Out-Null
Call DELETE "/api/v1/tasks/$($task.Json.id)" -Token $barry -Expect 204 -Name "Delete task" | Out-Null
$day = (Get-Date).AddDays(3).ToString("yyyy-MM-dd")
$cal = Call POST "/api/v1/calendar" @{ title = "Smoke test meeting"; date = $day; time = "10:30:00"; type = "Meeting"; visibility = "Private" } -Token $barry -Expect 201 -Name "Create calendar entry"
Call GET "/api/v1/calendar" -Token $barry -Name "Calendar month" | Out-Null
Call GET "/api/v1/calendar/$($cal.Json.id)" -Token $michaela -Expect 404 -Name "Private entry is invisible to others" | Out-Null
Call DELETE "/api/v1/calendar/$($cal.Json.id)" -Token $barry -Expect 204 -Name "Delete calendar entry" | Out-Null

Write-Host "`n== Dashboard, search, notifications ==" -ForegroundColor Cyan
Call GET "/api/v1/dashboard" -Token $sule -Name "Dashboard" | Out-Null
Call GET "/api/v1/search?q=Summit" -Token $sule -Name "Search" | Out-Null
Call GET "/api/v1/team" -Token $sule -Name "Team list" | Out-Null
Call GET "/api/v1/notifications?filter=unread" -Token $sule -Name "Unread notifications" | Out-Null
Call POST "/api/v1/notifications/read-all" -Token $sule -Name "Mark all read" | Out-Null

Write-Host "`n== Admin areas ==" -ForegroundColor Cyan
Call GET "/api/v1/campaigns" -Token $admin -Name "Campaigns" | Out-Null
Call GET "/api/v1/campaigns/audience?target=All" -Token $admin -Name "Campaign audience size" | Out-Null
Call GET "/api/v1/integrations/forms" -Token $admin -Name "Form integrations" | Out-Null

Write-Host "`n== Tokens: refresh and revoke ==" -ForegroundColor Cyan
$refreshed = Call POST "/api/v1/auth/refresh" -Token $barry -Name "Refresh token"
Call POST "/api/v1/auth/revoke" -Token $refreshed.Json.accessToken -Expect 204 -Name "Sign out everywhere" | Out-Null
Call GET "/api/v1/auth/me" -Token $barry -Expect 401 -Name "Original token no longer valid" | Out-Null
Call GET "/api/v1/auth/me" -Token $refreshed.Json.accessToken -Expect 401 -Name "Refreshed token no longer valid" | Out-Null

Write-Host "`n== Admin revokes someone else's sessions, then cleanup ==" -ForegroundColor Cyan
$suleId = ($users.Json | Where-Object { $_.email -eq "sule@$domain" }).id
Call POST "/api/v1/users/$suleId/revoke-sessions" -Token $admin -Expect 204 -Name "Admin revokes Sule's sessions" | Out-Null
Call GET "/api/v1/auth/me" -Token $sule -Expect 401 -Name "Sule's token is dead" | Out-Null
$leratoId = ($users.Json | Where-Object { $_.email -eq "lerato@$domain" }).id
Call POST "/api/v1/users/$leratoId/reset-password" @{ temporaryPassword = $Password } -Token $admin -Expect 204 -Name "Reset new hire back to the demo password" | Out-Null

Write-Host ("`nDone: {0} passed, {1} failed" -f $script:pass, $script:fail) -ForegroundColor $(if ($script:fail -eq 0) { "Green" } else { "Red" })
if ($script:fail -gt 0) { exit 1 }
