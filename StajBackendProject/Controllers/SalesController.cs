using Microsoft.AspNetCore.Mvc;
using StajBackendProject.Implements;
using StajBackendProject.Interfaces;

namespace StajBackendProject.Controllers
{
    [Route("api/sales")]
    [ApiController]
    public class SalesController : ControllerBase
    {
        private readonly IAdventureWorksService _salesService;

        public SalesController(IAdventureWorksService salesService)
        {
            _salesService = salesService;
        }

        /// <summary>
        /// Retrieves sales data directly from the primary relational database (AdventureWorks).
        /// </summary>
        /// <remarks>
        /// This endpoint bypasses the caching layer and executes a live SQL query against the `AdventureWorksContext`. 
        /// It guarantees real-time accuracy but may incur higher latency compared to the cache endpoint.
        /// </remarks>
        /// <returns>A collection of sales records fetched directly from the SQL Server database.</returns>
        /// <response code="200">Sales records successfully retrieved from the database.</response>
        /// <response code="401">Unauthorized access (A valid JWT Bearer token is required).</response>
        /// <response code="500">A database connectivity or query execution error occurred.</response>
        [HttpGet("database")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDataFromDatabase()
        {
            var data = await _salesService.GetSalesDataFromDatabaseAsync();
            return Ok(new { Count = data.Count, Source = "SQL Server - Slow" });
        }

        /// <summary>
        /// Retrieves sales data from the high-performance distributed cache layer (Redis).
        /// </summary>
        /// <remarks>
        /// Implements a **Cache-Aside Pattern**:
        /// 1. Attempts to fetch serialized sales data from the Redis cache (`StajProject_` instance).
        /// 2. If a **Cache Hit** occurs, data is returned immediately with sub-millisecond latency.
        /// 3. If a **Cache Miss** occurs, data is retrieved from the AdventureWorks database, populated into Redis with a Time-To-Live (TTL), and then returned.
        /// </remarks>
        /// <returns>A collection of sales records served from the Redis cache or fallback database.</returns>
        /// <response code="200">Sales records successfully retrieved from the cache (or populated on miss).</response>
        /// <response code="401">Unauthorized access (A valid JWT Bearer token is required).</response>
        /// <response code="500">An unexpected error occurred while reading from the Redis instance or fallback database.</response>
        [HttpGet("cache")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDataFromCache()
        {
            var data = await _salesService.GetSalesDataFromCacheAsync();
            return Ok(new { Count = data.Count, Source = "Redis Cache - Fast" });
        }
    }
}