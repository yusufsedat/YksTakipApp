using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace YksTakipApp.Api.Helpers
{
    public static class HttpContextExtensions
    {
        public static int? GetUserId(this HttpContext ctx)
        {
            var id = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (id is null) return null;
            return int.TryParse(id, out int parsed) ? parsed : null;
        }

        public static IResult ValidationProblem(this HttpContext ctx, IDictionary<string, string[]> errors)
        {
            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc9110#name-400-bad-request",
                title = "One or more validation errors occurred.",
                status = StatusCodes.Status400BadRequest,
                traceId = ctx.TraceIdentifier,
                errors
            };
            return Results.Json(problem, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
