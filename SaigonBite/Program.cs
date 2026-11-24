using Microsoft.EntityFrameworkCore;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Services;
using SaigonBite.Validators;
using SaigonBite.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SaigonBiteContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("SaigonBite")));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IAccountValidator, AccountValidator>();
builder.Services.AddSingleton<ICodeSender, SimulatedCodeSender>();
builder.Services.AddSingleton<IVerificationService, VerificationService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

var supportedCultures = new[] { "en", "vi" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
