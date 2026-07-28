using Microsoft.AspNetCore.Mvc;
using StajBackendProject.Implements;

namespace StajBackendProject.Controllers
{
    [Route("api/sales")]
    [ApiController]
    public class SalesController : ControllerBase
    {
        private readonly AdventureWorksService _salesService;

        public SalesController(AdventureWorksService salesService)
        {
            _salesService = salesService;
        }

        [HttpGet("database")]
        public async Task<IActionResult> GetDataFromDatabase()
        {
            var data = await _salesService.GetSalesDataFromDatabaseAsync();
            return Ok(new { Count = data.Count, Source = "SQL Server - Slow" });
        }

        [HttpGet("cache")]
        public async Task<IActionResult> GetDataFromCache()
        {
            var data = await _salesService.GetSalesDataFromCacheAsync();
            return Ok(new { Count = data.Count, Source = "Redis Cache - Fast" });
        }
    }
}