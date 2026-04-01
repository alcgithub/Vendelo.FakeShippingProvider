using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Vendelo.FakeShippingProvider.Models;
using Vendelo.FakeShippingProvider.Services;

namespace Vendelo.FakeShippingProvider.Controllers
{
    [ApiController]
    public class OAuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public OAuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet("/oauth/authorize")]
        public IActionResult Authorize([FromQuery] OAuthAuthorizeRequest request)
        {
            string errorCode;
            string errorMessage;
            if (!_authService.ValidateAuthorizeRequest(request, out errorCode, out errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(request?.redirect_uri))
                {
                    var sep = request.redirect_uri.Contains("?") ? "&" : "?";
                    var stateParam = string.IsNullOrWhiteSpace(request?.state) ? "" : $"&state={Uri.EscapeDataString(request.state)}";
                    var redirectError = $"{request.redirect_uri}{sep}error={Uri.EscapeDataString(errorCode ?? "invalid_request")}{stateParam}";
                    return Redirect(redirectError);
                }

                var status = errorCode == "oauth_disabled" ? 403 : 400;
                return StatusCode(status, new
                {
                    error = errorCode ?? "invalid_request",
                    errors = new
                    {
                        oauth = new[] { errorMessage ?? "Invalid authorize request." }
                    }
                });
            }

            var code = _authService.IssueAuthorizationCode(request);
            var sepOk = request.redirect_uri.Contains("?") ? "&" : "?";
            var stateOk = string.IsNullOrWhiteSpace(request.state) ? "" : $"&state={Uri.EscapeDataString(request.state)}";
            var redirectOk = $"{request.redirect_uri}{sepOk}code={Uri.EscapeDataString(code)}{stateOk}";
            return Redirect(redirectOk);
        }

        [HttpPost("/oauth/token")]
        public async Task<IActionResult> Token()
        {
            OAuthTokenRequest request = null;
            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync();
                request = new OAuthTokenRequest
                {
                    grant_type = form["grant_type"],
                    refresh_token = form["refresh_token"],
                    code = form["code"],
                    redirect_uri = form["redirect_uri"],
                    client_id = form["client_id"],
                    client_secret = form["client_secret"]
                };
            }
            else
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                    request = JsonSerializer.Deserialize<OAuthTokenRequest>(body);
            }

            string errorCode;
            string errorMessage;
            if (!_authService.ValidateOAuthRequest(request, out errorCode, out errorMessage))
            {
                var status = errorCode == "validation_error" ? 422 : 401;
                if (errorCode == "oauth_disabled")
                    status = 403;

                return StatusCode(status, new
                {
                    error = errorCode,
                    errors = new
                    {
                        oauth = new[] { errorMessage }
                    }
                });
            }

            var token = _authService.IssueOAuthToken();
            return Ok(token);
        }
    }
}
