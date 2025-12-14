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

var sqlConnectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
var dataSource = sqlConnectionStringBuilder.TryGetValue("Data Source", out var dataSourceValue)
    ? dataSourceValue?.ToString()
    : sqlConnectionStringBuilder.TryGetValue("Server", out var serverValue)
        ? serverValue?.ToString()
        : string.Empty;
var useSqlite = !OperatingSystem.IsWindows() && dataSource?.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) == true;

if (useSqlite)
{
    var configuredSqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection");
    var sqliteConnectionString = string.IsNullOrWhiteSpace(configuredSqliteConnectionString)
        ? $"Data Source=./{builder.Environment.ApplicationName}.db"
        : configuredSqliteConnectionString;
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(sqliteConnectionString));
    builder.Services.AddDbContext<ProductDbContext>(options =>
        options.UseSqlite(sqliteConnectionString));
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
//using (var scope = app.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;
//    try
//    {
//        // Migrate ApplicationDbContext
//        var applicationDbContext = services.GetRequiredService<ApplicationDbContext>();
//        applicationDbContext.Database.Migrate();
//        // Migrate ProductDbContext (Module: Products)
//        var productDbContext = services.GetRequiredService<ProductDbContext>();
//        productDbContext.Database.Migrate();
//    }
//    catch (Exception ex)
//    {
//        var logger = services.GetRequiredService<ILogger<Program>>();
//        logger.LogError(ex, "An error occurred while migrating the database.");
//        throw;
//    }
//}

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
