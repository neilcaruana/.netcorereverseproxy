using System.Globalization;
using Kavalan.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using ReverseProxyServer.Data.DTO;
using ReverseProxyServer.Web.Components.Dashboard;
using ReverseProxyServer.Web.Services;

namespace ReverseProxyServer.Web.Components.Pages;

public partial class Home : IAsyncDisposable
{
    [Inject] private IDashboardDataService DashboardService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IOptionsMonitor<DatabaseSettings> DatabaseSettings { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;

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
    private BandwidthStats? _bandwidthStats;

    // Connections
    private PagedResult<Data.DTO.Connection>? _pagedConnections;
    private int _currentPage = 1;
    private int _pageSize = 50;
    private ConnectionFilter _filter = new();
    private ConnectionLog? _connectionLogRef;
    private List<CountryInfo> _distinctCountries = [];

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
    private bool _realtimeWorldView;
    private const string MapElementId = "world-map";
    private const string MapId = "eb64f89d349abcdbea900459";
    private const string DatasetId = "3695db17-21fc-48d4-94ea-bdc7218fe18b";

    // Search
    private PagedResult<SearchResult>? _searchResults;
    private int _searchPage = 1;
    private string _searchTerm = string.Empty;
    private SearchSortOrder _searchSortOrder = SearchSortOrder.Relevance;
    private long _totalSearchableRecords;
    private bool _searchUseDateRange;
    private const int SearchPageSize = 24;

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
                if (!window.__scrollTopHandler) {
                    window.__scrollTopHandler = () => {
                        const btn = document.getElementById('scrollTopBtn');
                        if (btn) btn.style.opacity = window.scrollY > 200 ? '1' : '0';
                        if (btn) btn.style.pointerEvents = window.scrollY > 200 ? 'auto' : 'none';
                    };
                    window.addEventListener('scroll', window.__scrollTopHandler, { passive: true });
                }
                """);
            await JS.InvokeVoidAsync("__setDbSwitcherRef", _dotNetRef);
            ReadStateFromUrl();
            _ = StartHubConnectionAsync();
            await LoadAllDataAsync(preserveState: true);

            if (_activeTab == "search" && !string.IsNullOrWhiteSpace(_searchTerm))
                await LoadSearchResultsAsync();
        }
    }

    [JSInvokable]
    public void ToggleDbSwitcher()
    {
        _showDbSwitcher = !_showDbSwitcher;
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnCountryClickedFromMap(string countryCode)
    {
        await LogToConsoleAsync($"🗺️ OnCountryClickedFromMap CALLED with: '{countryCode}'");
        await LogToConsoleAsync($"🗺️ Current _activeTab BEFORE: '{_activeTab}'");

        _filter = new ConnectionFilter { CountryCode = countryCode };
        _currentPage = 1;
        _activeTab = "statistics";
        _connectionLogRef?.ClearExpandedSessions();

        await LogToConsoleAsync($"🗺️ _activeTab set to: '{_activeTab}', filter.CountryCode: '{_filter.CountryCode}'");

        await InvokeAsync(StateHasChanged);

        await LogToConsoleAsync("🗺️ StateHasChanged invoked, now loading connections...");

        await LoadConnectionsPageAsync();

        await LogToConsoleAsync($"🗺️ LoadConnectionsPageAsync complete. Page: {_currentPage}, Results: {_pagedConnections?.Items.Count ?? -1}");
        await UpdateUrlAsync();
    }

    private async Task FilterByIPAsync(string ipAddress)
    {
        _filter = new ConnectionFilter { RemoteAddress = ipAddress };
        _currentPage = 1;
        _connectionLogRef?.ClearExpandedSessions();
        StateHasChanged();
        await LoadConnectionsPageAsync();
        await UpdateUrlAsync();
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

    private async Task ChangePageSizeAsync(int size)
    {
        if (_pageSize == size) return;
        _pageSize = size;
        _currentPage = 1;
        _connectionLogRef?.ClearExpandedSessions();
        await LoadConnectionsPageAsync();
    }

    // --- Data Loading ---

    private async Task LoadAllDataAsync(bool preserveState = false)
    {
        _error = null;
        _connectionStats = null;
        _uniqueIPs = null;
        _uniquePorts = null;
        _blacklistedIPs = null;
        _topConnection = null;
        _bandwidthStats = null;
        _countryCounts = null;
        _mapInitialized = false;
        _isLoading = true;
        _isLoadingConnections = true;

        if (!preserveState)
        {
            _currentPage = 1;
            _connectionLogRef?.ClearExpandedSessions();
            _filter = new();
        }

        await InvokeAsync(StateHasChanged);

        var tasks = new List<Task>
        {
            RunAsync(async () =>
            {
                _connectionStats = await DashboardService.GetConnectionStatsAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            RunAsync(async () =>
            {
                _uniqueIPs = await DashboardService.GetUniqueIPsCountAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            RunAsync(async () =>
            {
                _uniquePorts = await DashboardService.GetUniquePortsCountAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            RunAsync(async () =>
            {
                _blacklistedIPs = await DashboardService.GetBlacklistedIPsCountAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            RunAsync(async () =>
            {
                _topConnection = await DashboardService.GetTopConnectionAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            RunAsync(async () =>
            {
                _bandwidthStats = await DashboardService.GetBandwidthStatsAsync(_fromDate, _toDate);
                await InvokeAsync(StateHasChanged);
            }),
            RunAsync(async () =>
            {
                _pagedConnections = await DashboardService.GetConnectionsPagedAsync(_fromDate, _toDate, _currentPage, _pageSize, _filter);
                _isLoadingConnections = false;
                await InvokeAsync(StateHasChanged);
            }),
            RunAsync(async () =>
            {
                _distinctCountries = await DashboardService.GetDistinctCountriesAsync(_fromDate, _toDate);
            }),
            RunAsync(async () =>
            {
                _totalSearchableRecords = await DashboardService.GetTotalConnectionDataCountAsync();
            })
        };

        await Task.WhenAll(tasks);

        _isLoading = false;
        await InvokeAsync(StateHasChanged);

        if (_activeTab == "worldview")
            await ActivateWorldViewAsync();
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await Task.Run(action);
        }
        catch (Exception ex)
        {
            _error ??= ex.Message;
        }
    }

    private async Task LoadConnectionsPageAsync()
    {
        _isLoadingConnections = true;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            _pagedConnections = await Task.Run(() => DashboardService.GetConnectionsPagedAsync(_fromDate, _toDate, _currentPage, _pageSize, _filter));
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isLoadingConnections = false;
            await InvokeAsync(StateHasChanged);
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
        await UpdateUrlAsync();
    }

    private async Task HandleFilterChanged(ConnectionFilter filter)
    {
        _filter = filter;
        _currentPage = 1;
        _connectionLogRef?.ClearExpandedSessions();
        await LoadConnectionsPageAsync();
        await UpdateUrlAsync();
    }

    private async Task HandleSortChanged(string column)
    {
        var currentColumn = _filter.SortColumn ?? nameof(Connection.ConnectionTime);

        if (currentColumn == column)
            _filter.SortDescending = !_filter.SortDescending;
        else
        {
            _filter.SortColumn = column;
            _filter.SortDescending = true;
        }

        _filter.SortColumn = column;
        _currentPage = 1;
        _connectionLogRef?.ClearExpandedSessions();
        await LoadConnectionsPageAsync();
        await UpdateUrlAsync();
    }

    // --- Search ---

    private async Task HandleSearchAsync(string searchTerm)
    {
        _searchTerm = searchTerm;
        _searchPage = 1;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _searchResults = null;
            await UpdateUrlAsync();
            return;
        }

        await LoadSearchResultsAsync();
        await UpdateUrlAsync();
    }

    private async Task HandleSearchPageChanged(int page)
    {
        _searchPage = page;
        await LoadSearchResultsAsync();
        await UpdateUrlAsync();
    }

    private async Task HandleSearchSortChanged(SearchSortOrder sortOrder)
    {
        _searchSortOrder = sortOrder;
        _searchPage = 1;
        await LoadSearchResultsAsync();
        await UpdateUrlAsync();
    }

    private void HandleSearchDateRangeToggled(bool useRange)
    {
        _searchUseDateRange = useRange;
    }

    private async Task LoadSearchResultsAsync()
    {
        try
        {
            var fromDate = _searchUseDateRange ? _fromDate : (DateTime?)null;
            var toDate = _searchUseDateRange ? _toDate : (DateTime?)null;
            _searchResults = await DashboardService.SearchConnectionDataAsync(_searchTerm, _searchPage, SearchPageSize, _searchSortOrder, fromDate, toDate);
        }
        catch (Exception ex)
        {
            _error = $"Search failed: {ex.Message}";
        }

        await InvokeAsync(StateHasChanged);
    }

    // --- Quick Date Ranges ---

    private async Task SetToday()
    {
        _fromDate = DateTime.Today;
        _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        await LoadAllDataAsync();
        await UpdateUrlAsync();
    }

    private async Task SetYesterday()
    {
        _fromDate = DateTime.Today.AddDays(-1);
        _toDate = DateTime.Today.AddSeconds(-1);
        await LoadAllDataAsync();
        await UpdateUrlAsync();
    }

    private async Task SetThisWeek()
    {
        var today = DateTime.Today;
        var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        _fromDate = today.AddDays(-diff);
        _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        await LoadAllDataAsync();
        await UpdateUrlAsync();
    }

    private async Task SetThisMonth()
    {
        _fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        await LoadAllDataAsync();
        await UpdateUrlAsync();
    }

    private async Task SetThisYear()
    {
        _fromDate = new DateTime(DateTime.Today.Year, 1, 1);
        _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        await LoadAllDataAsync();
        await UpdateUrlAsync();
    }

    private async Task SetLastYear()
    {
        _fromDate = new DateTime(DateTime.Today.Year - 1, 1, 1);
        _toDate = new DateTime(DateTime.Today.Year - 1, 12, 31, 23, 59, 59);
        await LoadAllDataAsync();
        await UpdateUrlAsync();
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

                if (_realtimeWorldView)
                    await TryPulseForConnectionAsync(logEvent.Message);
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
            _isLoadingWorldView = true;
            StateHasChanged();
            await Task.Yield();

            try { await JS.InvokeVoidAsync("worldMap.resize"); } catch { }

            _isLoadingWorldView = false;
            StateHasChanged();
            await UpdateUrlAsync();
            return;
        }

        _isLoadingWorldView = true;
        StateHasChanged();
        await Task.Yield();

        try
        {
            _countryCounts ??= await Task.Run(() => DashboardService.GetConnectionCountsByCountryAsync(_fromDate, _toDate));
            _mapInitialized = await InitMapAsync();

            if (_mapInitialized && _mapZoom.HasValue && _mapLat.HasValue && _mapLng.HasValue)
            {
                await JS.InvokeVoidAsync("worldMap.setState", _mapZoom.Value, _mapLat.Value, _mapLng.Value);
                _mapZoom = null;
                _mapLat = null;
                _mapLng = null;
            }
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

        await UpdateUrlAsync();
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
            var success = await JS.InvokeAsync<bool>("worldMap.init", MapElementId, MapId, DatasetId, colorScheme, countryData, _dotNetRef);
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

    private async Task ToggleRealtimeWorldViewAsync(bool enabled)
    {
        _realtimeWorldView = enabled;

        try
        {
            if (enabled)
            {
                var serverCountry = Configuration["ServerLocation:CountryCode"] ?? "MT";
                await JS.InvokeVoidAsync("worldMap.startRealtime", serverCountry);
            }
            else
            {
                await JS.InvokeVoidAsync("worldMap.stopRealtime");
            }
        }
        catch { }
    }

    private async Task TryPulseForConnectionAsync(string message)
    {
        try
        {
            // Log format: "Connection received from [CountryName] IP:Port..."
            const string prefix = "Connection received from [";
            var idx = message.IndexOf(prefix, StringComparison.Ordinal);
            if (idx < 0) return;

            var start = idx + prefix.Length;
            var end = message.IndexOf(']', start);
            if (end <= start) return;

            var countryName = message[start..end];
            if (string.IsNullOrWhiteSpace(countryName) || _countryCounts == null) return;

            var match = _countryCounts.FirstOrDefault(c =>
                c.CountryName.Equals(countryName, StringComparison.OrdinalIgnoreCase));
            if (match == null || string.IsNullOrWhiteSpace(match.CountryCode)) return;

            await InvokeAsync(async () =>
                await JS.InvokeVoidAsync("worldMap.addPulse", match.CountryCode));
        }
        catch { }
    }

    // --- Helpers ---

    private string GetDatabaseSize()
    {
        try
        {
            var path = DatabaseSettings.CurrentValue.Path;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "—";
            var bytes = new FileInfo(path).Length;
            return bytes switch
            {
                >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
                >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
                >= 1_024 => $"{bytes / 1_024.0:F1} KB",
                _ => $"{bytes} B"
            };
        }
        catch { return "—"; }
    }

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

    // --- URL State ---

    private void ReadStateFromUrl()
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("tab", out var tab))
            _activeTab = tab.ToString();

        if (query.TryGetValue("from", out var from) && DateTime.TryParse(from, CultureInfo.InvariantCulture, out var fromDate))
            _fromDate = fromDate;
        if (query.TryGetValue("to", out var to) && DateTime.TryParse(to, CultureInfo.InvariantCulture, out var toDate))
            _toDate = toDate;

        if (query.TryGetValue("page", out var page) && int.TryParse(page, out var p))
            _currentPage = p;

        if (query.TryGetValue("fType", out var fType)) _filter.ProxyType = fType;
        if (query.TryGetValue("fAddr", out var fAddr)) _filter.RemoteAddress = fAddr;
        if (query.TryGetValue("fPort", out var fPort)) _filter.RemotePort = fPort;
        if (query.TryGetValue("fLAddr", out var fLAddr)) _filter.LocalAddress = fLAddr;
        if (query.TryGetValue("fLPort", out var fLPort)) _filter.LocalPort = fLPort;
        if (query.TryGetValue("fBl", out var fBl)) _filter.IsBlacklisted = fBl;
        if (query.TryGetValue("fCountry", out var fCountry)) _filter.CountryCode = fCountry;
        if (query.TryGetValue("sort", out var sort)) _filter.SortColumn = sort;
        if (query.TryGetValue("sortDir", out var sortDir)) _filter.SortDescending = sortDir == "desc";

        if (query.TryGetValue("q", out var q)) _searchTerm = q.ToString();
        if (query.TryGetValue("sPage", out var sPage) && int.TryParse(sPage, out var sp)) _searchPage = sp;
        if (query.TryGetValue("sSort", out var sSort) && Enum.TryParse<SearchSortOrder>(sSort, out var so)) _searchSortOrder = so;

        if (query.TryGetValue("zoom", out var zoom)) _mapZoom = double.TryParse(zoom, CultureInfo.InvariantCulture, out var z) ? z : null;
        if (query.TryGetValue("lat", out var lat)) _mapLat = double.TryParse(lat, CultureInfo.InvariantCulture, out var la) ? la : null;
        if (query.TryGetValue("lng", out var lng)) _mapLng = double.TryParse(lng, CultureInfo.InvariantCulture, out var lo) ? lo : null;
    }

    private async Task UpdateUrlAsync()
    {
        var parameters = new Dictionary<string, string?>();

        if (_activeTab != "statistics") parameters["tab"] = _activeTab;

        var defaultFrom = DateTime.Today;
        var defaultTo = DateTime.Today.AddDays(1).AddSeconds(-1);
        if (_fromDate != defaultFrom) parameters["from"] = _fromDate.ToString("yyyy-MM-ddTHH:mm:ss");
        if (_toDate != defaultTo) parameters["to"] = _toDate.ToString("yyyy-MM-ddTHH:mm:ss");

        if (_activeTab == "statistics")
        {
            if (_currentPage > 1) parameters["page"] = _currentPage.ToString();
            if (!string.IsNullOrWhiteSpace(_filter.ProxyType)) parameters["fType"] = _filter.ProxyType;
            if (!string.IsNullOrWhiteSpace(_filter.RemoteAddress)) parameters["fAddr"] = _filter.RemoteAddress;
            if (!string.IsNullOrWhiteSpace(_filter.RemotePort)) parameters["fPort"] = _filter.RemotePort;
            if (!string.IsNullOrWhiteSpace(_filter.LocalAddress)) parameters["fLAddr"] = _filter.LocalAddress;
            if (!string.IsNullOrWhiteSpace(_filter.LocalPort)) parameters["fLPort"] = _filter.LocalPort;
            if (!string.IsNullOrWhiteSpace(_filter.IsBlacklisted)) parameters["fBl"] = _filter.IsBlacklisted;
            if (!string.IsNullOrWhiteSpace(_filter.CountryCode)) parameters["fCountry"] = _filter.CountryCode;
            if (!string.IsNullOrWhiteSpace(_filter.SortColumn)) parameters["sort"] = _filter.SortColumn;
            if (_filter.SortColumn != null && !_filter.SortDescending) parameters["sortDir"] = "asc";
        }

        if (_activeTab == "search")
        {
            if (!string.IsNullOrWhiteSpace(_searchTerm)) parameters["q"] = _searchTerm;
            if (_searchPage > 1) parameters["sPage"] = _searchPage.ToString();
            if (_searchSortOrder != SearchSortOrder.Relevance) parameters["sSort"] = _searchSortOrder.ToString();
        }

        if (_activeTab == "worldview")
        {
            try
            {
                var state = await JS.InvokeAsync<MapState?>("worldMap.getState");
                if (state != null)
                {
                    parameters["zoom"] = state.Zoom.ToString("F1", CultureInfo.InvariantCulture);
                    parameters["lat"] = state.Lat.ToString("F4", CultureInfo.InvariantCulture);
                    parameters["lng"] = state.Lng.ToString("F4", CultureInfo.InvariantCulture);
                }
            }
            catch { }
        }

        var url = QueryHelpers.AddQueryString("/", parameters);
        await JS.InvokeVoidAsync("eval", $"history.replaceState(null, '', '{url}')");
    }

    // Map state fields for URL restore
    private double? _mapZoom;
    private double? _mapLat;
    private double? _mapLng;

    private record MapState(double Zoom, double Lat, double Lng);

    private async Task SetTabAsync(string tab)
    {
        if (_activeTab == tab) return;

        if (_activeTab == "worldview")
        {
            try
            {
                var state = await JS.InvokeAsync<MapState?>("worldMap.getState");
                if (state != null)
                {
                    _mapZoom = state.Zoom;
                    _mapLat = state.Lat;
                    _mapLng = state.Lng;
                }
            }
            catch { }
        }

        _activeTab = tab;

        if (tab == "worldview")
            await ActivateWorldViewAsync();

        await UpdateUrlAsync();
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
