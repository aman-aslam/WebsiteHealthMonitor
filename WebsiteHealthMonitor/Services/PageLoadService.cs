using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Playwright;

namespace WebsiteHealthMonitor.Services
{
    public class PageLoadResult
    {
        public string Url { get; set; } = "";
        public long? DomContentLoadedMs { get; set; }
        public long? LoadMs { get; set; }
        public long? FinishMs { get; set; }
        public bool StillLoading { get; set; }   // true = some request never finished within the cap
        public int RequestCount { get; set; }
        public int FailedRequestCount { get; set; }
        public string? Error { get; set; }
    }

    public class PageLoadService : IAsyncDisposable
    {
        private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan MaxFinishWait = TimeSpan.FromSeconds(30);
        private const int NavigationTimeoutMs = 45_000;

        // Connections that stay open on purpose must not block "finished"
        private static readonly HashSet<string> IgnoredTypes =
            new(StringComparer.OrdinalIgnoreCase) { "websocket", "eventsource", "ping" };

        private readonly SemaphoreSlim _browserLock = new(1, 1);
        private readonly SemaphoreSlim _measureLock = new(1, 1); // one measurement at a time

        private IPlaywright? _playwright;
        private IBrowser? _browser;

        // Kept so your existing controller code still compiles. Returns Finish time in ms.
        public async Task<long> GetPageLoadTimeAsync(string url)
        {
            var result = await MeasureAsync(url);

            if (result.Error != null)
                throw new InvalidOperationException(result.Error);

            return result.FinishMs ?? 0;
        }

        public async Task<PageLoadResult> MeasureAsync(string url)
        {
            var result = new PageLoadResult { Url = url };

            // Parallel measurements would slow each other down and skew the numbers
            await _measureLock.WaitAsync();

            try
            {
                var browser = await GetBrowserAsync();

                // New context = empty cache, same as "Disable cache" in DevTools
                await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent =
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                        "(KHTML, like Gecko) Chrome/124.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1366, Height = 768 },
                    Locale = "en-IN"
                });

                var page = await context.NewPageAsync();

                var pending = new ConcurrentDictionary<IRequest, byte>();
                var sw = new Stopwatch();

                long lastDoneMs = 0;   // time the most recent request finished or failed
                int total = 0;
                int failed = 0;

                // Two discards (_, _) are real discards, so these are fine
                page.DOMContentLoaded += (_, _) => result.DomContentLoadedMs = sw.ElapsedMilliseconds;
                page.Load += (_, _) => result.LoadMs = sw.ElapsedMilliseconds;

                // NOTE: first parameter is named "sender", NOT "_", so "out _" below is a true discard
                page.Request += (sender, request) =>
                {
                    if (IsIgnored(request)) return;

                    Interlocked.Increment(ref total);
                    pending[request] = 0;
                };

                page.RequestFinished += (sender, request) =>
                {
                    if (pending.TryRemove(request, out _))
                        Interlocked.Exchange(ref lastDoneMs, sw.ElapsedMilliseconds);
                };

                page.RequestFailed += (sender, request) =>
                {
                    if (pending.TryRemove(request, out _))
                    {
                        Interlocked.Increment(ref failed);
                        Interlocked.Exchange(ref lastDoneMs, sw.ElapsedMilliseconds);
                    }
                };

                sw.Start();

                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = NavigationTimeoutMs
                });

                // Wait until nothing is in flight AND nothing finished for QuietPeriod
                while (sw.Elapsed < MaxFinishWait)
                {
                    if (pending.IsEmpty)
                    {
                        long idleFor = sw.ElapsedMilliseconds - Interlocked.Read(ref lastDoneMs);

                        if (idleFor >= QuietPeriod.TotalMilliseconds)
                            break;
                    }

                    await Task.Delay(100);
                }

                result.StillLoading = !pending.IsEmpty;

                // Finish = when the LAST request completed (not including the quiet wait)
                result.FinishMs = result.StillLoading
                    ? sw.ElapsedMilliseconds
                    : Interlocked.Read(ref lastDoneMs);

                result.RequestCount = total;
                result.FailedRequestCount = failed;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            finally
            {
                _measureLock.Release();
            }

            return result;
        }

        private static bool IsIgnored(IRequest request) =>
            request.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            request.Url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
            IgnoredTypes.Contains(request.ResourceType);

        private async Task<IBrowser> GetBrowserAsync()
        {
            if (_browser is { IsConnected: true })
                return _browser;

            await _browserLock.WaitAsync();

            try
            {
                if (_browser is { IsConnected: true })
                    return _browser;

                _playwright ??= await Playwright.CreateAsync();

                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

                return _browser;
            }
            finally
            {
                _browserLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_browser != null)
                await _browser.CloseAsync();

            _playwright?.Dispose();
            _browserLock.Dispose();
            _measureLock.Dispose();
        }
    }
}