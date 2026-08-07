using AuthCore.Models;

namespace AuthCore.Interfaces
{
    public interface IAdventureWorksService
    {
        Task<List<SalesOrderDetail>> GetSalesDataFromDatabaseAsync();
        Task<List<SalesOrderDetail>> GetSalesDataFromCacheAsync();
    }
}