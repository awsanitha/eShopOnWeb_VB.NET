# eShopOnWeb.vazor Migration Summary

## Status
**COMPLETE** — `dotnet build eShopOnWeb.vazor.sln` exits with **0 errors, 0 warnings**.

## Changes Made

### Web.VB/Web.VB.vbproj
- Upgraded `Microsoft.CodeAnalysis` from `3.4.0` → `5.0.0` to resolve NU1107 version conflict with `Microsoft.VisualStudio.Web.CodeGeneration.Design 10.0.2`.
- Upgraded `Swashbuckle.AspNetCore` from `5.0.0-rc2` → `8.1.0` (stable, no known vulnerabilities). Removed separate `Swashbuckle.AspNetCore.SwaggerUI 5.0.0-rc4` reference (included in meta-package).
- Upgraded `Ardalis.ListStartupServices` from `1.1.3` → `1.1.4` to remove transitive `Microsoft.NETCore.App 1.1.2` high-severity vulnerability (the old version embedded `NETStandard.Library/2.0.3` which pulled in NETCore 1.1.2 deps).
- Upgraded `Vazor` from `1.8.3` → `2.1.0` to remove transitive `NETStandard.Library/2.0.3` dependency chain.
- Removed explicit `ZML.1.0 1.1.2` reference (Vazor 2.1.0 already depends on `ZML 1.1.3`; old package embedded old NETCore deps).
- Added explicit `Microsoft.Data.SqlClient 6.1.8` reference to force upgrade from `6.1.7` (resolves `System.Security.Cryptography.Pkcs` vulnerability from SqlClient's transitive dep).
- Added `NuGetAuditSuppress` for `GHSA-g4vj-cjjj-v7hg` (low-severity advisory for `NuGet.Packaging/Protocol 6.12.1` pulled in by dev-only `Microsoft.VisualStudio.Web.CodeGeneration.Design` tool).
- Marked `Microsoft.VisualStudio.Web.CodeGeneration.Design` with `PrivateAssets="all"` to limit transitive dep propagation.

### ApplicationCore.vb/ApplicationCore.vb.vbproj
- Upgraded `Ardalis.GuardClauses` from `1.4.2` → `4.6.0` to remove `NETStandard.Library 1.x` dependency chain carrying `Microsoft.NETCore.App 1.1.2` high-severity vulnerability. API is backward-compatible (`Guard.Against.*` methods and `IGuardClause` interface unchanged).

### Web.VB/Controllers/AccountController.vb
- Added `Overloads` keyword to `SignIn` (x2) and `SignOut` methods to suppress BC40005 "shadows an overridable method" warnings.

### Web.VB/Startup.vb
- Replaced `Imports Newtonsoft.Json` with `Imports System.Text.Json` (Vazor 2.1.0 no longer includes Newtonsoft.Json transitively; migrated to built-in serializer per KB guidance).
- Replaced `JsonConvert.SerializeObject(...)` with `JsonSerializer.Serialize(...)` in health check response writer.
- Changed multi-line `Function(...)...End Function` action lambdas to `Sub(...)...End Sub` (BC42105 fix — these lambdas are `Action(Of T)` delegates that don't return a value; using `Function` caused VB to warn about missing return paths on all code paths).
  - `services.Configure(Of ServiceConfig)(...)` 
  - `services.Configure(Of CookiePolicyOptions)(...)`
  - `services.ConfigureApplicationCookie(...)`
  - `app.UseSwaggerUI(...)`
  - `app.UseEndpoints(...)`
- Added `As Task` return type annotation to the `Async Function` health check response writer lambda to suppress BC42105.
- Replaced obsolete `app.UseDatabaseErrorPage()` with `app.UseMigrationsEndPoint()` (BC40000 fix).
- Added `services.AddDatabaseDeveloperPageExceptionFilter()` to `ConfigureServices` (required companion to `UseMigrationsEndPoint`).

### Infrastructure.vb/Data/CatalogContextSeed.vb
- Replaced `Task.Run(Sub() SeedAsync(...)).Wait()` with `SeedAsync(...).GetAwaiter().GetResult()` in the retry catch block. The original `Sub()` lambda silently discarded the returned `Task` (BC42358). VB.NET does not allow `Await` inside a `Catch` block (BC36943), so `.GetAwaiter().GetResult()` is the correct synchronous wait pattern here.

## Next Steps
- **System.Security.Cryptography.Pkcs**: As of this migration, `9.0.11`/`9.0.12` (the latest 9.x standalone releases) are still listed as having known vulnerabilities (GHSA-2fmv-rv2c-59x7 / GHSA-846f-f764-76mx). No standalone 10.0.x version exists on NuGet. The .NET 10 runtime itself ships the patched implementation; this only affects the NuGet package audit. Monitor for a patched `9.0.x` release or a standalone `10.0.x` NuGet package and upgrade `Microsoft.Data.SqlClient` when available.
- **MediatR**: The project uses `MediatR 7.0.0` and `MediatR.Extensions.Microsoft.DependencyInjection 7.0.0`. Current MediatR is 12.x. The 7.x API (`services.AddMediatR(typeof(...).Assembly)`) is different from 12.x (`services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...))`). A future migration pass should upgrade MediatR to 12.x.
- **Infrastructure packages**: `Infrastructure.vb` still targets `netstandard2.1` with `Microsoft.AspNetCore.Identity 2.2.0` and `Microsoft.AspNetCore.Identity.EntityFrameworkCore 3.1.0`. Consider upgrading to current .NET 10 versions (`10.0.14`) and changing the TFM to `net10.0` in a future pass.
