using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Vendelo.FakeShippingProvider.Services;

namespace Vendelo.FakeShippingProvider.Middleware
{
    public class BearerAuthMiddleware
    {
        private readonly RequestDelegate _next;

        public BearerAuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, IAuthService authService)
        {
            var path = context.Request.Path.Value ?? "";
            if (!authService.IsProtectedRoute(path))
            {
                await _next(context);
                return;
            }

            var authorization = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "unauthorized",
                    errors = new
                    {
                        authorization = new[] { "Missing or invalid Bearer token." }
                    }
                });
                return;
            }

            var token = authorization.Substring("Bearer ".Length).Trim();
            if (!authService.IsTokenAccepted(token))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "unauthorized",
                    errors = new
                    {
                        authorization = new[] { "Token rejected. Validate STATIC_TOKEN or OAuth settings." }
                    }
                });
                return;
            }

            await _next(context);
        }
    }
}

