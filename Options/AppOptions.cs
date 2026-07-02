using System.Collections.Generic;

namespace Vendelo.FakeShippingProvider.Options
{
    public class AppOptions
    {
        public AuthOptions Auth { get; set; } = new AuthOptions();
        public BehaviorOptions Behavior { get; set; } = new BehaviorOptions();
        public StorageOptions Storage { get; set; } = new StorageOptions();
    }

    public class AuthOptions
    {
        public string Mode { get; set; } = "both";
        public string StaticToken { get; set; } = "vendelo-static-token";
        public string OAuthClientId { get; set; } = "vendelo-client";
        public string OAuthClientSecret { get; set; } = "vendelo-secret";
        public string OAuthRedirectUri { get; set; } = "";
        public string OAuthRefreshToken { get; set; } = "vendelo-oauth-refresh-token";
        public string OAuthAccessToken { get; set; } = "vendelo-oauth-access-token";
        public long OAuthExpiresIn { get; set; } = 864000;
        public string OAuthTokenType { get; set; } = "Bearer";
    }

    public class BehaviorOptions
    {
        public bool EnableDebugRoutes { get; set; } = true;
        public string ForceErrorForService { get; set; } = "";

        // Nomes de user_fields que podem receber valor fake na resposta.
        // Se vazia, nenhum campo é alterado (todos voltam com o valor original).
        public List<string> FakeUserFieldNames { get; set; } = new List<string>();
    }

    public class StorageOptions
    {
        public string DataFile { get; set; } = "data/db.json";
    }
}
