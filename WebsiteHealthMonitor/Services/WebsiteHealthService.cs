using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Authentication;
using WebsiteHealthMonitor.Models;

namespace WebsiteHealthMonitor.Services
{
    public class WebsiteHealthService
    {
        private const int MaxAttempts = 2;
        private const long SlowThresholdMs = 5000;

        private readonly IHttpClientFactory _httpClientFactory;

        public WebsiteHealthService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<WebsiteHealthResult> CheckWebsiteAsync(
            int id, string name, string url, CancellationToken ct = default)
        {
            WebsiteHealthResult result = null!;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                result = await CheckOnceAsync(id, name, url, ct);

                if (result.IsUp)
                    return result;

                if (attempt < MaxAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }

            return result;
        }

        private async Task<WebsiteHealthResult> CheckOnceAsync(
            int id, string name, string url, CancellationToken ct)
        {
            var result = new WebsiteHealthResult
            {
                Id = id,
                Name = name,
                Url = url,
                CheckedAt = DateTime.UtcNow
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var client = _httpClientFactory.CreateClient("HealthCheck");

                using var response = await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);

                stopwatch.Stop();

                int code = (int)response.StatusCode;

                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                result.StatusCode = code;

                if (code >= 500)
                {
                    result.IsUp = false;
                    result.Status = "DOWN";
                    result.ErrorMessage = $"Server returned {code} {response.ReasonPhrase}";
                }
                else if (code >= 400)
                {
                    result.IsUp = true;
                    result.Status = "WARNING";
                    result.ErrorMessage = $"Server returned {code} {response.ReasonPhrase}";
                }
                else if (result.ResponseTimeMs > SlowThresholdMs)
                {
                    result.IsUp = true;
                    result.Status = "SLOW";
                }
                else
                {
                    result.IsUp = true;
                    result.Status = "UP";
                }

                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // caller cancelled (browser closed / app shutdown), not a site problem
            }
            catch (TaskCanceledException)
            {
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                result.Status = "TIMEOUT";
                result.IsUp = false;
                result.ErrorMessage = "Request timed out (server not responding or network blocked).";
                return result;
            }
            catch (HttpRequestException ex)
            {
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                result.IsUp = false;

                var inner = ex.InnerException;

                if (inner is AuthenticationException)
                {
                    result.Status = "SSL_ERROR";
                    result.ErrorMessage = "Certificate/TLS problem: " + inner.Message;
                }
                else if (inner is SocketException se)
                {
                    switch (se.SocketErrorCode)
                    {
                        case SocketError.HostNotFound:
                        case SocketError.TryAgain:
                            result.Status = "DNS_FAILURE";
                            result.ErrorMessage = "Hostname could not be resolved from the monitor's network.";
                            break;
                        case SocketError.ConnectionRefused:
                            result.Status = "DOWN";
                            result.ErrorMessage = "Connection refused (web server/service is not running).";
                            break;
                        case SocketError.TimedOut:
                        case SocketError.HostUnreachable:
                        case SocketError.NetworkUnreachable:
                            result.Status = "UNREACHABLE";
                            result.ErrorMessage = "Host unreachable (network, firewall or routing issue).";
                            break;
                        default:
                            result.Status = "DOWN";
                            result.ErrorMessage = se.Message;
                            break;
                    }
                }
                else
                {
                    result.Status = "DOWN";
                    result.ErrorMessage = inner?.Message ?? ex.Message;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                result.Status = "ERROR";
                result.IsUp = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
    }
}