using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace YksTakipApp.Api.Helpers
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");

                object problem = _env.IsProduction()
                    ? new
                    {
                        type = "https://tools.ietf.org/html/rfc9110#name-500-internal-server-error",
                        title = "An unexpected error occurred.",
                        status = StatusCodes.Status500InternalServerError,
                        traceId = context.TraceIdentifier
                    }
                    : new
                    {
                        type = "https://tools.ietf.org/html/rfc9110#name-500-internal-server-error",
                        title = "An unexpected error occurred.",
                        status = StatusCodes.Status500InternalServerError,
                        traceId = context.TraceIdentifier,
                        detail = ex.Message
                    };

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }
}


