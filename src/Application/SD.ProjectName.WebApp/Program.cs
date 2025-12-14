using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.Modules.Products.Infrastructure;
using SD.ProjectName.WebApp.Data;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

var dataSource = GetDataSourceFromConnectionString(connectionString);
var useSqlite = !OperatingSystem.IsWindows() && IsLocalDbDataSource(dataSource);

if (useSqlite)
{
    var configuredSqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection");
    if (string.IsNullOrWhiteSpace(configuredSqliteConnectionString))
    {
        throw new InvalidOperationException("Connection string 'SqliteConnection' not found.");
    }

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

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

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
        applicationDbContext.Database.Migrate();
        // Migrate ProductDbContext (Module: Products)
        var productDbContext = services.GetRequiredService<ProductDbContext>();
        productDbContext.Database.Migrate();
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

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

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
