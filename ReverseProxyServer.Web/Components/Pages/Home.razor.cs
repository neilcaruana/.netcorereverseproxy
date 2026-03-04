using System.Diagnostics;
using Kavalan.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using ReverseProxyServer.Web.Components.Dashboard;
using ReverseProxyServer.Web.Services;

namespace ReverseProxyServer.Web.Components.Pages;

public partial class Home : IAsyncDisposable
{
    [Inject] private IDashboardDataService DashboardService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IOptionsMonitor<DatabaseSettings> DatabaseSettings { get; set; } = default!;

    // Tab state
    private string _activeTab = "statistics";

    // Date range
    private DateTime _fromDate = DateTime.Today;
    private DateTime _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);

    // Loading
    private bool _isLoading = true;
    private bool _isLoadingConnections = true;
    private bool _hasInitialized;
    private string? _error;

    // Stats
    private ConnectionStats? _connectionStats;
    private long? _uniqueIPs;
    private long? _uniquePorts;
    private long? _blacklistedIPs;
    private TopConnectionInfo? _topConnection;

    // Connections
    private PagedResult<Data.DTO.Connection>? _pagedConnections;
    private int _currentPage = 1;
    private const int PageSize = 25;
    private ConnectionFilter _filter = new();
    private ConnectionLog? _connectionLogRef;

    // Realtime
    private HubConnection? _hubConnection;
    private bool _hubConnected;
    private readonly List<LogEvent> _liveEvents = new();
    private bool _autoScroll = true;
    private const int MaxLiveEvents = 500;

    // World view
    private List<CountryConnectionCount>? _countryCounts;
    private bool _isLoadingWorldView;
    private bool _mapInitialized;
    private bool _darkMapStyle = true;
    private const string MapElementId = "world-map";
    private const string MapId = "eb64f89d349abcdbea900459";
    private const string DatasetId = "3695db17-21fc-48d4-94ea-bdc7218fe18b";

    // Database switcher
    private bool _showDbSwitcher;
    private string _activeDb = "Release";
    private DotNetObjectReference<Home>? _dotNetRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_hasInitialized)
        {
            _hasInitialized = true;
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("eval", """
                if (!window.__dbSwitcherHandler) {
                    window.__dbSwitcherHandler = (e) => {
                        if (e.ctrlKey && e.shiftKey && e.code === 'KeyD') {
                            e.preventDefault();
                            if (window.__dbSwitcherRef)
                                window.__dbSwitcherRef.invokeMethodAsync('ToggleDbSwitcher');
                        }
                    };
                    document.addEventListener('keydown', window.__dbSwitcherHandler);
                }
                window.__setDbSwitcherRef = (ref) => { window.__dbSwitcherRef = ref; };
                """);
            await JS.InvokeVoidAsync("__setDbSwitcherRef", _dotNetRef);
            _ = StartHubConnectionAsync();
            await LoadAllDataAsync();
        }
    }

    [JSInvokable]
    public void ToggleDbSwitcher()
    {
        _showDbSwitcher = !_showDbSwitcher;
        InvokeAsync(StateHasChanged);
    }

    private async Task SwitchDatabaseAsync(string target)
    {
        var settings = DatabaseSettings.CurrentValue;
        var newPath = target == "Debug" ? settings.DebugPath : settings.ReleasePath;

        if (string.IsNullOrEmpty(newPath) || settings.Path == newPath)
        {
            _showDbSwitcher = false;
            return;
        }

        settings.Path = newPath;
        _activeDb = target;
        _showDbSwitcher = false;
        await LoadAllDataAsync();
    }

    // --- Data Loading ---

    private async Task LoadAllDataAsync()
    {
        var totalStopwatch = Stopwatch.StartNew();
        await LogToConsoleAsync("⏱️ Starting dashboard data load...");

        _error = null;
        _connectionStats = null;
        _uniqueIPs = null;
        _uniquePorts = null;
        _blacklistedIPs = null;
        _topConnection = null;
        _countryCounts = null;
        _mapInitialized = false;
        _isLoading = true;
        _isLoadingConnections = true;
        _currentPage = 1;
        _connectionLogRef?.ClearExpandedSessions();
        _filter = new();

        await InvokeAsync(StateHasChanged);

        var tasks = new List<Task>
        {
            LoadWithTimingAsync("ConnectionStats", async () =>
            {
                _connectionStats = await DashboardService.GetConnectionStatsAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            LoadWithTimingAsync("UniqueIPs", async () =>
            {
                _uniqueIPs = await DashboardService.GetUniqueIPsCountAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            LoadWithTimingAsync("UniquePorts", async () =>
            {
                _uniquePorts = await DashboardService.GetUniquePortsCountAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            LoadWithTimingAsync("BlacklistedIPs", async () =>
            {
                _blacklistedIPs = await DashboardService.GetBlacklistedIPsCountAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            LoadWithTimingAsync("TopConnection", async () =>
            {
                _topConnection = await DashboardService.GetTopConnectionAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            LoadWithTimingAsync("Connections", async () =>
            {
                _pagedConnections = await DashboardService.GetConnectionsPagedAsync(_fromDate, _toDate, _currentPage, PageSize, _filter);
                _isLoadingConnections = false;
                await InvokeAsync(StateHasChanged);
            })
        };

        await Task.WhenAll(tasks);

        _isLoading = false;
        totalStopwatch.Stop();
        await LogToConsoleAsync($"✅ Dashboard fully loaded in {totalStopwatch.ElapsedMilliseconds}ms");
        await InvokeAsync(StateHasChanged);

        if (_activeTab == "worldview")
            await ActivateWorldViewAsync();
    }

    private async Task LoadWithTimingAsync(string name, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await Task.Run(action);
            await LogToConsoleAsync($"📊 {name} loaded in {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            _error ??= ex.Message;
            await LogToConsoleAsync($"❌ {name} failed: {ex.Message}", true);
        }
    }

    private async Task LoadConnectionsPageAsync()
    {
        var sw = Stopwatch.StartNew();
        _isLoadingConnections = true;
        StateHasChanged();
        await Task.Yield();

        try
        {
            _pagedConnections = await Task.Run(() => DashboardService.GetConnectionsPagedAsync(_fromDate, _toDate, _currentPage, PageSize, _filter));
            await LogToConsoleAsync($"📋 Connections page {_currentPage} loaded in {sw.ElapsedMilliseconds}ms ({_pagedConnections.Items.Count} of {_pagedConnections.TotalCount} records)");
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            await LogToConsoleAsync($"❌ Connections failed: {ex.Message}", true);
        }
        finally
        {
            _isLoadingConnections = false;
            StateHasChanged();
        }
    }

    // --- Page & Filter Callbacks ---

    private async Task HandlePageChanged(int page)
    {
        if (page < 1 || (_pagedConnections != null && page > _pagedConnections.TotalPages)) return;
        if (_isLoadingConnections) return;

        _currentPage = page;
        _connectionLogRef?.ClearExpandedSessions();
        await LoadConnectionsPageAsync();
    }

    private async Task HandleFilterChanged(ConnectionFilter filter)
    {
        _filter = filter;
        _currentPage = 1;
        _connectionLogRef?.ClearExpandedSessions();
        await LoadConnectionsPageAsync();
    }

    // --- Quick Date Ranges ---

    private async Task SetToday()
    {
        _fromDate = DateTime.Today;
        _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        await LoadAllDataAsync();
    }

    private async Task SetYesterday()
    {
        _fromDate = DateTime.Today.AddDays(-1);
        _toDate = DateTime.Today.AddSeconds(-1);
        await LoadAllDataAsync();
    }

    private async Task SetThisWeek()
    {
        var today = DateTime.Today;
        var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        _fromDate = today.AddDays(-diff);
        _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        await LoadAllDataAsync();
    }

    private async Task SetThisMonth()
    {
        _fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        await LoadAllDataAsync();
    }

    private async Task SetThisYear()
    {
        _fromDate = new DateTime(DateTime.Today.Year, 1, 1);
        _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        await LoadAllDataAsync();
    }

    private async Task SetLastYear()
    {
        _fromDate = new DateTime(DateTime.Today.Year - 1, 1, 1);
        _toDate = new DateTime(DateTime.Today.Year - 1, 12, 31, 23, 59, 59);
        await LoadAllDataAsync();
    }

    // --- SignalR ---

    private async Task StartHubConnectionAsync()
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(Navigation.ToAbsoluteUri("/hubs/proxy-events"))
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<LogEvent>("ReceiveLogEvent", async (logEvent) =>
            {
                _liveEvents.Insert(0, logEvent);
                if (_liveEvents.Count > MaxLiveEvents)
                    _liveEvents.RemoveAt(_liveEvents.Count - 1);

                if (_autoScroll)
                    await InvokeAsync(StateHasChanged);
            });

            _hubConnection.Reconnected += _ => { _hubConnected = true; return InvokeAsync(StateHasChanged); };
            _hubConnection.Closed += _ => { _hubConnected = false; return InvokeAsync(StateHasChanged); };

            await _hubConnection.StartAsync();
            _hubConnected = true;
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            _hubConnected = false;
        }
    }

    private void ClearLiveEvents() => _liveEvents.Clear();

    private void TogglePauseAutoScroll()
    {
        _autoScroll = !_autoScroll;
        if (_autoScroll) StateHasChanged();
    }

    // --- World View ---

    private async Task ActivateWorldViewAsync()
    {
        _activeTab = "worldview";

        if (_mapInitialized)
        {
            try { await JS.InvokeVoidAsync("worldMap.resize"); } catch { }
            return;
        }

        _isLoadingWorldView = true;
        StateHasChanged();
        await Task.Yield();

        try
        {
            _countryCounts ??= await Task.Run(() => DashboardService.GetConnectionCountsByCountryAsync(_fromDate, _toDate));
            _mapInitialized = await InitMapAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isLoadingWorldView = false;
            StateHasChanged();
        }
    }

    private async Task<bool> InitMapAsync()
    {
        var countryData = _countryCounts?
            .Where(c => !string.IsNullOrWhiteSpace(c.CountryCode))
            .ToDictionary(c => c.CountryCode.ToUpperInvariant(), c => c.ConnectionCount)
            ?? [];

        var colorScheme = _darkMapStyle ? "DARK" : "LIGHT";

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var success = await JS.InvokeAsync<bool>("worldMap.init", MapElementId, MapId, DatasetId, colorScheme, countryData);
            if (success) return true;
            await Task.Delay(300);
        }

        return false;
    }

    private async Task ToggleMapStyleAsync(bool dark)
    {
        if (_darkMapStyle == dark) return;
        _darkMapStyle = dark;
        _isLoadingWorldView = true;
        StateHasChanged();
        await Task.Yield();

        try
        {
            await InitMapAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isLoadingWorldView = false;
            StateHasChanged();
        }
    }

    // --- Helpers ---

    private async Task LogToConsoleAsync(string message, bool isError = false)
    {
        try
        {
            var jsMethod = isError ? "console.error" : "console.log";
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            await JS.InvokeVoidAsync("eval", $"{jsMethod}('[{timestamp}] {message.Replace("'", "\\'")}')");
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        try { await JS.InvokeVoidAsync("worldMap.dispose"); } catch { }
        try
        {
            await JS.InvokeVoidAsync("eval", """
                if (window.__dbSwitcherHandler) {
                    document.removeEventListener('keydown', window.__dbSwitcherHandler);
                    window.__dbSwitcherHandler = null;
                    window.__dbSwitcherRef = null;
                }
                """);
        }
        catch { }

        _dotNetRef?.Dispose();

        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
