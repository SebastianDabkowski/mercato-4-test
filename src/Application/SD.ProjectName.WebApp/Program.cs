using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.Modules.Products.Infrastructure;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;
using SD.ProjectName.WebApp.Services;
using System.Data.Common;

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
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddPasswordValidator<CommonPasswordValidator>();

builder.Services.AddTransient<IEmailSender, LoggingEmailSender>();

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<GetProducts>();

builder.Services.AddRazorPages();

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
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

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
    if (disableMigrations || useSqlite)
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
