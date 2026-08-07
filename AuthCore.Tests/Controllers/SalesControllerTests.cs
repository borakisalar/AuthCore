using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using AuthCore.Controllers;
using AuthCore.Interfaces;
using AuthCore.Models;
using Xunit;

namespace AuthCore.Tests.Controllers
{
    public class SalesControllerTests
    {
        private readonly Mock<IAdventureWorksService> _mockAdventureWorksService;
        private readonly SalesController _controller;

        public SalesControllerTests()
        {
            _mockAdventureWorksService = new Mock<IAdventureWorksService>();
            _controller = new SalesController(_mockAdventureWorksService.Object);
        }

        [Fact]
        public async Task GetDataFromDatabase_ReturnsOk_WithDataAndSource()
        {
            var salesData = new List<SalesOrderDetail>
            {
                new SalesOrderDetail { SalesOrderID = 1, SalesOrderDetailID = 1 },
                new SalesOrderDetail { SalesOrderID = 1, SalesOrderDetailID = 2 }
            };

            _mockAdventureWorksService.Setup(s => s.GetSalesDataFromDatabaseAsync())
                .ReturnsAsync(salesData);

            var result = await _controller.GetDataFromDatabase();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new
            {
                Count = 2,
                Source = "SQL Server - Slow"
            });
        }

        [Fact]
        public async Task GetDataFromDatabase_EmptyData_ReturnsOk_WithZeroCount()
        {
            _mockAdventureWorksService.Setup(s => s.GetSalesDataFromDatabaseAsync())
                .ReturnsAsync(new List<SalesOrderDetail>());

            var result = await _controller.GetDataFromDatabase();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var valType = okResult.Value.GetType();
            var count = (int)valType.GetProperty("Count").GetValue(okResult.Value);
            count.Should().Be(0);
        }

        [Fact]
        public async Task GetDataFromCache_ReturnsOk_WithDataAndSource()
        {
            var salesData = new List<SalesOrderDetail>
            {
                new SalesOrderDetail { SalesOrderID = 2, SalesOrderDetailID = 3 }
            };

            _mockAdventureWorksService.Setup(s => s.GetSalesDataFromCacheAsync())
                .ReturnsAsync(salesData);

            var result = await _controller.GetDataFromCache();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new
            {
                Count = 1,
                Source = "Redis Cache - Fast"
            });
        }

        [Fact]
        public async Task GetDataFromCache_EmptyData_ReturnsOk_WithZeroCount()
        {
            _mockAdventureWorksService.Setup(s => s.GetSalesDataFromCacheAsync())
                .ReturnsAsync(new List<SalesOrderDetail>());

            var result = await _controller.GetDataFromCache();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var valType = okResult.Value.GetType();
            var count = (int)valType.GetProperty("Count").GetValue(okResult.Value);
            count.Should().Be(0);
        }

        [Fact]
        public async Task GetDataFromDatabase_ServiceThrows_ThrowsException()
        {
            _mockAdventureWorksService.Setup(s => s.GetSalesDataFromDatabaseAsync())
                .ThrowsAsync(new Exception("Database error"));

            await Assert.ThrowsAsync<Exception>(() => _controller.GetDataFromDatabase());
        }
    }
}
