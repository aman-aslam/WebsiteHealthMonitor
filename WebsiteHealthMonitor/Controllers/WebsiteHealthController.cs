using Microsoft.AspNetCore.Mvc;
using WebsiteHealthMonitor.Models;
using WebsiteHealthMonitor.Services;

namespace WebsiteHealthMonitor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebsiteHealthController : ControllerBase
    {
        private readonly WebsiteHealthService _healthService;
        private readonly PageLoadService _pageLoadService;

        public WebsiteHealthController(
            WebsiteHealthService healthService, PageLoadService pageLoadService)
        {
            _healthService = healthService;
            _pageLoadService = pageLoadService;
        }

        // List of sites only (no checking), so the page can draw cards instantly
        [HttpGet("sites")]
        public ActionResult GetSites() =>
            Ok(WebsiteCatalog.All.Select(w => new { id = w.Id, name = w.Name, url = w.Url }));

        [HttpGet("check-all")]
        public async Task<ActionResult<WebsiteHealthResult[]>> CheckAll(CancellationToken ct)
        {
            var tasks = WebsiteCatalog.All.Select(w =>
                _healthService.CheckWebsiteAsync(w.Id, w.Name, w.Url, ct));

            return Ok(await Task.WhenAll(tasks));
        }

        [HttpGet("check/{id:int}")]
        public async Task<ActionResult<WebsiteHealthResult>> CheckWebsite(int id, CancellationToken ct)
        {
            var site = WebsiteCatalog.All.FirstOrDefault(w => w.Id == id);

            if (site.Id == 0)
                return NotFound(new { message = "Website not found." });

            var result = await _healthService.CheckWebsiteAsync(site.Id, site.Name, site.Url, ct);

            return Ok(result);
        }

        [HttpGet("page-load-test")]
        public async Task<IActionResult> PageLoadTest([FromQuery] int id = 1)
        {
            var site = WebsiteCatalog.All.FirstOrDefault(w => w.Id == id);

            if (site.Id == 0)
                return NotFound(new { message = "Website not found." });

            var result = await _pageLoadService.MeasureAsync(site.Url);

            if (result.Error != null)
                return StatusCode(500, new { id = site.Id, url = site.Url, error = result.Error });

            return Ok(new
            {
                id = site.Id,
                url = site.Url,
                domContentLoadedMs = result.DomContentLoadedMs,
                loadMs = result.LoadMs,
                finishMs = result.FinishMs,
                stillLoading = result.StillLoading,
                requestCount = result.RequestCount,
                failedRequestCount = result.FailedRequestCount,
                measuredAt = DateTime.UtcNow
            });
        }
    }
}