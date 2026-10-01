# One-off: turns this folder into a Git repo with the main / develop / feature branch structure and pushes it.
#   .\scripts\init-repo.ps1 -Remote https://github.com/<you>/UncoveringGreatnessCRM.git
param([Parameter(Mandatory)][string]$Remote)
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
git init -b main
git add -A
git commit -m "chore: initial import of Uncovering Greatness CRM"
git remote add origin $Remote
git push -u origin main
git switch -c develop
git push -u origin develop
git switch -c feature/ci-cd-pipeline
git push -u origin feature/ci-cd-pipeline
Write-Host "Branches created: main, develop, feature/ci-cd-pipeline." -ForegroundColor Green
Write-Host "Next: set branch protection and Azure secrets (docs/GITHUB_WORKFLOW.md and docs/HOSTING.md)." -ForegroundColor Cyan
