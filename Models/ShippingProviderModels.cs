using System.Collections.Generic;

namespace Vendelo.FakeShippingProvider.Models
{
    public class ShippingProviderQuoteRequest
    {
        public ShippingProviderQuoteParty from { get; set; }
        public ShippingProviderQuoteParty to { get; set; }
        public string incoterms { get; set; }
        public List<ShippingProviderQuoteProduct> products { get; set; }
        public List<ShippingProviderUserField> user_fields { get; set; }
    }

    public class ShippingProviderQuoteParty
    {
        public string postal_code { get; set; }
        public string erp_id { get; set; }
        public string document { get; set; }
        public string company_document { get; set; }
        public string state_register { get; set; }
    }

    public class ShippingProviderQuoteProduct
    {
        public string id { get; set; }
        public string erp_id { get; set; }
        public string name { get; set; }
        public decimal width { get; set; }
        public decimal height { get; set; }
        public decimal length { get; set; }
        public decimal weight { get; set; }
        public decimal quantity { get; set; }
        public decimal unit_price { get; set; }
        public decimal discount_total { get; set; }
        public decimal insurance_value { get; set; }
        public List<ShippingProviderUserField> user_fields { get; set; }
    }

    public class ShippingProviderUserField
    {
        public string name { get; set; }
        public string value { get; set; }
    }

    public class ShippingProviderQuoteResponse
    {
        public string id { get; set; }
        public string name { get; set; }
        public decimal custom_price { get; set; }
        public decimal discount { get; set; }
        public string currency { get; set; }
        public int custom_delivery_time { get; set; }
        public ShippingProviderCompany company { get; set; }
        public List<ShippingProviderPackage> packages { get; set; }
        public ShippingProviderAdditionalServices additional_services { get; set; }
        public List<ShippingProviderUserField> user_fields { get; set; }
        public string error { get; set; }
    }

    public class ShippingProviderCompany
    {
        public string id { get; set; }
        public string name { get; set; }
        public string picture { get; set; }
    }

    public class ShippingProviderPackage
    {
        public string format { get; set; }
        public decimal weight { get; set; }
        public decimal price { get; set; }
        public decimal insurance_value { get; set; }
        public ShippingProviderDimensions dimensions { get; set; }
        public List<ShippingProviderPackageProduct> products { get; set; }
    }

    public class ShippingProviderDimensions
    {
        public int width { get; set; }
        public int height { get; set; }
        public int length { get; set; }
    }

    public class ShippingProviderPackageProduct
    {
        public string id { get; set; }
        public decimal quantity { get; set; }
        public List<ShippingProviderUserField> user_fields { get; set; }
    }

    public class ShippingProviderAdditionalServices
    {
        public bool receipt { get; set; }
        public bool own_hand { get; set; }
        public bool collect { get; set; }
    }

    public class ShippingProviderTokenResponse
    {
        public string token_type { get; set; }
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public long expires_in { get; set; }
    }

    public class ShippingProviderCartRequest
    {
        public string service { get; set; }
        public ShippingProviderCartParty from { get; set; }
        public ShippingProviderCartParty to { get; set; }
        public List<ShippingProviderQuoteProduct> products { get; set; }
        public List<ShippingProviderCartVolume> volumes { get; set; }
        public ShippingProviderCartOptions options { get; set; }
    }

    public class ShippingProviderCartParty : ShippingProviderQuoteParty
    {
        public string name { get; set; }
        public string phone { get; set; }
        public string email { get; set; }
        public string address { get; set; }
        public string number { get; set; }
        public string complement { get; set; }
        public string district { get; set; }
        public string city { get; set; }
        public string country_id { get; set; } = "BR";
    }

    public class ShippingProviderCartVolume
    {
        public int height { get; set; }
        public int width { get; set; }
        public int length { get; set; }
        public decimal weight { get; set; }
    }

    public class ShippingProviderCartOptions
    {
        public decimal insurance_value { get; set; }
        public bool receipt { get; set; }
        public bool own_hand { get; set; }
        public bool reverse { get; set; }
        public bool non_commercial { get; set; }
        public ShippingProviderCartInvoice invoice { get; set; }
        public string platform { get; set; } = "Vendelo";
        public List<ShippingProviderCartTag> tags { get; set; }
    }

    public class ShippingProviderCartInvoice
    {
        public string key { get; set; }
    }

    public class ShippingProviderCartTag
    {
        public string tag { get; set; }
        public string url { get; set; }
    }

    public class ShippingProviderCartResponse
    {
        public string id { get; set; }
        public string protocol { get; set; }
        public string self_tracking { get; set; }
        public string error { get; set; }
        public Dictionary<string, string[]> errors { get; set; }
    }

    public class ShippingProviderGenerateRequest
    {
        public List<string> orders { get; set; }
    }

    public class ShippingProviderGenerateResponse
    {
        public string id { get; set; }
        public string status { get; set; }
        public string label_url { get; set; }
        public string tracking { get; set; }
        public string error { get; set; }
    }

    public class ShippingProviderCancelRequest
    {
        public ShippingProviderCancelOrder order { get; set; }
    }

    public class ShippingProviderCancelOrder
    {
        public string id { get; set; }
        public string reason_id { get; set; } = "2";
        public string description { get; set; }
    }

    public class ShippingProviderCancelResponse
    {
        public bool cancelled { get; set; }
        public string error { get; set; }
        public Dictionary<string, string[]> errors { get; set; }
    }

    public class ShippingProviderOrderInfo
    {
        public string id { get; set; }
        public int? service_id { get; set; }
        public int? agency_id { get; set; }
        public decimal? quote { get; set; }
        public decimal? price { get; set; }
        public int? delivery_min { get; set; }
        public int? delivery_max { get; set; }
        public string status { get; set; }
        public string format { get; set; }
        public string self_tracking { get; set; }
        public string tracking { get; set; }
    }

    public class ValidationErrorResponse
    {
        public string error { get; set; } = "validation_error";
        public Dictionary<string, string[]> errors { get; set; } = new Dictionary<string, string[]>();
    }

    public class OAuthTokenRequest
    {
        public string grant_type { get; set; }
        public string refresh_token { get; set; }
        public string code { get; set; }
        public string redirect_uri { get; set; }
        public string client_id { get; set; }
        public string client_secret { get; set; }
    }

    public class OAuthAuthorizeRequest
    {
        public string response_type { get; set; }
        public string client_id { get; set; }
        public string redirect_uri { get; set; }
        public string state { get; set; }
        public string scope { get; set; }
    }

    public class OAuthAuthorizationCodeState
    {
        public string code { get; set; }
        public string clientId { get; set; }
        public string redirectUri { get; set; }
        public string scope { get; set; }
        public string issuedAtUtc { get; set; }
    }

    public class OAuthState
    {
        public string accessToken { get; set; }
        public string refreshToken { get; set; }
        public long expiresIn { get; set; }
        public string tokenType { get; set; }
        public string issuedAtUtc { get; set; }
    }

    public class StoredOrder : ShippingProviderOrderInfo
    {
        public string protocol { get; set; }
        public ShippingProviderCartRequest request { get; set; }
        public string cancel_reason { get; set; }
        public List<StoredEvent> events { get; set; } = new List<StoredEvent>();
    }

    public class StoredEvent
    {
        public string atUtc { get; set; }
        public string @event { get; set; }
        public string requestId { get; set; }
        public string reason { get; set; }
    }

    public class StorageState
    {
        public Dictionary<string, StoredOrder> orders { get; set; } = new Dictionary<string, StoredOrder>();
        public OAuthState oauth { get; set; } = new OAuthState();
        public Dictionary<string, OAuthAuthorizationCodeState> oauthCodes { get; set; } = new Dictionary<string, OAuthAuthorizationCodeState>();
        public Dictionary<string, string> meta { get; set; } = new Dictionary<string, string>();
    }
}
