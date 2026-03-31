using Microsoft.AspNetCore.Mvc;
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

        [HttpPost("/oauth/token")]
        public IActionResult Token([FromBody] OAuthTokenRequest request)
        {
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

