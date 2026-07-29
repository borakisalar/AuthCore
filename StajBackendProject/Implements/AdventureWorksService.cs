using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using StajBackendProject.Interfaces;
using StajBackendProject.Models;

namespace StajBackendProject.Implements
{
    public class AdventureWorksService : IAdventureWorksService
    {
        private readonly AdventureWorksContext _context;
        private readonly IDistributedCache _cache;

        public AdventureWorksService(AdventureWorksContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<List<SalesOrderDetail>> GetSalesDataFromDatabaseAsync()
        {
            return await _context.SalesOrderDetails.ToListAsync();
        }

        public async Task<List<SalesOrderDetail>> GetSalesDataFromCacheAsync()
        {
            string cacheKey = "AllSalesOrderDetails";
            List<SalesOrderDetail> salesList;

            var cachedData = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                salesList = JsonSerializer.Deserialize<List<SalesOrderDetail>>(cachedData);
            }
            else
            {
                salesList = await _context.SalesOrderDetails.ToListAsync();

                var cacheOptions = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1));

                var serializedData = JsonSerializer.Serialize(salesList);
                await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);
            }

            return salesList;
        }
    }
}