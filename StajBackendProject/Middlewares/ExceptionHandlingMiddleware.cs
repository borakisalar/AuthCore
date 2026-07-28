using System.Net;
using System.Text.Json;

namespace StajBackendProject.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }   

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in the system.: {Message}. Detail: {StackTrace}", ex.Message, ex.StackTrace);
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var statusCode = exception switch
            {
                InvalidOperationException => (int)HttpStatusCode.Conflict,          // 409
                ArgumentException => (int)HttpStatusCode.BadRequest,                // 400
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,    // 401
                _ => (int)HttpStatusCode.InternalServerError                        // 500
            };

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            string message = exception.Message;

            if (statusCode == (int)HttpStatusCode.InternalServerError)
            {
                message = "An unexpected error occurred on the server side. Please try again later.";
            }

            var response = new
            {
                Message = "An error occurred on the server side.",
                Error = exception.Message
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}