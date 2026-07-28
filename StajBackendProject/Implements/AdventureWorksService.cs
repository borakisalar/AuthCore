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

        // 1. Geleneksel Yöntem: Doğrudan Veritabanından (SQL) Çekme - YAVAŞ
        public async Task<List<SalesOrderDetail>> GetSalesDataFromDatabaseAsync()
        {
            // 112.000 satırın tamamını doğrudan SQL'den çekiyoruz
            return await _context.SalesOrderDetails.ToListAsync();
        }

        // 2. Modern Yöntem: Redis (RAM) Üzerinden Çekme - ÇOK HIZLI
        public async Task<List<SalesOrderDetail>> GetSalesDataFromCacheAsync()
        {
            string cacheKey = "AllSalesOrderDetails";
            List<SalesOrderDetail> salesList;

            // 1. Adım: Önce Redis'e sor! Veri önbellekte var mı?
            var cachedData = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                // Veri Redis'te var! Saniyenin onda biri sürede JSON'dan çevir ve dön
                salesList = JsonSerializer.Deserialize<List<SalesOrderDetail>>(cachedData);
            }
            else
            {
                // 2. Adım: Veri Redis'te YOK. Mecburen veritabanına git (Saniyeler sürer)
                salesList = await _context.SalesOrderDetails.ToListAsync();

                // 3. Adım: Bir dahaki sefere hızlı çekebilmek için SQL'den gelen veriyi Redis'e yaz!
                var cacheOptions = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1)); // 1 saat boyunca silinmesin

                // Veriyi JSON formatına çevirip Redis'e kaydediyoruz
                var serializedData = JsonSerializer.Serialize(salesList);
                await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);
            }

            return salesList;
        }
    }
}