using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace HotelReservationSytemAPI.Middlewares
{
    public class TokenBlacklistMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenBlacklistMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, AppDbContext db)
        {
            // Get Authorization header
            var authHeader = context.Request.Headers["Authorization"].ToString();

            // If there is a Bearer token, check if it's blacklisted
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();

                // Check if token exists in blacklist
                var isBlacklisted = await db.BlackListTokens.AnyAsync(t => t.Token == token);

                if (isBlacklisted)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("This token has been revoked.");
                    return;
                }
            }

            // If no Authorization or not blacklisted → continue request
            await _next(context);
        }
    }
}
