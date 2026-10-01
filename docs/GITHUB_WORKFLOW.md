# GitHub workflow and pipelines (9.4.4)

## Branches

| Branch | Purpose | Deploys to |
|---|---|---|
| `main` | Always releasable. Only changed by pull request from `develop` (release) or `hotfix/*`. | **Production** (your cPanel server) |
| `develop` | Integration branch. Every finished feature merges here first. | nothing, CI only |
| `feature/<short-name>` | One feature or task, branched from `develop` (e.g. `feature/pipeline-keyboard-move`). | nothing, CI only |
| `bugfix/<short-name>` | Fix found on `develop`. | nothing, CI only |
| `hotfix/<short-name>` | Urgent production fix, branched from `main`, merged to `main` **and** back to `develop`. | Production after merge |
| `release/<version>` | Optional stabilisation branch before merging to `main`. | nothing, CI only |

```
feature/x ─┐                      ┌─> main (production)
feature/y ─┼─PR─> develop ──PR────┘
bugfix/z  ─┘
```

Create the structure with `scripts/init-repo.ps1 -Remote <your GitHub URL>`.

## Day-to-day flow

```bash
git switch develop && git pull
git switch -c feature/my-change
# ...work, commit often: "feat: ...", "fix: ...", "docs: ...", "test: ..."
git push -u origin feature/my-change      # CI runs on the push
# open a Pull Request into develop; CI must be green; get a review; squash-merge
# when develop is ready to release: Pull Request develop -> main
```

## Pipelines (`.github/workflows`)

| Workflow | Trigger | What it does |
|---|---|---|
| `ci.yml` | Push to `develop`, `feature/**`, `bugfix/**`, `hotfix/**`, `release/**`; pull requests into `develop` or `main` | Restore, build in Release, run the xUnit tests, check packages for known vulnerabilities, upload the test report. |
| `deploy.yml` | Push (i.e. a merge) to `main` | Runs `ci.yml` first; if green, publishes the app, deploys it to the target chosen by the repository variable `DEPLOY_TARGET` (`cpanel`: copy over SSH and restart; `render`: trigger the Render deploy hook), then calls `/api/v1/health` to confirm the new version is live. A failed health check fails the run. An Azure variant is kept in `.github/workflows-optional/`. |

Deployment never happens when a test fails, because `deploy.yml` requires the CI job to pass.

## Repository settings to turn on (GitHub > Settings)

**Branches > Add branch protection rule** for `main` and `develop`:
- Require a pull request before merging (at least 1 approval)
- Require status checks to pass: select **Build and test**
- Require branches to be up to date before merging
- Block force pushes and deletions

**Environments**: `production` (optionally with required reviewers), with the secrets and variables listed in `docs/HOSTING.md` section 4.

## How this shows CI/CD practice

- **Branch management**: protected `main`/`develop`, short-lived feature branches, pull-request template (`.github/pull_request_template.md`) with a checklist.
- **Automated testing**: every push and PR runs the unit tests; a red build blocks merging.
- **Automated deployment**: merging to `main` publishes and deploys with no manual steps, followed by an automatic smoke test.
