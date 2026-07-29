using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StajBackendProject.Models;

namespace StajBackendProject.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var userIdString = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userEmail = context.User.FindFirstValue(ClaimTypes.Email) ?? context.User.FindFirstValue(ClaimTypes.Name);

                int? userId = null;
                if (int.TryParse(userIdString, out int parsedId))
                {
                    userId = parsedId;
                }

                var statusCode = (int)HttpStatusCode.InternalServerError;
                if (ex is InvalidOperationException) statusCode = (int)HttpStatusCode.Conflict;
                else if (ex is ArgumentException) statusCode = (int)HttpStatusCode.BadRequest;
                else if (ex is UnauthorizedAccessException) statusCode = (int)HttpStatusCode.Unauthorized;

                await LogErrorToDatabaseAsync(context, userId, userEmail, ex, statusCode);

                Log.Error(ex, "Error in HTTP {Method} {Path} endpoint! User ID: {UserId}, Email: {UserEmail}",
                    context.Request.Method, context.Request.Path, userId ?? 0, userEmail ?? "Anonim");

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = statusCode;

                var response = new
                {
                    StatusCode = statusCode,
                    Message = ex.Message,
                    Detail = "A system error occurred and it was reported to the technical team."
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }

        private async Task LogErrorToDatabaseAsync(HttpContext context, int? userId, string? userEmail, Exception ex, int statusCode)
        {
            try
            {
                var dbContext = context.RequestServices.GetRequiredService<UsersContext>();

                var errorLog = new ErrorLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Path = context.Request.Path,
                    Method = context.Request.Method,
                    ExceptionMessage = ex.Message,
                    StackTrace = ex.StackTrace,
                    StatusCode = statusCode,
                    Timestamp = DateTime.UtcNow
                };

                dbContext.ErrorLogs.Add(errorLog);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                Log.Fatal(dbEx, "The error could not be saved to the database!");
            }
        }
    }
}