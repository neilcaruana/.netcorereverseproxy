using ReverseProxyServer.Web.Components;
using ReverseProxyServer.Web.Hubs;
using ReverseProxyServer.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();

// Register the dashboard data service
builder.Services.AddScoped<IDashboardDataService, DashboardDataService>();

// Configure database path from appsettings
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<ProxyEventHub>("/hubs/proxy-events");

app.Run();
