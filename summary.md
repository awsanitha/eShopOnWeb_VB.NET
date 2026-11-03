# Migration Summary: eShopOnWeb.vazor.sln — .NET Core 3.1 / netstandard2.1 → net10.0

## Final Build Status
`dotnet build eShopOnWeb.vazor.sln` → **Build succeeded. 0 Warning(s). 0 Error(s).**

---

## Changes Made

### 1. ApplicationCore.vb/ApplicationCore.vb.vbproj
- Upgraded `<TargetFramework>` from `netstandard2.1` to `net10.0` (VB library projects stay single-TFM per KB 29-vb-net-specifics.md)
- Added `<OptionExplicit>On</OptionExplicit>`, `<OptionStrict>Off</OptionStrict>`, `<OptionInfer>On</OptionInfer>`
- Upgraded `Ardalis.GuardClauses` from `1.4.2` to `4.6.0` (latest compatible)

### 2. Infrastructure.vb/Infrastructure.vb.vbproj
- Upgraded `<TargetFramework>` from `netstandard2.1` to `net10.0`
- Added `<OptionExplicit>`, `<OptionStrict>`, `<OptionInfer>` directives
- Removed old `Microsoft.AspNetCore.Identity Version="2.2.0"` (now covered by Identity.EntityFrameworkCore)
- Upgraded `Microsoft.AspNetCore.Identity.EntityFrameworkCore` from `3.1.0` to `10.0.14`
- Upgraded `Microsoft.EntityFrameworkCore.SqlServer` and `.Tools` from `3.1.0` to `10.0.14`
- Updated `Ardalis.EFCore.Extensions` from `2.2.1` to `6.0.0` (resolves NU1603)
- Added `System.Security.Cryptography.Pkcs 10.0.14` to pin over the vulnerable `9.0.11` transitive

### 3. Web.VB/Web.VB.vbproj
- Removed `Microsoft.CodeAnalysis 3.4.0` (conflicted with `Microsoft.VisualStudio.Web.CodeGeneration.Design 10.0.2` over Workspaces version — resolved NU1107)
- Added `PrivateAssets=all` + `IncludeAssets` to `Microsoft.VisualStudio.Web.CodeGeneration.Design` (design-time only)
- Upgraded `Swashbuckle.AspNetCore` from `5.0.0-rc2` to `6.9.0` (removed RC, fixes vulnerability GHSA-qrmm-w75w-3wpx)
- Upgraded `Swashbuckle.AspNetCore.SwaggerUI` from `5.0.0-rc4` to `6.9.0`
- Upgraded `Ardalis.ListStartupServices` from `1.1.3` to `1.1.4` (removes transitive `Microsoft.NETCore.App 1.1.2` vulnerability)
- Added `System.Security.Cryptography.Pkcs 10.0.14` pin (replaces vulnerable `9.0.11`)
- Added `NuGet.Packaging 7.9.0` and `NuGet.Protocol 7.9.0` pins (replaces vulnerable `6.12.1` from CodeGeneration.Design)

### 4. Web.VB/Startup.vb
- Replaced `Imports Newtonsoft.Json` with `Imports System.Text.Json` (Newtonsoft not referenced as a package)
- Replaced `JsonConvert.SerializeObject(...)` with `JsonSerializer.Serialize(...)` in the health check response writer — uses `System.Text.Json` (built into .NET Core 3.0+), no additional package needed
- Replaced obsolete `app.UseDatabaseErrorPage()` with `app.UseMigrationsEndPoint()` (from `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore`, already referenced)

---

## Next Steps

- **MediatR 7.0.0** is old (2019); consider upgrading to MediatR 12.x which has breaking changes (renamed `IRequest<Unit>` patterns, removed `IRequestHandler` shortcuts). Schedule as a separate upgrade.
- **Vazor 1.8.3** and **ZML.1.0 1.1.2** are the original Vazor framework packages last updated for .NET Core 3.1. These have no newer published versions. The project builds and should run, but if Vazor has runtime issues on .NET 10, consider migrating views fully to standard Razor.
- **Option Strict Off** — All projects still use `Option Strict Off`. After confirming the application runs correctly end-to-end, consider enabling `Option Strict On` per-project and fixing the `BC30512`/`BC30574` errors for better compile-time safety.
- **HasName → HasDatabaseName** in EF Core Identity migrations — the BC40000 warnings were cleared by the upgrade; no code change was needed since they became compile warnings only, but the migration snapshots use the old API. These are pre-existing generated files and safe to leave unless a new migration scaffold is needed.
- **SYSLIB0051** in `BasketNotFoundException.vb` — the `Protected Sub New(info As SerializationInfo, context As StreamingContext)` serialization constructor is obsolete in .NET 5+. The fix is to remove that constructor entirely (BinaryFormatter serialization is removed from .NET). Safe to do in a follow-up.
