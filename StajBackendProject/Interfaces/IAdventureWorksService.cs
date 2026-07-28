using StajBackendProject.Models;

namespace StajBackendProject.Interfaces
{
    public interface IAdventureWorksService
    {
        Task<List<SalesOrderDetail>> GetSalesDataFromDatabaseAsync();
        Task<List<SalesOrderDetail>> GetSalesDataFromCacheAsync();
    }
}