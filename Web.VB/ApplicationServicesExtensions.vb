Imports System.Runtime.CompilerServices
Imports Ardalis.ListStartupServices
Imports MediatR
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Diagnostics.HealthChecks
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Identity
Imports Microsoft.AspNetCore.Mvc.ApplicationModels
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.eShopWeb.ApplicationCore.Interfaces
Imports Microsoft.eShopWeb.ApplicationCore.Services
Imports Microsoft.eShopWeb.Infrastructure.Data
Imports Microsoft.eShopWeb.Infrastructure.Identity
Imports Microsoft.eShopWeb.Infrastructure.Logging
Imports Microsoft.eShopWeb.Infrastructure.Services
Imports Microsoft.eShopWeb.Web.Interfaces
Imports Microsoft.eShopWeb.Web.Services
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Diagnostics.HealthChecks
Imports Microsoft.Extensions.Hosting
Imports Newtonsoft.Json
Imports Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation
Imports System.Net.Mime
Imports Microsoft.eShopWeb.Web.ViewModels
Imports Microsoft.eShopWeb.ApplicationCore

''' <summary>
''' Service-registration composition for the web app. This replaces the legacy
''' <c>Startup</c> class: on net10.0 the app uses the minimal hosting model, where
''' <c>Program.vb</c> is the composition root and registration lives in
''' <c>IServiceCollection</c> extension methods. Every registration, option and
''' middleware statement is carried over from the original Startup unchanged.
''' </summary>
Public Module ApplicationServicesExtensions

    ''' <summary>
    ''' Registers services for the current environment. This reproduces the
    ''' environment-specific <c>ConfigureDevelopmentServices</c> /
    ''' <c>ConfigureTestingServices</c> / <c>ConfigureProductionServices</c> convention
    ''' that <c>UseStartup(Of Startup)</c> used to resolve by reflection.
    ''' </summary>
    <Extension>
    Public Sub AddApplicationServices(ByVal services As IServiceCollection,
                                      ByVal configuration As IConfiguration,
                                      ByVal environment As IWebHostEnvironment)
        If environment.IsDevelopment() Then
            services.AddDevelopmentServices(configuration)
        ElseIf environment.IsEnvironment("Testing") Then
            services.AddTestingServices(configuration)
        Else
            services.AddProductionServices(configuration)
        End If
    End Sub

    <Extension>
    Public Sub AddDevelopmentServices(ByVal services As IServiceCollection, ByVal configuration As IConfiguration)
        services.AddInMemoryDatabaseServices(configuration)
    End Sub

    <Extension>
    Public Sub AddTestingServices(ByVal services As IServiceCollection, ByVal configuration As IConfiguration)
        services.AddInMemoryDatabaseServices(configuration)
    End Sub

    <Extension>
    Public Sub AddInMemoryDatabaseServices(ByVal services As IServiceCollection, ByVal configuration As IConfiguration)
        services.AddDbContext(Of CatalogContext)(Sub(c) c.UseInMemoryDatabase("Catalog"))
        services.AddDbContext(Of AppIdentityDbContext)(Sub(options) options.UseInMemoryDatabase("Identity"))
        services.AddCoreServices(configuration)
    End Sub

    <Extension>
    Public Sub AddProductionServices(ByVal services As IServiceCollection, ByVal configuration As IConfiguration)
        services.AddDbContext(Of CatalogContext)(Sub(c) c.UseSqlServer(configuration.GetConnectionString("CatalogConnection")))
        services.AddDbContext(Of AppIdentityDbContext)(Sub(options) options.UseSqlServer(configuration.GetConnectionString("IdentityConnection")))
        services.AddCoreServices(configuration)
    End Sub

    ''' <summary>The former <c>Startup.ConfigureServices</c>, unchanged.</summary>
    <Extension>
    Public Sub AddCoreServices(ByVal services As IServiceCollection, ByVal configuration As IConfiguration)
        ConfigureCookieSettings(services)
        CreateIdentityIfNotCreated(services)
        services.AddMediatR(Sub(cfg) cfg.RegisterServicesFromAssembly(GetType(BasketViewModelService).Assembly))
        services.AddScoped(GetType(IAsyncRepository(Of)), GetType(EfRepository(Of)))
        services.AddScoped(Of ICatalogViewModelService, CachedCatalogViewModelService)()
        services.AddScoped(Of IBasketService, BasketService)()
        services.AddScoped(Of IBasketViewModelService, BasketViewModelService)()
        services.AddScoped(Of IOrderService, OrderService)()
        services.AddScoped(Of IOrderRepository, OrderRepository)()
        services.AddScoped(Of CatalogViewModelService)()
        services.AddScoped(Of ICatalogItemViewModelService, CatalogItemViewModelService)()
        services.Configure(Of CatalogSettings)(configuration)
        services.AddSingleton(Of IUriComposer)(New UriComposer(configuration.[Get](Of CatalogSettings)()))
        services.AddScoped(GetType(IAppLogger(Of)), GetType(LoggerAdapter(Of)))
        services.AddTransient(Of IEmailSender, EmailSender)()
        services.AddMemoryCache()
        ' Replaces the removed app.UseDatabaseErrorPage() middleware (ASP.NET Core 3.1 -> 10).
        services.AddDatabaseDeveloperPageExceptionFilter()
        services.AddRouting(
                 Sub(options) options.ConstraintMap("slugify") = GetType(SlugifyParameterTransformer)
            )
        services.AddMvc(
                Sub(options) options.Conventions.Add(New RouteTokenTransformerConvention(New SlugifyParameterTransformer()))
          )
        services.AddRazorPages(
               Sub(options) options.Conventions.AuthorizePage("/Basket/Checkout")
            )
        services.AddControllersWithViews(). ' Enable Vazor
        AddRazorRuntimeCompilation(
             Sub(options)
                 ' Shared views (_ViewImports/_ViewStart/_Layout/partials) — resolved with
                 ' OS-independent paths; see VazorSharedViewFileProvider for why Vazor's
                 ' own static lookup cannot find them on a non-Windows host.
                 options.FileProviders.Add(New Infrastructure.VazorSharedViewFileProvider(GetType(Program).Assembly))
                 ' Dynamic per-request pages produced by VazorPage.CreateNew.
                 options.FileProviders.Add(New Vazor.VazorViewProvider())
             End Sub
        )
        services.AddHttpContextAccessor()
        services.AddSwaggerGen(Sub(c) c.SwaggerDoc("v1", New OpenApi.OpenApiInfo With {
        .Title = "My API",
        .Version = "v1"
    }))
        services.AddHealthChecks()
        services.Configure(Of ServiceConfig)(Sub(config)
                                                 config.Services = New List(Of ServiceDescriptor)(services)
                                                 config.Path = "/allservices"
                                             End Sub)
    End Sub

    Private Sub CreateIdentityIfNotCreated(ByVal services As IServiceCollection)
        ' ASP.NET Core 10 throws when BuildServiceProvider() is called during registration,
        ' so the "is Identity already registered?" probe inspects the service collection
        ' instead. Same behavior, no extra container.
        Dim identityAlreadyRegistered =
            services.Any(Function(descriptor) descriptor.ServiceType Is GetType(UserManager(Of ApplicationUser)))

        If Not identityAlreadyRegistered Then
            services.AddIdentity(Of ApplicationUser, IdentityRole)().AddDefaultUI().AddEntityFrameworkStores(Of AppIdentityDbContext)().AddDefaultTokenProviders()
        End If
    End Sub

    Private Sub ConfigureCookieSettings(ByVal services As IServiceCollection)
        services.Configure(Of CookiePolicyOptions)(Sub(options)
                                                       options.CheckConsentNeeded = Function(context) True
                                                       options.MinimumSameSitePolicy = SameSiteMode.None
                                                   End Sub)
        services.ConfigureApplicationCookie(Sub(options)
                                                options.Cookie.HttpOnly = True
                                                options.ExpireTimeSpan = TimeSpan.FromHours(1)
                                                options.LoginPath = "/Account/Login"
                                                options.LogoutPath = "/Account/Logout"
                                                options.Cookie = New CookieBuilder With {
                                                    .IsEssential = True
                                                }
                                            End Sub)
    End Sub

    ''' <summary>The former <c>Startup.Configure</c>, unchanged apart from middleware order.</summary>
    <Extension>
    Public Sub UseApplicationPipeline(ByVal app As IApplicationBuilder, ByVal env As IWebHostEnvironment)

        ZML.ZmlPages.Compile() ' Erase this statement when you fisnish converting all .zml files to vazor files
        Vazor.VazorSharedView.CreateAll()

        app.UseHealthChecks("/health", New HealthCheckOptions With {
            .ResponseWriter = Async Function(context, report)
                                  Dim result = JsonConvert.SerializeObject(New With {
                                               Key .status = report.Status.ToString(),
                                               Key .errors = report.Entries.[Select](Function(e) New With {
                                               Key .key = e.Key,
                                               Key .value = [Enum].GetName(GetType(HealthStatus), e.Value.Status)
                                          })
                                      })
                                  context.Response.ContentType = MediaTypeNames.Application.Json
                                  Await context.Response.WriteAsync(result)
                              End Function
        })

        If env.IsDevelopment() Then
            app.UseDeveloperExceptionPage()
            app.UseShowAllServicesMiddleware()
            app.UseMigrationsEndPoint()
        Else
            app.UseExceptionHandler("/Error")
            app.UseHsts()
        End If

        app.UseHttpsRedirection()
        app.UseStaticFiles()
        app.UseRouting()
        app.UseCookiePolicy()
        app.UseAuthentication()
        app.UseAuthorization()
        app.UseSwagger()
        app.UseSwaggerUI(Sub(c)
                             c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1")
                         End Sub)
        app.UseEndpoints(Sub(endpoints)
                             endpoints.MapControllerRoute("default", "{controller:slugify=Home}/{action:slugify=Index}/{id?}")
                             endpoints.MapRazorPages()
                             endpoints.MapHealthChecks("home_page_health_check")
                             endpoints.MapHealthChecks("api_health_check")
                         End Sub)
    End Sub

End Module
