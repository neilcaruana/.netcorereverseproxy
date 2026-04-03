using Microsoft.JSInterop;
using ReverseProxyServer.Web.Components;
using ReverseProxyServer.Web.Hubs;
using ReverseProxyServer.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();

// Register the dashboard data service with logging decorator
builder.Services.AddScoped<DashboardDataService>();
builder.Services.AddScoped<IDashboardDataService>(sp =>
    new LoggingDashboardDataService(
        sp.GetRequiredService<DashboardDataService>(),
        sp.GetRequiredService<IJSRuntime>()));

// Register the port lookup service (singleton — loads IANA CSV once into memory)
builder.Services.AddSingleton<PortLookupService>();

// Configure database path from appsettings
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<ProxyEventHub>("/hubs/proxy-events");

app.Run();
