using System;
using Vendelo.FakeShippingProvider.Models;
using Vendelo.FakeShippingProvider.Options;

namespace Vendelo.FakeShippingProvider.Services
{
    public interface IAuthService
    {
        bool IsProtectedRoute(string path);
        bool IsTokenAccepted(string bearerToken);
        ShippingProviderTokenResponse IssueOAuthToken();
        string IssueAuthorizationCode(OAuthAuthorizeRequest request);
        bool ValidateAuthorizeRequest(OAuthAuthorizeRequest request, out string errorCode, out string errorMessage);
        bool ValidateOAuthRequest(OAuthTokenRequest request, out string errorCode, out string errorMessage);
    }

    public class AuthService : IAuthService
    {
        private readonly AppOptions _options;
        private readonly IDataStore _store;

        public AuthService(AppOptions options, IDataStore store)
        {
            _options = options;
            _store = store;
        }

        public bool IsProtectedRoute(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var p = path.ToLowerInvariant();
            if (p.StartsWith("/health"))
                return false;
            if (p.StartsWith("/oauth/token"))
                return false;
            if (p.StartsWith("/oauth/authorize"))
                return false;
            if (p.StartsWith("/debug"))
                return false;
            if (p.StartsWith("/tracking/"))
                return false;

            return true;
        }

        public bool IsTokenAccepted(string bearerToken)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return false;

            var mode = (_options.Auth.Mode ?? "both").Trim().ToLowerInvariant();
            var allowStatic = mode == "both" || mode == "static";
            var allowOAuth = mode == "both" || mode == "oauth";
            if (allowStatic && bearerToken == _options.Auth.StaticToken)
                return true;

            if (allowOAuth)
            {
                if (bearerToken == _options.Auth.OAuthAccessToken)
                    return true;

                var db = _store.Read();
                if (db?.oauth?.accessToken == bearerToken)
                    return true;
            }

            return false;
        }

        public ShippingProviderTokenResponse IssueOAuthToken()
        {
            var state = _store.Read();
            state.oauth = new OAuthState
            {
                accessToken = _options.Auth.OAuthAccessToken,
                refreshToken = _options.Auth.OAuthRefreshToken,
                expiresIn = _options.Auth.OAuthExpiresIn,
                tokenType = _options.Auth.OAuthTokenType,
                issuedAtUtc = DateTime.UtcNow.ToString("O")
            };
            _store.Write(state);

            return new ShippingProviderTokenResponse
            {
                token_type = state.oauth.tokenType,
                access_token = state.oauth.accessToken,
                refresh_token = state.oauth.refreshToken,
                expires_in = state.oauth.expiresIn
            };
        }

        public string IssueAuthorizationCode(OAuthAuthorizeRequest request)
        {
            var code = $"fake-code-{Guid.NewGuid():N}";
            var state = _store.Read();
            state.oauthCodes ??= new System.Collections.Generic.Dictionary<string, OAuthAuthorizationCodeState>();
            state.oauthCodes[code] = new OAuthAuthorizationCodeState
            {
                code = code,
                clientId = request.client_id,
                redirectUri = NormalizeRedirectUri(request.redirect_uri),
                scope = request.scope,
                issuedAtUtc = DateTime.UtcNow.ToString("O")
            };
            _store.Write(state);
            return code;
        }

        public bool ValidateAuthorizeRequest(OAuthAuthorizeRequest request, out string errorCode, out string errorMessage)
        {
            errorCode = null;
            errorMessage = null;
            var mode = (_options.Auth.Mode ?? "both").Trim().ToLowerInvariant();
            if (!(mode == "both" || mode == "oauth"))
            {
                errorCode = "oauth_disabled";
                errorMessage = "Set AUTH_MODE=oauth or AUTH_MODE=both.";
                return false;
            }

            if (request == null)
            {
                errorCode = "validation_error";
                errorMessage = "Query is required.";
                return false;
            }

            if (!string.Equals(request.response_type, "code", StringComparison.Ordinal))
            {
                errorCode = "unsupported_response_type";
                errorMessage = "response_type must be code.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.client_id) || request.client_id != _options.Auth.OAuthClientId)
            {
                errorCode = "invalid_client";
                errorMessage = "Invalid client_id.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.redirect_uri))
            {
                errorCode = "invalid_request";
                errorMessage = "redirect_uri is required.";
                return false;
            }

            var expectedRedirectUri = NormalizeRedirectUri(_options.Auth.OAuthRedirectUri);
            if (!string.IsNullOrWhiteSpace(expectedRedirectUri))
            {
                var currentRedirectUri = NormalizeRedirectUri(request.redirect_uri);
                if (!string.Equals(currentRedirectUri, expectedRedirectUri, StringComparison.OrdinalIgnoreCase))
                {
                    errorCode = "invalid_request";
                    errorMessage = "redirect_uri is not allowed by Auth__OAuthRedirectUri.";
                    return false;
                }
            }

            return true;
        }

        public bool ValidateOAuthRequest(OAuthTokenRequest request, out string errorCode, out string errorMessage)
        {
            errorCode = null;
            errorMessage = null;
            var mode = (_options.Auth.Mode ?? "both").Trim().ToLowerInvariant();
            if (!(mode == "both" || mode == "oauth"))
            {
                errorCode = "oauth_disabled";
                errorMessage = "Set AUTH_MODE=oauth or AUTH_MODE=both.";
                return false;
            }

            if (request == null)
            {
                errorCode = "validation_error";
                errorMessage = "Body is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.grant_type))
            {
                errorCode = "validation_error";
                errorMessage = "grant_type is required.";
                return false;
            }

            if (request.client_id != _options.Auth.OAuthClientId || request.client_secret != _options.Auth.OAuthClientSecret)
            {
                errorCode = "invalid_client";
                errorMessage = "Invalid client_id or client_secret.";
                return false;
            }

            if (string.Equals(request.grant_type, "refresh_token", StringComparison.Ordinal))
            {
                if (request.refresh_token != _options.Auth.OAuthRefreshToken)
                {
                    errorCode = "invalid_grant";
                    errorMessage = "Invalid refresh token.";
                    return false;
                }

                return true;
            }

            if (string.Equals(request.grant_type, "authorization_code", StringComparison.Ordinal))
            {
                var db = _store.Read();
                db.oauthCodes ??= new System.Collections.Generic.Dictionary<string, OAuthAuthorizationCodeState>();

                if (string.IsNullOrWhiteSpace(request.code))
                {
                    errorCode = "invalid_grant";
                    errorMessage = "Code is required.";
                    return false;
                }

                if (!db.oauthCodes.TryGetValue(request.code, out var codeState) || codeState == null)
                {
                    errorCode = "invalid_grant";
                    errorMessage = "Invalid or expired authorization code.";
                    return false;
                }

                if (!string.Equals(codeState.clientId, request.client_id, StringComparison.Ordinal))
                {
                    errorCode = "invalid_grant";
                    errorMessage = "Authorization code does not match client_id.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(request.redirect_uri) &&
                    !string.Equals(codeState.redirectUri, NormalizeRedirectUri(request.redirect_uri), StringComparison.OrdinalIgnoreCase))
                {
                    errorCode = "invalid_grant";
                    errorMessage = "redirect_uri does not match authorization request.";
                    return false;
                }

                db.oauthCodes.Remove(request.code); // One-time use auth code.
                _store.Write(db);
                return true;
            }

            errorCode = "unsupported_grant_type";
            errorMessage = "grant_type must be refresh_token or authorization_code.";
            return false;
        }

        private string NormalizeRedirectUri(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            return value.Trim().TrimEnd('/');
        }
    }
}
