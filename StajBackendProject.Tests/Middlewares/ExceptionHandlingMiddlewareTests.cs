using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using FluentAssertions;
using StajBackendProject.Middlewares;
using StajBackendProject.Models;
using StajBackendProject.Models.Dto;
using StajBackendProject.Interfaces;

namespace StajBackendProject.Tests.Middlewares
{
    public class ExceptionHandlingMiddlewareTests : IDisposable
    {
        private readonly UsersContext _context;
        private readonly IServiceProvider _serviceProvider;

        public ExceptionHandlingMiddlewareTests()
        {
            var options = new DbContextOptionsBuilder<UsersContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new UsersContext(options);

            var services = new ServiceCollection();
            services.AddScoped(sp => _context);
            _serviceProvider = services.BuildServiceProvider();
        }

        private DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext
            {
                RequestServices = _serviceProvider
            };
            context.Response.Body = new MemoryStream();
            return context;
        }

        private async Task<JsonDocument> ReadResponseBodyAsync(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            var bodyText = await reader.ReadToEndAsync();
            return JsonDocument.Parse(bodyText);
        }

        [Fact]
        public async Task InvokeAsync_NoException_ShouldCallNextDelegate()
        {
            var context = CreateContext();
            bool nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            nextCalled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task InvokeAsync_GenericException_ShouldReturn500()
        {
            var context = CreateContext();
            RequestDelegate next = (ctx) => throw new Exception("Generic error");
            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task InvokeAsync_InvalidOperationException_ShouldReturn409()
        {
            var context = CreateContext();
            RequestDelegate next = (ctx) => throw new InvalidOperationException("Conflict error");
            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be(409);
        }

        [Fact]
        public async Task InvokeAsync_ArgumentException_ShouldReturn400()
        {
            var context = CreateContext();
            RequestDelegate next = (ctx) => throw new ArgumentException("Bad request error");
            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task InvokeAsync_UnauthorizedAccessException_ShouldReturn401()
        {
            var context = CreateContext();
            RequestDelegate next = (ctx) => throw new UnauthorizedAccessException("Unauthorized error");
            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task InvokeAsync_ShouldSaveErrorLogToDatabase()
        {
            var context = CreateContext();
            context.Request.Path = "/api/test";
            context.Request.Method = "GET";
            
            RequestDelegate next = (ctx) => throw new Exception("Db error log test");
            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            var log = await _context.ErrorLogs.FirstOrDefaultAsync();
            log.Should().NotBeNull();
            log.ExceptionMessage.Should().Be("Db error log test");
            log.Path.Should().Be("/api/test");
            log.Method.Should().Be("GET");
            log.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task InvokeAsync_ShouldExtractUserIdAndEmailIfAuthenticated()
        {
            var context = CreateContext();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "123"),
                new Claim(ClaimTypes.Email, "test@example.com")
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));
            
            RequestDelegate next = (ctx) => throw new Exception("Auth test");
            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            var log = await _context.ErrorLogs.FirstOrDefaultAsync();
            log.Should().NotBeNull();
            log.UserId.Should().Be(123);
            log.UserEmail.Should().Be("test@example.com");
        }
        
        [Fact]
        public async Task InvokeAsync_ShouldExtractEmailFromNameClaimIfEmailClaimMissing()
        {
            var context = CreateContext();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "123"),
                new Claim(ClaimTypes.Name, "test@example.com")
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));
            
            RequestDelegate next = (ctx) => throw new Exception("Auth test 2");
            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            var log = await _context.ErrorLogs.FirstOrDefaultAsync();
            log.Should().NotBeNull();
            log.UserEmail.Should().Be("test@example.com");
        }

        [Fact]
        public async Task InvokeAsync_ResponseBody_ShouldContainCorrectJsonStructure()
        {
            var context = CreateContext();
            RequestDelegate next = (ctx) => throw new ArgumentException("Bad args");
            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            var json = await ReadResponseBodyAsync(context);
            var root = json.RootElement;
            
            root.TryGetProperty("StatusCode", out _).Should().BeTrue();
            root.TryGetProperty("Message", out _).Should().BeTrue();
            root.TryGetProperty("Detail", out _).Should().BeTrue();
            
            root.GetProperty("StatusCode").GetInt32().Should().Be(400);
        }

        [Fact]
        public async Task InvokeAsync_NullUser_ShouldLogNullUserIdAndEmail()
        {
            var context = CreateContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity()); // Unauthenticated
            
            RequestDelegate next = (ctx) => throw new Exception("No auth");
            var middleware = new ExceptionHandlingMiddleware(next);

            await middleware.InvokeAsync(context);

            var log = await _context.ErrorLogs.FirstOrDefaultAsync();
            log.Should().NotBeNull();
            log.UserId.Should().BeNull();
            log.UserEmail.Should().BeNull();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
