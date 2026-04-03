using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.JSInterop;
using ReverseProxyServer.Data.DTO;

namespace ReverseProxyServer.Web.Services;

public class LoggingDashboardDataService(IDashboardDataService inner, IJSRuntime js) : IDashboardDataService
{
    public async Task<ConnectionStats> GetConnectionStatsAsync(DateTime fromDate, DateTime toDate) =>
        await LogCallAsync(() => inner.GetConnectionStatsAsync(fromDate, toDate));

    public async Task<long> GetUniqueIPsCountAsync(DateTime fromDate, DateTime toDate) =>
        await LogCallAsync(() => inner.GetUniqueIPsCountAsync(fromDate, toDate));

    public async Task<long> GetUniquePortsCountAsync(DateTime fromDate, DateTime toDate) =>
        await LogCallAsync(() => inner.GetUniquePortsCountAsync(fromDate, toDate));

    public async Task<long> GetBlacklistedIPsCountAsync(DateTime fromDate, DateTime toDate) =>
        await LogCallAsync(() => inner.GetBlacklistedIPsCountAsync(fromDate, toDate));

    public async Task<PagedResult<Connection>> GetConnectionsPagedAsync(DateTime fromDate, DateTime toDate, int page, int pageSize, ConnectionFilter? filter = null) =>
        await LogCallAsync(() => inner.GetConnectionsPagedAsync(fromDate, toDate, page, pageSize, filter),
            result => $"{result.Items.Count} of {result.TotalCount} records, page {result.Page}");

    public async Task<List<ConnectionData>> GetConnectionDataAsync(string sessionId) =>
        await LogCallAsync(() => inner.GetConnectionDataAsync(sessionId),
            result => $"{result.Count} items, session {sessionId[..8]}…");

    public async Task<IPDetails> GetIPDetailsAsync(string ipAddress) =>
        await LogCallAsync(() => inner.GetIPDetailsAsync(ipAddress),
            result => ipAddress);

    public async Task<List<CountryConnectionCount>> GetConnectionCountsByCountryAsync(DateTime fromDate, DateTime toDate) =>
        await LogCallAsync(() => inner.GetConnectionCountsByCountryAsync(fromDate, toDate),
            result => $"{result.Count} countries");

    public async Task<List<CountryInfo>> GetDistinctCountriesAsync(DateTime fromDate, DateTime toDate) =>
        await LogCallAsync(() => inner.GetDistinctCountriesAsync(fromDate, toDate),
            result => $"{result.Count} countries");

    public async Task<BandwidthStats> GetBandwidthStatsAsync(DateTime fromDate, DateTime toDate) =>
        await LogCallAsync(() => inner.GetBandwidthStatsAsync(fromDate, toDate));

    public async Task<TopConnectionInfo?> GetTopConnectionAsync(DateTime fromDate, DateTime toDate) =>
        await LogCallAsync(() => inner.GetTopConnectionAsync(fromDate, toDate),
            result => result != null ? $"{result.IPAddress} ({result.Hits} hits)" : "none");

    public async Task<PagedResult<SearchResult>> SearchConnectionDataAsync(string searchTerm, int page, int pageSize, SearchSortOrder sortOrder = SearchSortOrder.Relevance) =>
        await LogCallAsync(() => inner.SearchConnectionDataAsync(searchTerm, page, pageSize, sortOrder),
            result => $"{result.Items.Count} of {result.TotalCount} results, page {result.Page}");

    public async Task<SearchResult?> GetSearchResultMetadataAsync(long connectionDataId) =>
        await LogCallAsync(() => inner.GetSearchResultMetadataAsync(connectionDataId),
            result => result != null ? $"id {connectionDataId}" : $"id {connectionDataId} not found");

    public async Task<string?> GetHighlightedDataAsync(long connectionDataId, string searchTerm) =>
        await LogCallAsync(() => inner.GetHighlightedDataAsync(connectionDataId, searchTerm),
            result => result != null ? $"{result.Length} chars" : "no data");

    private async Task<T> LogCallAsync<T>(Func<Task<T>> action, Func<T, string>? summarize = null, [CallerMemberName] string method = "")
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action();
            sw.Stop();
            var summary = summarize != null ? $" → {summarize(result)}" : string.Empty;
            await LogAsync($"📊 {method} completed in {sw.ElapsedMilliseconds}ms{summary}");
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            await LogAsync($"❌ {method} failed after {sw.ElapsedMilliseconds}ms: {ex.Message}", isError: true);
            throw;
        }
    }

    private async Task LogAsync(string message, bool isError = false)
    {
        try
        {
            var jsMethod = isError ? "console.error" : "console.log";
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            await js.InvokeVoidAsync("eval", $"{jsMethod}('[{timestamp}] {message.Replace("'", "\\'")}')");
        }
        catch { }
    }
}
