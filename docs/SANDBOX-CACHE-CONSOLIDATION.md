# Future Sandbox Cache Consolidation

## Status

This is a deferred design note. The current sandbox intentionally uses explicit project-scoped volumes for SQL, dependency directories, .NET build outputs, data-protection keys, and tool caches. That implementation is working and should remain unchanged until the repository has enough additional projects to justify consolidation.

The proposed future change reduces the sandbox from twelve volumes to four:

- SQL data
- One consolidated sandbox cache
- Root `node_modules`
- Client `node_modules`

Do not replace the `/workspace` bind mount with a named volume. A volume at `/workspace` would hide the checked-out worktree, so host edits would no longer reach Vite or `dotnet watch` without a separate source-copy or synchronization system.

## When to Revisit

Revisit this when one or more of these become true:

- New .NET Functions or worker projects make per-project `bin` and `obj` mounts repetitive.
- Sandbox volume count becomes difficult to maintain.
- Host-side ignored build artifacts become a recurring cleanup or ownership problem.
- A repository-wide Node workspace strategy is adopted.

The volume count itself is not a Docker runtime problem. All volumes are scoped by the sandbox Compose project and `./dev/sandbox down` removes them. The main reason to consolidate is maintainability as the solution grows.

## Proposed Design

Keep the source bind mount and route disposable outputs into `/sandbox-cache`:

```text
/workspace                         live host worktree
/workspace/node_modules            root dependency volume
/workspace/client/node_modules     client dependency volume
/sandbox-cache                     consolidated cache volume
  data-protection/
  dotnet-artifacts/<project>/bin/
  dotnet-artifacts/<project>/obj/
  dotnet-home/
  npm/
  nuget/
```

Keep SQL in its own volume because database state has a distinct lifecycle and may need separate inspection or future persistence controls.

### Compose environment

Add these sandbox-only variables to `.devcontainer/docker-compose.sandbox.yml`:

```yaml
environment:
  DataProtection__KeysPath: /sandbox-cache/data-protection
  DOTNET_CLI_HOME: /sandbox-cache/dotnet-home
  NUGET_PACKAGES: /sandbox-cache/nuget
  NPM_CONFIG_CACHE: /sandbox-cache/npm
  SANDBOX_ARTIFACTS_ROOT: /sandbox-cache/dotnet-artifacts
```

Mount one cache volume and retain the two package-local Node dependency mounts:

```yaml
volumes:
  - sandbox-root-node-modules:/workspace/node_modules
  - sandbox-client-node-modules:/workspace/client/node_modules
  - sandbox-cache:/sandbox-cache
```

The startup command must make all three mount points writable by `vscode` before dropping root privileges.

### Repository-wide .NET output redirection

Add a root `Directory.Build.props` that activates only when the sandbox variable is set:

```xml
<Project>
  <PropertyGroup Condition="'$(SANDBOX_ARTIFACTS_ROOT)' != ''">
    <BaseOutputPath>$(SANDBOX_ARTIFACTS_ROOT)/$(MSBuildProjectName)/bin/</BaseOutputPath>
    <BaseIntermediateOutputPath>$(SANDBOX_ARTIFACTS_ROOT)/$(MSBuildProjectName)/obj/</BaseIntermediateOutputPath>
    <MSBuildProjectExtensionsPath>$(BaseIntermediateOutputPath)</MSBuildProjectExtensionsPath>
    <DefaultItemExcludes>$(DefaultItemExcludes);$(MSBuildProjectDirectory)/bin/**;$(MSBuildProjectDirectory)/obj/**</DefaultItemExcludes>
  </PropertyGroup>
</Project>
```

The `DefaultItemExcludes` addition is required. Once `BaseIntermediateOutputPath` moves outside the project directory, the SDK stops automatically excluding an old local `obj` tree. Without the explicit exclusions, existing generated assembly files can be compiled alongside the new files and cause duplicate assembly attribute errors.

Because the props file is conditional, normal host and Visual Studio builds continue using their ordinary local `bin` and `obj` paths. New .NET projects beneath the repository root inherit the sandbox behavior without additional Compose mounts.

Project names should remain unique. If duplicate `MSBuildProjectName` values are introduced later, use a stable repository-relative identity in the cache path instead.

### Data-protection keys

`server/Program.cs` currently fixes the local key path under `.aspnet`. Add an optional `DataProtection:KeysPath` configuration value while retaining the current path as the default. The sandbox can then direct keys into `/sandbox-cache/data-protection` without affecting normal development or deployments.

### Node projects

Node resolves `node_modules` relative to package directories, so adding another standalone Node package would still require another dependency mount under this design. Avoid symlinking package directories into the cache because that mutates the host worktree and behaves poorly when multiple sandboxes share one checkout.

If Node projects proliferate, evaluate npm workspaces or another repository-wide package layout separately. Do not couple that migration to the .NET cache consolidation.

## Cleanup Across Compose Changes

Changing the declared volume list creates a migration concern: a sandbox created with an older Compose definition may own volumes that are no longer declared by the new file. `docker compose down --volumes` may not remove retired named volumes that it no longer knows about.

Before shipping the consolidation, update `./dev/sandbox down` to remove any remaining volumes and networks with the exact ownership label:

```text
com.docker.compose.project=<resolved sandbox project name>
```

Resolve exact labeled resource IDs after the normal Compose teardown and remove only those resources. Do not use broad name matching or Docker-wide prune commands.

## Verification Checklist

Run the full sandbox acceptance sequence after implementing the change:

1. Confirm the merged sandbox Compose model declares exactly four volumes and still uses dynamic loopback-only ports.
2. Record a fingerprint of any existing host `server/**/bin`, `server/**/obj`, and test build artifacts.
3. Start a cold sandbox with empty project volumes and wait for database-backed health.
4. Confirm .NET output, NuGet, npm cache, .NET CLI state, and data-protection keys exist under `/sandbox-cache`.
5. Confirm the host build-artifact fingerprint did not change.
6. Complete the local admin login cookie and authenticated API flow.
7. Run `dotnet test` inside the sandbox and verify test outputs also land under `/sandbox-cache`.
8. With `SANDBOX_ARTIFACTS_ROOT` unset, query MSBuild properties and confirm ordinary builds still use local `bin` and `obj`.
9. Start a second named sandbox and prove both are healthy with distinct ports, SQL volumes, and cache volumes.
10. Tear down the first sandbox and prove the second remains healthy.
11. Tear down the second and confirm no labeled containers, networks, or volumes remain.
12. Create simulated retired labeled resources and confirm the enhanced `down` command removes them.

The earlier experiment completed this sequence successfully after adding the explicit legacy `bin/obj` exclusions. Existing NuGet and npm audit warnings were unchanged and are outside this refactor.
