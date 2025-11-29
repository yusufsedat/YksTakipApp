namespace YksTakipApp.Api.Helpers
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
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

                var problem = new
                {
                    type = "https://tools.ietf.org/html/rfc9110#name-500-internal-server-error",
                    title = "An unexpected error occurred.",
                    status = StatusCodes.Status500InternalServerError,
                    traceId = context.TraceIdentifier
                };

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }
}


