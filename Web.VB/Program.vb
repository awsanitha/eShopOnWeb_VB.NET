
Imports System.Text
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Identity
Imports Microsoft.eShopWeb.Infrastructure.Data
Imports Microsoft.eShopWeb.Infrastructure.Identity
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging

''' <summary>
''' Composition root. On net10.0 the app uses the minimal hosting model, so the service
''' graph and the middleware pipeline are both wired up here (the legacy Startup class
''' and UseStartup(Of Startup) reflection convention are gone). The registration bodies
''' live in <see cref="ApplicationServicesExtensions"/>.
''' </summary>
Public Class Program

    ' VB.NET has no async Main, so the async startup path is awaited here. This is
    ' process-startup code (not a request path), which is the one place where
    ' blocking on a Task is appropriate.
    Public Shared Sub Main(ByVal args As String())
        RunAsync(args).GetAwaiter().GetResult()
    End Sub

    Private Shared Async Function RunAsync(ByVal args As String()) As Task
        Dim builder = WebApplication.CreateBuilder(args)

        ' Service registration (environment-aware: in-memory databases for
        ' Development/Testing, SQL Server for Production).
        builder.Services.AddApplicationServices(builder.Configuration, builder.Environment)

        Dim app = builder.Build()

        ' Middleware pipeline + endpoint mapping.
        app.UseApplicationPipeline(app.Environment)

        Await SeedDatabaseAsync(app)

        Await app.RunAsync()
    End Function

    ''' <summary>Seeds the catalog and identity stores before the host starts serving.</summary>
    Private Shared Async Function SeedDatabaseAsync(ByVal app As WebApplication) As Task
        Using scope = app.Services.CreateScope()
            Dim Services = scope.ServiceProvider
            Dim LoggerFactory = Services.GetRequiredService(Of ILoggerFactory)()
            Try
                Dim CatalogContext = Services.GetRequiredService(Of CatalogContext)()
                Await CatalogContextSeed.SeedAsync(CatalogContext, LoggerFactory)

                Dim UserManager = Services.GetRequiredService(Of UserManager(Of ApplicationUser))()
                Dim RoleManager = Services.GetRequiredService(Of RoleManager(Of IdentityRole))()
                Await AppIdentityDbContextSeed.SeedAsync(UserManager, RoleManager)
            Catch ex As Exception
                Dim Logger = LoggerFactory.CreateLogger(Of Program)()
                Logger.LogError(ex, "An error occurred seeding the DB.")
            End Try
        End Using
    End Function

End Class

