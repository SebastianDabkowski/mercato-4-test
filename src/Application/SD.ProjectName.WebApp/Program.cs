using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.Modules.Products.Infrastructure;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;
using SD.ProjectName.WebApp.Services;
using SD.ProjectName.WebApp;
using System.Data.Common;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

var sqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=./SDProjectName.db";
var dataSource = GetDataSourceFromConnectionString(connectionString);
var useSqlite = !OperatingSystem.IsWindows();
var disableHttpsRedirection = builder.Configuration.GetValue<bool>("DisableHttpsRedirection");
var disableMigrations = builder.Configuration.GetValue<bool>("DisableMigrations");
var useFakeExternalAuth = builder.Configuration.GetValue<bool>("UseFakeExternalAuth");

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = useFakeExternalAuth ? 1000 : 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

if (useSqlite)
{
    var configuredSqliteConnectionString = sqliteConnectionString;
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(configuredSqliteConnectionString));
    builder.Services.AddDbContext<ProductDbContext>(options =>
        options.UseSqlite(configuredSqliteConnectionString));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
    builder.Services.AddDbContext<ProductDbContext>(options =>
        options.UseSqlServer(connectionString));
}
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 4;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddPasswordValidator<CommonPasswordValidator>();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24);
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(1);
});

builder.Services.AddTransient<IEmailSender, LoggingEmailSender>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, LoggingAuthorizationMiddlewareResultHandler>();

var authenticationBuilder = builder.Services.AddAuthentication();

var googleSection = builder.Configuration.GetSection("Authentication:Google");
var facebookSection = builder.Configuration.GetSection("Authentication:Facebook");
var googleConfigured = !string.IsNullOrWhiteSpace(googleSection["ClientId"]) && !string.IsNullOrWhiteSpace(googleSection["ClientSecret"]);
var facebookConfigured = !string.IsNullOrWhiteSpace(facebookSection["AppId"]) && !string.IsNullOrWhiteSpace(facebookSection["AppSecret"]);

if (useFakeExternalAuth || !googleConfigured)
{
    authenticationBuilder.AddScheme<FakeExternalAuthOptions, FakeExternalAuthHandler>("Google", "Google", options =>
    {
        builder.Configuration.GetSection("FakeExternalAuth:Google").Bind(options);
        options.ClaimsIssuer = "Google";
    });
}
else
{
    authenticationBuilder.AddGoogle(options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClientId = googleSection["ClientId"]!;
        options.ClientSecret = googleSection["ClientSecret"]!;
    });
}

if (useFakeExternalAuth || !facebookConfigured)
{
    authenticationBuilder.AddScheme<FakeExternalAuthOptions, FakeExternalAuthHandler>("Facebook", "Facebook", options =>
    {
        builder.Configuration.GetSection("FakeExternalAuth:Facebook").Bind(options);
        options.ClaimsIssuer = "Facebook";
    });
}
else
{
    authenticationBuilder.AddFacebook(options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.AppId = facebookSection["AppId"]!;
        options.AppSecret = facebookSection["AppSecret"]!;
    });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/AccessDenied";
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BuyerOnly", policy => policy.RequireRole(IdentityRoles.Buyer));
    options.AddPolicy("SellerOnly", policy => policy.RequireRole(IdentityRoles.Seller));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(IdentityRoles.Admin));
});

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<GetProducts>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Buyer", "BuyerOnly");
    options.Conventions.AuthorizeFolder("/Seller", "SellerOnly");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AllowAnonymousToPage("/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage("/Products/List");
    options.Conventions.AllowAnonymousToPage("/Privacy");
});

var app = builder.Build();

// Apply migrations on startup for all modules
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Migrate ApplicationDbContext
        var applicationDbContext = services.GetRequiredService<ApplicationDbContext>();
        InitializeDatabase(applicationDbContext, useSqlite, disableMigrations);
        // Migrate ProductDbContext (Module: Products)
        var productDbContext = services.GetRequiredService<ProductDbContext>();
        InitializeDatabase(productDbContext, useSqlite, disableMigrations);
        await EnsureRolesAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!disableHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapPost("/_test/create-account", async ([FromBody] TestAccountRequest request, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager) =>
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Results.Ok(existing.Id);
        }

        var user = new ApplicationUser
        {
            AccountType = request.AccountType,
            AccountStatus = request.EmailConfirmed ? AccountStatus.Verified : AccountStatus.Unverified,
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.AccountType == AccountType.Seller ? "Seller" : "Buyer",
            LastName = "Test",
            TermsAcceptedAt = DateTimeOffset.UtcNow,
            RequiresKyc = request.AccountType == AccountType.Seller,
            KycStatus = request.AccountType == AccountType.Seller ? KycStatus.NotStarted : KycStatus.Approved,
            EmailVerificationSentAt = DateTimeOffset.UtcNow,
            EmailVerifiedAt = request.EmailConfirmed ? DateTimeOffset.UtcNow : null,
            KycSubmittedAt = request.AccountType == AccountType.Seller ? null : DateTimeOffset.UtcNow,
            KycApprovedAt = request.AccountType == AccountType.Seller ? null : DateTimeOffset.UtcNow,
            EmailConfirmed = request.EmailConfirmed,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Results.BadRequest(createResult.Errors.Select(e => e.Description));
        }

        var role = request.AccountType == AccountType.Seller ? IdentityRoles.Seller : IdentityRoles.Buyer;
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }

        var addToRoleResult = await userManager.AddToRoleAsync(user, role);
        if (!addToRoleResult.Succeeded)
        {
            return Results.BadRequest(addToRoleResult.Errors.Select(e => e.Description));
        }

        if (request.EmailConfirmed && user.AccountStatus != AccountStatus.Verified)
        {
            user.AccountStatus = AccountStatus.Verified;
            await userManager.UpdateAsync(user);
        }

        return Results.Ok(user.Id);
    });
}

await app.RunAsync();

static string? GetDataSourceFromConnectionString(string connectionString)
{
    var sqlConnectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
    if (sqlConnectionStringBuilder.TryGetValue("Data Source", out var dataSourceValue))
    {
        return dataSourceValue?.ToString();
    }

    if (sqlConnectionStringBuilder.TryGetValue("Server", out var serverValue))
    {
        return serverValue?.ToString();
    }

    return null;
}

static bool IsLocalDbDataSource(string? dataSource) =>
    dataSource?.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) == true ||
    dataSource?.Contains("mssqllocaldb", StringComparison.OrdinalIgnoreCase) == true;

static void InitializeDatabase(DbContext context, bool useSqlite, bool disableMigrations)
{
    if (useSqlite)
    {
        if (context is ApplicationDbContext)
        {
            // SQLite deployments can already contain Identity tables without migration history; EnsureCreated avoids duplicate table errors.
            context.Database.EnsureCreated();
        }
        else if (disableMigrations)
        {
            context.Database.EnsureCreated();
        }
        else
        {
            context.Database.Migrate();
        }

        if (context is ApplicationDbContext applicationDbContext)
        {
            SqliteIdentitySchemaUpdater.EnsureIdentityColumns(applicationDbContext.Database.GetDbConnection());
        }

        return;
    }

    if (disableMigrations)
    {
        context.Database.EnsureCreated();
        return;
    }

    context.Database.Migrate();
}

static async Task EnsureRolesAsync(IServiceProvider services)
{
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var roleName in IdentityRoles.All)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}
