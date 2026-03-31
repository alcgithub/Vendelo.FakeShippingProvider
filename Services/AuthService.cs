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
            if (p.StartsWith("/debug"))
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

            if (!string.Equals(request.grant_type, "refresh_token", StringComparison.Ordinal))
            {
                errorCode = "validation_error";
                errorMessage = "grant_type must be refresh_token.";
                return false;
            }

            if (request.client_id != _options.Auth.OAuthClientId || request.client_secret != _options.Auth.OAuthClientSecret)
            {
                errorCode = "invalid_client";
                errorMessage = "Invalid client_id or client_secret.";
                return false;
            }

            if (request.refresh_token != _options.Auth.OAuthRefreshToken)
            {
                errorCode = "invalid_grant";
                errorMessage = "Invalid refresh token.";
                return false;
            }

            return true;
        }
    }
}

