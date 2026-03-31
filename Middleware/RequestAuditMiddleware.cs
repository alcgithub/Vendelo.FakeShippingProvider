using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Vendelo.FakeShippingProvider.Middleware
{
    public class RequestAuditMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestAuditMiddleware> _logger;

        public RequestAuditMiddleware(RequestDelegate next, ILogger<RequestAuditMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var requestId = Guid.NewGuid().ToString("N");
            context.Items["requestId"] = requestId;

            context.Request.EnableBuffering();
            var body = await ReadBody(context.Request);
            context.Request.Body.Position = 0;

            _logger.LogInformation(
                "REQ {Method} {Path} requestId={RequestId} auth={Auth} body={Body}",
                context.Request.Method,
                context.Request.Path,
                requestId,
                context.Request.Headers["Authorization"].ToString(),
                body
            );

            await _next(context);

            _logger.LogInformation(
                "RES {Method} {Path} requestId={RequestId} status={StatusCode}",
                context.Request.Method,
                context.Request.Path,
                requestId,
                context.Response.StatusCode
            );
        }

        private static async Task<string> ReadBody(HttpRequest request)
        {
            if (request.ContentLength == null || request.ContentLength == 0)
                return "";

            using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            {
                var raw = await reader.ReadToEndAsync();
                if (raw.Length > 4000)
                    return raw.Substring(0, 4000) + "...(truncated)";
                return raw;
            }
        }
    }
}

