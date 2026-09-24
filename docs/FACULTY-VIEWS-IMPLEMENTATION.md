# Faculty views implementation

## Progress

Branch: `srk/faculty-only-views`, started from `5b9e7ec` on 2026-09-23.

**All implementation and acceptance stages are complete.** Final acceptance passed against `dc00b12` on 2026-09-23. The remaining work is limited to the explicitly deferred scope below.

Work through one reviewable stage at a time. Check an item only after its implementation and listed validation pass. Record evidence below, including limitations. Intermediate stages may still use the existing views; the completed refactor must remove both obsolete views in one forward migration.

## Agreed scope

- Replace `dbo.vw_CurrentEmployee` with `dbo.vw_CurrentFacultyWithAccrual` and `dbo.vw_CurrentAccrualBalance` with `dbo.vw_CurrentFacultyAccrualBalance`.
- Both final views require `People.IsEmployee = 1`, `People.IsFaculty = 1`, and a matching accrual record. A NULL faculty flag does not qualify.
- Keep the source `IsFaculty` column in the person/department projection. Remove `HasCurrentAccrualRecord` from the final model and its consumers. `LeaveType.HasAccrualBalance` is unrelated and stays.
- Preserve each employee's latest stored accrual date, deterministic position selection, Level5 -> Level4 -> Level3 department fallback, effective reporting overrides, and `MAX(CalculatedBal)` position aggregation. Preserve leave-type grouping, counts, min/max balances, and divergence reporting.
- Preserve keyless EF mappings, `IamId` as `char(10)`, and decimal precision. Leave imported tables and staging/promotion infrastructure intact.
- Use employee entries in `People` for admin/CAO membership, identity, candidates, and cleanup. Accruals and an existing AppUser are not prerequisites for those assignments. Explicit assignments still determine access. Load identities by assignment IAM IDs; search candidates on demand with at least two characters and a SQL limit of 20 results. Never preload the employee directory for a picker or cleanup.
- Faculty who are also admins or CAOs remain in faculty rosters. CAO candidates may have any faculty status, including true or NULL. Require current employee membership and preserve the active-user and existing admin/CAO/chair exclusions in the picker; keep those rules separate from faculty roster membership.
- Keep authentication and the existing role-cleanup schedule unchanged. Update cleanup eligibility sources, including faculty/department eligibility for chairs.
- Preserve pending-request approval and current-faculty calendar behavior. Preserve request-name fallback and clearly handle dashboards that are no longer available. Expanded historical access is outside this work.
- Close an effective reporting override through its table even when its owner no longer appears in the faculty view. UI access for those owners is deferred to [issue #55](https://github.com/ucdavis/leaves/issues/55).
- Calculate faculty cap metrics from the new faculty balance view. Determine import presence and latest import update from raw `EmployeeAccrualBalances`; row existence does not certify import quality.
- Do not retain legacy view aliases or compatibility wrappers. Historical migration source files remain; do not reset the shared database or squash unrelated migration history.
- Existing self-approval behavior is outside this refactor and needs separate review.

## Stages

### 0. Branch and checklist

- [x] Recheck the working tree and repository instructions.
- [x] Create an isolated worktree and `srk/` branch.
- [x] Capture the handoff and interview decisions in this checklist.

### 1. Employee membership checks

- [x] Use `People.IsEmployee == true` in `DirectoryUserExistsAsync` and admin/CAO assignment membership checks.
- [x] Trim incoming IAM IDs only; compare the stored `IamId` directly so the primary-key index can support the lookup.
- [x] Cover employees without accruals or AppUser rows, nonemployees, unknown employee status, missing identities, and IAM whitespace handling.
- [x] Run the focused tests and the server test suite. Keep existing views, authentication, and role-cleanup timing unchanged.

### 2. Separate role identity and cleanup from faculty data

- [x] Split People-based employee identities from faculty/department data in role-options loading and responses.
- [x] Keep admin/CAO candidates and assignment names available without accruals or AppUser rows.
- [x] Use People employee membership for admin/CAO cleanup; use positive faculty eligibility, accrual presence, and resolved department for chairs.
- [x] Preserve active-target, effective-date, closure, and deletion behavior. Update both chair lookup paths or narrowly consolidate them.
- [x] Test valid non-accrual assignments surviving cleanup, ineligible identities receiving cleanup, chair department changes, and assignment effective dates.
- [x] Validate role assignment and cleanup behavior in the disposable sandbox.

### 3. Separate faculty rosters from directory candidates

- [x] Split department response/frontend faculty rosters from People-based CAO identities and candidates.
- [x] Include eligible faculty admins and CAOs in the Faculty page and department rosters.
- [x] Preserve current CAO names, broad admin search, populated form state, and the active-user and existing-role exclusions. Follow-up: remove the faculty-status restriction from CAO candidacy.
- [x] Avoid full employee-directory loads for all page requests and role cleanup. Load assignment identities by IAM ID and search candidates on demand. Remove negative nonfaculty filtering from faculty workflows as the positive query takes over.
- [x] Test the response boundaries and frontend selection behavior. Run relevant client tests, build, lint, and react-doctor.
- [x] Exercise admin/CAO selection and faculty rosters through sandbox API/browser flows.

### 4. Faculty views, migration, and remaining consumers

- [x] Add the two final EF models/query properties, including a consistent nullable `IsFaculty` projection, and remove superseded application types/properties.
- [x] Add a forward migration that creates the new views and drops both old views in the same Up operation; update the EF snapshot and provide normal rollback behavior.
- [x] Preserve the existing dynamic EXEC migration pattern and all agreed SQL calculation/department behavior.
- [x] Update role/directory, faculty dashboard/history, approval workspace, and status consumers to the new query properties.
- [x] Preserve pending request scope and AppUser/name fallback when a faculty row is missing; handle unavailable approver dashboard drill-down clearly.
- [x] Query the override table directly for closure, preserving effective-date selection and ordering.
- [x] Separate raw-import presence from faculty-only vacation-cap metrics; retain the raw latest-update source.
- [x] Update model/query tests, add forward-migration coverage, and update ignored types in notification tests. Keep historical migration tests truthful.
- [x] Run relevant server/client checks and the EF pending-model check.

### 5. SQL and end-to-end acceptance

- [x] Validate a fresh database and an upgrade from the old schema in disposable sandbox SQL. Check rollback and re-upgrade without resetting shared data.
- [x] Prove one faculty row per person and one balance row per faculty/leave type. Exclude nonfaculty, NULL faculty, nonemployees, and people without accruals.
- [x] Seed latest-date, multiple-position, department-fallback, divergent-balance, and effective-override cases; verify the actual SQL results.
- [x] Prove admins/CAOs without accruals remain selectable, named, valid, and survive cleanup; check chairs and faculty users with administrative roles.
- [x] Close an override after its owner loses faculty status or accruals through the API.
- [x] Prove faculty-only cap metrics and independent import presence with a nonfaculty-only raw import.
- [x] Exercise faculty dashboards, pending approvals, calendar behavior, admin/CAO selection, and department rosters through sandbox API/browser flows.
- [x] Verify the final schema contains both new views and neither old view, with imported tables and unrelated records preserved.
- [x] Complete server tests, EF pending-model checks, frontend tests/build/lint, and react-doctor as applicable.
- [x] Tear down every sandbox and record any remaining gaps before considering the refactor complete.

## Validation rules

- Use `./dev/sandbox up --json` for browser/API/integration validation. Use its returned app addresses and role login URLs, and finish with `./dev/sandbox down` for the same sandbox name.
- Do not run the ordinary application against the configured shared database. Application startup applies migrations.
- In-memory unit tests prove service/controller behavior, not SQL Server view definitions. Stage 5 must execute the real view SQL.
- No shared-database migrations, cloud deployment, merge, or database reset are part of these implementation stages.

## Evidence log

### 2026-09-23: setup

- Original checkout was clean on `main` at `5b9e7ec6761e41556ce97ffc78c93de7d5398056`.
- Created the isolated `srk/faculty-only-views` branch. No application or database changes preceded this stage.
- Deferred override UI work is tracked in issue #55.

### 2026-09-23: stage 1 complete

- Changed three membership queries in `AdminDirectoryDataService` and `AdminRolesController` to read employee entries from `People`. Preserved IAM trimming and existing validation messages. The shared CAO/chair assignment preflight now uses the same employee criterion; the additional chair eligibility check remains unchanged.
- Added `tests/server.tests/Services/AdminRoleMembershipTests.cs` with 15 focused cases. These exercise directory lookup and controller assignment behavior, including faculty/nonfaculty/unknown faculty employees without accruals or AppUser rows, false/NULL employee flags, student/external flags, missing identities, and whitespace.
- Release build and focused tests passed: `dotnet test tests/server.tests/server.tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AdminRoleMembershipTests --verbosity quiet`.
- Full server suite passed, **97 tests, 0 failures, 0 skipped**: `dotnet test tests/server.tests/server.tests.csproj --configuration Release --no-build --no-restore --verbosity minimal`.
- `git diff --check` passed.
- The build reported NuGet advisory warnings for unchanged OpenTelemetry and System.Security.Cryptography.Xml dependencies. No dependency versions were changed.
- Validation used the existing in-memory test provider. SQL Server view behavior and authenticated HTTP/browser flows remain unverified and are tracked in later stages. No sandbox, ordinary app, or shared-database migration was run.
- Directory identity responses, cleanup, frontend roster separation, new views, and migration remain pending. Stage 1 is compatible with the existing schema and does not complete the overall refactor.

### 2026-09-23: stage 1 index lookup follow-up

- Read-only inspection confirmed the test database has `PK_People`, a unique clustered primary-key index on `IamId`. It contained 135,631 People rows and no IAM IDs with leading spaces at inspection time.
- Estimated plans for equivalent lookup SQL showed a clustered index scan with column-side trimming, versus a clustered index seek with direct equality. These are estimated-plan results, not application latency measurements.
- Removed column-side `.Trim()` from all three stage 1 membership checks. Incoming values are still trimmed before comparison; no schema/index change is needed.
- Updated in-memory fixtures to use full-length IAM IDs while retaining whitespace in request values. The in-memory provider does not reproduce SQL Server's trailing-space comparison rules for `char` columns.
- Rebuilt and reran the full server suite with `dotnet test tests/server.tests/server.tests.csproj --configuration Release --no-restore --verbosity quiet`: **97 passed, 0 failed, 0 skipped**. Existing dependency advisory warnings remain unchanged.

### 2026-09-23: stage 2 complete

- Split `AdminRoleOptionsData` into employee identities and current faculty. Employee loading projects only IAM ID, name, and email from `People` where `IsEmployee == true`. Role responses and admin/CAO cleanup use those employee identities.
- Current faculty loading requires positive employee/faculty flags and accrual presence, retaining the existing view for department resolution until stage 4. Nonfaculty employee role options no longer depend on accrual-derived departments.
- Chair assignment validation, chair role responses, and chair cleanup use that faculty eligibility. Both role and department controllers now use the same chair lookup. A department override alone cannot make a person without accruals eligible as chair.
- Consolidated the role controller's employee membership checks into `DirectoryUserExistsAsync`, retaining direct IAM equality and input trimming.
- Added 13 role service tests covering identity loading, non-accrual admin/CAO assignments, cleanup eligibility, department scope, active targets, effective dates, and closure audit fields. Updated the existing controller fixture for its shared directory-service dependency.
- Release build and full server suite passed: **110 tests, 0 failures, 0 skipped**, using `dotnet test tests/server.tests/server.tests.csproj --configuration Release --no-restore --verbosity quiet`. Existing dependency advisory warnings remain unchanged. `git diff --check` passed.
- Started the isolated environment with `./dev/sandbox up --name faculty-views-stage2 --json`. Seeded eight synthetic People entries, matching accruals for the relevant cases, two departments, an override without accruals, and role assignments.
- Real SQL verified that the faculty query excludes nonfaculty, NULL faculty, nonemployees, unknown employee status, and people without accruals. The People projection still includes eligible employees without accruals.
- Authenticated API checks created admin/CAO assignments for an employee without accruals or an AppUser, verified names and email through `/api/admin/roles`, and assigned a valid faculty chair. Both chair endpoints rejected wrong faculty status, missing accruals, nonemployee status, and the wrong department. Admin assignment rejected false/NULL employee status.
- A temporary verification runner called `CleanupInactiveRoleAssignmentsAsync` directly against the sandbox database. It proved eligible admin/CAO assignments survive, nonemployee admins are deleted, invalid CAO/chair assignments close with audit fields and effective dates, future/expired assignments remain untouched, and an eligible faculty chair who is also admin remains assigned. The weekly timer and authentication code were not changed.
- Removed the sandbox with `./dev/sandbox down --name faculty-views-stage2`; verified no containers, volumes, or networks with its Compose project label remain. No shared database was used for stage 2 validation.
- Department-page candidate separation, faculty/CAO roster presentation, new view models, and the forward migration remain pending in stages 3-5. No frontend code changed in this stage.

### 2026-09-23: stage 3 complete

- Split `/api/admin/departments` into `facultyUsers` and lightweight `caoUsers`. Faculty lists now use the shared positive employee/faculty/accrual query. Faculty admins and CAOs remain in both the Faculty page and department rosters, with their existing role designation preserved.
- Department CAO identities come from People employees who are explicitly nonfaculty or currently assigned as CAO. This includes assigned faculty CAOs and staff without accruals or AppUser rows. Missing employee identities retain the assignment IAM ID for display fallback.
- Faculty and approval-workspace requests do not load the staff candidate directory. Removed the negative nonfaculty ID set and its filters. Approval workspaces use the positive faculty collection and retain the existing AppUser/name fallback and request-scope logic.
- The CAO picker still requires an active user with `nfa` designation. Admin, CAO, and chair role precedence still excludes those people from the picker. NULL faculty status is not treated as nonfaculty. Broad admin search continues to use the separate People-based role response.
- Added four backend tests for CAO loading, roster membership, candidate rules, identity display, and fallback. Added two React tests covering faculty roster membership and CAO selection, including preserving populated selection through query invalidation/refetch and submitting the selected IAM ID.
- Full server suite passed: **114 tests, 0 failures, 0 skipped** with `dotnet test tests/server.tests/server.tests.csproj --configuration Release --no-restore --verbosity quiet`. Full client suite passed: **3 tests**, using `npm test -- --run`. Client production build, ESLint, and `git diff --check` passed.
- React Doctor reported **88/100**, no errors, and one pre-existing high-complexity warning for `AdminDepartmentsRoute`. The stage reduces its roster-filter conditions; a broader component refactor is outside this stage. Existing dependency advisory and baseline-browser-data warnings remain; no dependency versions changed.
- Started `faculty-views-stage3` using `./dev/sandbox up --name faculty-views-stage3 --json`. Seeded eleven People cases, seven matching accrual cases, an inactive AppUser, two clusters, a department, and faculty/staff assignments. SQL and authenticated API checks proved roster inclusion/exclusion, current CAO names, staff candidacy without accruals/AppUser, inactive and NULL-faculty handling, broad admin search, and saving/reloading a CAO assignment without creating an AppUser.
- Playwright exercised the real sandbox UI: department roster and Faculty page both showed faculty admins/CAOs; CAO search returned eligible staff and omitted inactive/current-role/NULL-faculty cases; confirmation displayed both names; saving updated the current CAO label. Broad admin search found faculty without accruals and successfully added that employee as an active application admin. Browser console contained no application errors.
- Chair and CAO approval-workspace API smoke checks returned HTTP 200 with their current faculty rosters. Pending-request, dashboard-unavailable, and calendar edge cases remain in stages 4-5.
- Closed the validation browser and removed the sandbox with `./dev/sandbox down --name faculty-views-stage3`. Verified no containers, volumes, or networks with its Compose project label remain. No shared database or cloud environment was used.
- The application still uses the old SQL views through the temporary positive faculty query. Final models/views, the forward migration, remaining consumers, and SQL migration acceptance remain in stages 4-5. Changes are uncommitted for review.

### 2026-09-23: CAO split-appointment follow-up complete

- Revised the stage 3 candidate rule at the user's request. A staff member with a teaching appointment may have `IsFaculty = true`; that flag must not prevent selecting them as CAO. NULL faculty status also does not disqualify a current employee.
- CAO loading now requires only `People.IsEmployee == true`. Removed the assigned-IAM exception parameter, since assigned and unassigned employees follow the same membership rule. Faculty status remains available for designation display and does not control CAO selection.
- The picker now excludes inactive users and existing admins, CAOs, or chairs directly. Faculty roster eligibility remains unchanged.
- The original stage 3 evidence above records the earlier `nfa` restriction and is superseded by this follow-up for CAO candidacy.
- Updated server coverage for unassigned employees with true, false, and NULL faculty flags and no accrual/AppUser requirements. Parameterized the React selection test across all three cases, including refetch persistence and confirmation. Inactive-user and existing admin/CAO/chair exclusions are still asserted.
- Validation passed: **114 server tests**, **5 client tests**, client build, ESLint, and `git diff --check`. React Doctor remains **88/100**, with no errors and the existing department-component complexity warning. Existing dependency and browser-baseline warnings remain unchanged.
- The disposable `faculty-views-cao-candidates` sandbox verified all three faculty-flag cases through SQL-backed responses and authenticated assignment APIs. People with false or NULL employee status remain excluded. Faculty roster membership is unchanged. The real browser selected and saved a faculty-flagged employee without accruals or an AppUser, then displayed that employee as the current CAO.
- Closed the validation browser and removed the sandbox; no containers, volumes, or networks with its Compose project label remain. No shared database was touched. The follow-up is uncommitted and leaves the previously staged stage 3 changes intact.

### 2026-09-23: bounded employee search follow-up complete

- Corrected the bulk-load design at the user's request. The department response now includes only current CAO identities looked up by assigned IAM IDs. It does not contain unassigned CAO candidates.
- Added admin-authorized `GET /api/admin/departments/cao-candidates?query=...` and `GET /api/admin/roles/admin-candidates?query=...`. Both require 2-128 characters, support name/email substring and IAM-prefix matching, and apply deterministic ordering and `Take(20)` in SQL. Empty or one-character searches return no candidates. Eligibility exclusions apply before the result cap, and faculty status remains independent from CAO candidacy.
- Updated both pickers to search as users type and forward cancellation through TanStack Query and the API. They show search progress, failures, empty results, and a refinement hint at the limit. A selected identity is kept separately from search/page data, survives refetches, and is cleared when the search text changes. Late results from a previous query do not replace the current query's results.
- Found and removed the same full-directory load from role options and weekly cleanup. Role options load employee/faculty data only for IAM IDs referenced by assignments; cleanup uses the current assignments being checked. Admin search retains its broader employee policy and department labels while excluding existing admins. CAO search retains inactive-user and current admin/CAO/chair exclusions.
- Added tests for minimum/maximum query length, name/email/IAM matching, all faculty flags, employee eligibility, assignment dates, filtering before the 20-result cap, and deterministic results. Updated identity-loader tests to assert unrelated employees are omitted. React tests cover demand-driven search, all faculty-status selections, clearing a stale selection, preserving a selection across page refetch, and a delayed search response.
- Validation passed: **124 server tests**, **8 client tests**, frontend production build, ESLint, and `git diff --check`. React Doctor reported **82/100**, no errors, and one complexity warning in the existing department route. Existing dependency advisory and browser-baseline warnings remain unchanged.
- In the isolated `faculty-directory-search` sandbox, seeded 250 additional employees, including 50 assigned admins sorted ahead of other matches. SQL-backed API checks proved both searches return exactly 20 eligible matches after exclusions, page responses omit unrelated employees, true/false/NULL faculty cases remain searchable, and empty/short queries return no matches. Targeted cleanup preserved valid admin/CAO assignments and removed a nonemployee assignment.
- Playwright verified the result-limit hint, refined a search by IAM ID, selected and saved a faculty-flagged CAO without accruals/AppUser, and searched and assigned an employee with NULL faculty status as admin. An authenticated API read confirmed the browser-created admin assignment persisted and was active.
- This follow-up supersedes earlier evidence describing directory-wide CAO or admin candidate responses. Stage 4 remains next. No shared database, cloud deployment, or git commit was performed.
- Closed the validation browser and removed the sandbox. Verified no containers, volumes, or networks remain with its Compose project label. Follow-up changes remain uncommitted, with the previously staged changes preserved.

### 2026-09-23: stage 4 complete

- Added `CurrentFacultyWithAccrual` and `CurrentFacultyAccrualBalance` with keyless mappings to the final view names. Preserved IAM column type and balance precision, added nullable `IsFaculty` to the faculty projection, and removed `HasCurrentAccrualRecord` from application models and consumers.
- Added `20260923231654_ReplaceCurrentViewsWithFacultyViews`. Up creates both faculty-only views and drops both old views in the same migration transaction. Down restores the exact preceding view definitions and removes the new views. The designer and snapshot contain only the intended model changes; historical migration source and tests are unchanged.
- Both new views require positive employee/faculty flags and matching accruals. Their SQL retains per-employee latest dates, representative position ranking, Level5/4/3 fallback, Pacific effective overrides, and MAX/count/min/max/divergence calculations. Dynamic EXEC preserves CREATE VIEW batch requirements.
- Replaced the remaining dashboard/history, directory/roles, approval workspace, and status references. Removed the temporary People join from faculty loading. People-based bounded candidate search and assignment identity loading remain unchanged.
- Kept pending approval routing by the stored department/cluster snapshot and AppUser/IAM name fallback. Approved calendar entries still require current faculty roster membership. Approver dashboard lookup returns unavailable when its target has no faculty row, including self drill-down; the page explains that pending requests remain in the approval workspace.
- Override closure now queries the override table directly, ignores active-target query filters to match the view, and selects by effective date, latest start, then highest ID. It uses the Pacific business date for both selection and the exclusive end date, avoiding an evening UTC/Pacific mismatch; audit timestamps remain UTC. Owners need no faculty, People, or AppUser row for the closure endpoint. Deferred UI access remains issue #55.
- Import presence now uses the existing raw-table MAX of required `LastUpdated`, which is NULL only when the raw table is empty. No additional directory or balance query is needed. Faculty vacation-cap metrics read the new faculty balance view.
- Added migration Up/Down coverage, override selection tests with active/inactive departments and missing owners, and chair/CAO pending-request scope and name-fallback tests. Updated model/query and notification-test mappings. Full server suite passed: **130 tests, 0 failures, 0 skipped**.
- Full client suite passed: **8 tests**. Client production build, ESLint, and `git diff --check` passed. React Doctor remains **82/100**, no errors, with the existing department-route complexity warning. Existing dependency advisories and browser-baseline warnings remain unchanged.
- EF reported no pending model changes using the Release build and a dummy localhost connection. Its installed 8.0.21 CLI warns that the runtime is 10.0.1; scaffolding and the model check succeeded. The design-time host-aborted log occurs before application initialization. No shared database was contacted.
- Started `faculty-views-stage4` through `./dev/sandbox up --name faculty-views-stage4 --json`. The fresh isolated SQL database applied the migration successfully and contained both new views and neither obsolete view. Synthetic faculty, staff, NULL-faculty, nonemployee, and missing-accrual cases proved both final views enforce the same membership rules.
- Authenticated API smoke checks loaded admin roles, departments, faculty, status, faculty dashboard/history, and chair/CAO workspaces and faculty drill-down. After removing a requester's faculty flag, both approvers still saw the pending request with its AppUser name; the approved calendar and roster omitted the requester, and dashboard drill-down returned 404. The admin override endpoint closed that requester's existing override.
- Playwright showed the specific unavailable-dashboard message, then followed Approvals and found the same named pending request with its action buttons. The browser's only error was the expected dashboard API 404.
- A second sandbox check removed all positive faculty flags while retaining raw accruals. Status still reported accrual data present with the raw latest-update timestamp, with zero faculty cap metrics. Removing the disposable raw rows changed import status to planned with no timestamp.
- Stage 5 still needs the complete old-schema upgrade, rollback/re-upgrade, position/fallback/override edge-case matrix, and end-to-end decision acceptance. The stage 4 smoke checks do not complete that acceptance stage. Changes remain uncommitted for review.
- Closed the validation browser and removed the sandbox with `./dev/sandbox down --name faculty-views-stage4`. Verified no containers, volumes, or networks with its Compose project label remain. No shared database, cloud deployment, or git commit was performed.

### 2026-09-23: stage 5 complete

- Validated committed implementation `dc00b12` on `srk/faculty-only-views`. No application changes were needed during acceptance. Authentication and historical migration files remain unchanged; only the new migration/designer and current model snapshot differ from the base migration set.
- Started `faculty-views-acceptance` with `./dev/sandbox up --name faculty-views-acceptance --json`. Its fresh SQL database applied all migrations and contained exactly the two final views. Created a second database, `LeavesFacultyMigrationAcceptance`, on that same disposable SQL instance for upgrade tests, leaving the sandbox application's database on the current schema.
- Migrated the second database through `20260828170808_AddEmployeeAccrualImportSupport`, seeded 18 People cases, accrual rows, overrides, staging rows, and unrelated application records, then applied the generated idempotent upgrade script. Ran that script again, rolled back through EF, and re-upgraded through EF. All transitions passed. Rollback restored the exact original view definitions; the final schema contains neither obsolete view.
- Compared hashes of the serialized rows in every application/import/staging table, excluding EF migration history, and both promotion procedure definitions before and after every transition. All matched. The populated import and staging tables, role/override/request records, and promotion procedures survived upgrade, repeated idempotent execution, rollback, and re-upgrade.
- Real SQL verified exact faculty membership for true/false/NULL employee and faculty flags, missing accrual exclusion, one faculty row per eligible person, and one balance row per faculty/leave type. The results retain each employee's own latest stored date and omit leave types present only on older dates.
- Position fixtures proved nonblank job/class precedence and position-number tie-breaking. They also proved Level5/4/3 department fallback, missing departments, People-first identity with accrual fallback, MAX rather than SUM, position counts, min/max values, and divergent versus equal position balances.
- Override fixtures proved Pacific business dates, inclusive starts, exclusive ends, future/expired exclusion, latest-start/highest-ID precedence, source department preservation, and unchanged handling of an override pointing to an inactive department.
- SQL-backed API tests verified 20-result limits for admin/CAO search, eligibility without accruals or AppUsers, faculty-flagged CAO candidacy, and NULL-faculty admin candidacy. Assignments stayed named and valid after targeted cleanup, while a nonemployee admin was removed. Faculty admins and CAOs remained in rosters. Chair endpoints rejected missing-accrual and nonfaculty cases.
- Created leave requests through the faculty API and approved a current-faculty request. Its approved calendar entry appeared. After moving the requester to another department, pending requests stayed with their stored approval scope, the approved calendar followed the current roster, and both original approvers were denied current dashboard access. Out-of-scope request decisions returned 404.
- After removing the requester's faculty flag, chair/CAO pending lists retained the AppUser display name and dashboard drill-down returned 404. The CAO denied one pending request, recording one action and one decision notification; another decision on that request was rejected. The admin API closed overrides both after loss of faculty status and after loss of accruals without an AppUser.
- Playwright verified the faculty admin and faculty CAO in the department roster, selected and saved a faculty-flagged CAO without accruals/AppUser, and assigned a NULL-faculty employee as admin. Authenticated API reads confirmed both browser assignments persisted with their People names and did not create AppUsers. Candidate search remained admin-only.
- In the browser, the chair approved the remaining pending request after its requester lost faculty eligibility. The pending list became empty. Database checks found exactly one chair approval action and one decision notification. The former faculty member remained outside the current-faculty calendar, and the requester's existing history showed the saved decision. The Team Calendar rendered successfully with the current chair only. Browser console reported no errors or warnings for these assignment and approval flows.
- Verified faculty-only vacation-cap metrics despite a staff balance above the cap. With all faculty flags cleared but raw accrual rows retained, import status stayed ready with its raw latest-update timestamp and zero faculty cap metrics. Removing the disposable raw rows changed status to planned with no timestamp.
- Final checks passed: **130 server tests, 8 client tests, frontend production build, ESLint, EF pending-model check, and `git diff --check`**. React Doctor remains **82/100**, with one existing department-route complexity warning and no errors. Unchanged NuGet dependency advisories and the installed EF 8.0.21 CLI/runtime 10.0.1 version warning remain outside this refactor.
- Validation runner source and detailed logs remain locally under `/private/tmp/leaves-faculty-acceptance`, `/private/tmp/leaves-faculty-api-acceptance`, and `/tmp/leaves-faculty-acceptance-*.log`. These temporary checks use only the named sandbox manifest and loopback SQL endpoint. The checklist is the durable acceptance record.
- Scope exclusions remain deliberate: override-management UI for owners absent from the faculty roster is [issue #55](https://github.com/ucdavis/leaves/issues/55), and existing self-approval behavior is unchanged. No shared database migration, deployment, push, or merge was performed.
- Closed the browser and removed `faculty-views-acceptance` with `./dev/sandbox down --name faculty-views-acceptance`. Verified zero containers, volumes, or networks with its Compose label remain, including the volume holding both disposable databases. All checklist items are complete. The checklist is the only uncommitted file.

### 2026-09-24: review regression coverage

- Replaced SQL-fragment assertions with a SQL Server migration test covering upgrade, rollback, re-upgrade, view membership, IAM lookups, per-person latest dates, representative positions, Level5/4/3 fallback, Pacific-effective overrides, and MAX/count/min/max/divergence results. Rollback compares observable query results with the preceding schema. EF metadata tests remain unchanged.
- The integration test creates and deletes a uniquely named database on the disposable sandbox SQL service. Ordinary unit runs explicitly skip it unless `LEAVES_SANDBOX_TESTS=1`. Run it inside the sandbox app container, where `sql` resolves to the disposable database service:

```bash
./dev/sandbox up --name faculty-view-tests --json
sandbox_project=$(./dev/sandbox status --name faculty-view-tests --json | python3 -c 'import json,sys; print(json.load(sys.stdin)["project"])')
sandbox_app=$(docker ps --filter "label=com.docker.compose.project=$sandbox_project" --filter label=com.docker.compose.service=devcontainer --format '{{.ID}}')
docker exec --user vscode --workdir /workspace --env LEAVES_SANDBOX_TESTS=1 "$sandbox_app" dotnet test tests/server.tests/server.tests.csproj --filter 'FullyQualifiedName~FacultyViewsMigrationTests|FullyQualifiedName~CurrentFacultyWithAccrualTests|FullyQualifiedName~CurrentFacultyAccrualBalanceTests'
./dev/sandbox down --name faculty-view-tests
```

- Focused verification passed: **3 tests, 0 failures, 0 skipped**, including the live SQL migration test and both EF metadata tests. No full test or lint suite was run in this review phase. Existing dependency advisory warnings remain. Removed `faculty-view-tests` afterward: two containers, twelve volumes, and one network. No shared database or application code was changed.
