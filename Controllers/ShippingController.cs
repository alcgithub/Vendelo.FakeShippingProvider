using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Vendelo.FakeShippingProvider.Models;
using Vendelo.FakeShippingProvider.Options;
using Vendelo.FakeShippingProvider.Services;

namespace Vendelo.FakeShippingProvider.Controllers
{
    [ApiController]
    public class ShippingController : ControllerBase
    {
        private static readonly string[] CarrierErpIds =
        {
            "V10000",
            "V30000",
            "V60000",
            "V10010",
            "V20000",
            "V50000",
            "V22000",
            "V21000",
            "V70000"
        };

        private readonly IDataStore _store;
        private readonly AppOptions _options;
        private readonly ILogger<ShippingController> _logger;

        public ShippingController(IDataStore store, AppOptions options, ILogger<ShippingController> logger)
        {
            _store = store;
            _options = options;
            _logger = logger;
        }

        [HttpPost("/api/v1/shipment/calculate")]
        public IActionResult Calculate([FromBody] ShippingProviderQuoteRequest request)
        {
            var payloadJson = JsonSerializer.Serialize(request);
            Console.WriteLine($"[calculate] payload: {payloadJson}");
            _logger.LogWarning("CALCULATE_IN requestId={RequestId} payload={Payload}", HttpContext.Items["requestId"]?.ToString(), payloadJson);

            var errors = RequestValidator.ValidateQuote(request);
            if (errors.Count > 0)
                return UnprocessableEntity(new ValidationErrorResponse { errors = errors });

            if (!string.IsNullOrWhiteSpace(_options.Behavior.ForceErrorForService) &&
                string.Equals(request.incoterms, _options.Behavior.ForceErrorForService, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new[] { new ShippingProviderQuoteResponse { error = "Forced error for test." } });
            }

            var totalWeight = request.products.Sum(x => x.weight * x.quantity);
            var totalValue = GetTotalValue(request.products);
            var basePrice = Math.Max(15m, 9.9m + totalWeight * 4.2m + totalValue * 0.015m);
            var ids = new[] { "1", "2", "3", "4" };
            var names = new[] { "SEDEX", "PAC", "Jadlog Package", "ChinaLog" };
            var days = new[] { 2, 5, 3, 12 };
            var factors = new[] { 1.25m, 1.0m, 1.13m, 1.0m };

            var rows = new List<ShippingProviderQuoteResponse>();
            for (var i = 0; i < ids.Length; i++)
            {
                var isChinaLog = ids[i] == "4";
                var quotePrice = isChinaLog
                    ? GetUnitPriceRef(request.products, basePrice)
                    : Math.Round(basePrice * factors[i], 2);
                // Na cotação ChinaLog a transportadora sempre vem preenchida; nas
                // demais mantém o sorteio (podendo vir nula).
                var carrierErpId = PickCarrierErpId(forceCarrier: isChinaLog);
                // O mesmo conjunto de user_fields (com o eventual fake já aplicado) é
                // reutilizado no package e no root da cotação, para não devolver um
                // valor de um lado e outro diferente do outro.
                var quoteUserFields = MaybeReturnUserFields(request.user_fields, $"quote:{ids[i]}", isChinaLog);
                rows.Add(new ShippingProviderQuoteResponse
                {
                    id = ids[i],
                    name = names[i],
                    custom_price = quotePrice,
                    discount = ids[i] == "4" ? 0m : (i == 0 ? 0m : Math.Round(basePrice * factors[i] * 0.05m, 2)),
                    currency = "BRL",
                    custom_delivery_time = days[i],
                    carrier = string.IsNullOrWhiteSpace(carrierErpId) ? null : new ShippingProviderQuoteResponseCarrier
                    {
                        erp_id = carrierErpId
                    },
                    company = PickCompany(ids[i]),
                    packages = new List<ShippingProviderPackage>
                    {
                        new ShippingProviderPackage
                        {
                            format = "box",
                            weight = totalWeight,
                            price = quotePrice,
                            insurance_value = Math.Round(totalValue * 0.02m, 2),
                            dimensions = new ShippingProviderDimensions { width = 20, height = 10, length = 30 },
                            products = request.products.Select(x => new ShippingProviderPackageProduct
                            {
                                id = x.id,
                                quantity = x.quantity,
                                user_fields = MaybeReturnUserFields(x.user_fields, $"item:{x.id}", isChinaLog)
                            }).ToList(),
                            user_fields = quoteUserFields
                        }
                    },
                    additional_services = new ShippingProviderAdditionalServices
                    {
                        receipt = true,
                        own_hand = false,
                        collect = false
                    },
                    user_fields = quoteUserFields,
                    error = null
                });
            }

            var responseJson = JsonSerializer.Serialize(rows);
            Console.WriteLine($"[calculate] response: {responseJson}");
            _logger.LogWarning("CALCULATE_OUT requestId={RequestId} response={Response}", HttpContext.Items["requestId"]?.ToString(), responseJson);

            return Ok(rows);
        }

        [HttpPost("/api/v1/cart")]
        public IActionResult AddCart([FromBody] ShippingProviderCartRequest request)
        {
            var errors = RequestValidator.ValidateCart(request);
            if (errors.Count > 0)
                return UnprocessableEntity(new ValidationErrorResponse { errors = errors });

            var id = "ord_" + Guid.NewGuid().ToString("N").Substring(0, 16);
            var protocol = "PR-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var tracking = "VX" + Random.Shared.Next(1000000, 9999999) + "BR";

            var db = _store.Read();
            var order = new StoredOrder
            {
                id = id,
                service_id = ParseServiceId(request.service),
                agency_id = 42,
                quote = Math.Round(GetTotalValue(request.products) * 0.12m + 9.9m, 2),
                price = Math.Round(GetTotalValue(request.products) * 0.12m + 9.9m, 2),
                delivery_min = 2,
                delivery_max = 3,
                status = "pending",
                format = "box",
                self_tracking = tracking,
                tracking = null,
                protocol = protocol,
                request = request,
                events = new List<StoredEvent>
                {
                    new StoredEvent
                    {
                        atUtc = DateTime.UtcNow.ToString("O"),
                        @event = "cart_created",
                        requestId = HttpContext.Items["requestId"]?.ToString()
                    }
                }
            };

            db.orders[id] = order;
            _store.Write(db);

            return Ok(new ShippingProviderCartResponse
            {
                id = id,
                protocol = protocol,
                self_tracking = order.self_tracking,
                error = null,
                errors = null
            });
        }

        [HttpPost("/api/v1/shipment/generate")]
        public IActionResult Generate([FromBody] ShippingProviderGenerateRequest request)
        {
            var errors = RequestValidator.ValidateGenerate(request);
            if (errors.Count > 0)
                return UnprocessableEntity(new ValidationErrorResponse { errors = errors });

            var db = _store.Read();
            var output = new List<ShippingProviderGenerateResponse>();

            foreach (var orderId in request.orders)
            {
                if (!db.orders.TryGetValue(orderId, out var order))
                {
                    output.Add(new ShippingProviderGenerateResponse
                    {
                        id = orderId,
                        status = "error",
                        label_url = null,
                        tracking = null,
                        error = "Order not found."
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(order.tracking))
                    order.tracking = "VX" + Random.Shared.Next(1000000, 9999999) + "BR";

                var trackingCode = NormalizeTrackingCode(order.tracking) ?? order.tracking;
                var trackingUrl = BuildTrackingUrl(trackingCode);
                order.self_tracking = NormalizeTrackingCode(order.self_tracking) ?? order.tracking;
                order.status = "released";
                order.events.Add(new StoredEvent
                {
                    atUtc = DateTime.UtcNow.ToString("O"),
                    @event = "label_generated",
                    requestId = HttpContext.Items["requestId"]?.ToString()
                });

                output.Add(new ShippingProviderGenerateResponse
                {
                    id = orderId,
                    status = "released",
                    label_url = trackingUrl,
                    tracking = trackingUrl ?? trackingCode,
                    error = null
                });
            }

            _store.Write(db);
            return Ok(output);
        }

        [HttpPost("/api/v1/cart/cancel")]
        public IActionResult Cancel([FromBody] ShippingProviderCancelRequest request)
        {
            var errors = RequestValidator.ValidateCancel(request);
            if (errors.Count > 0)
                return UnprocessableEntity(new ValidationErrorResponse { errors = errors });

            var db = _store.Read();
            if (!db.orders.TryGetValue(request.order.id, out var order))
            {
                return Ok(new ShippingProviderCancelResponse
                {
                    cancelled = false,
                    error = "Order not found.",
                    errors = new Dictionary<string, string[]> { { "order", new[] { "Unknown order id." } } }
                });
            }

            order.status = "cancelled";
            order.cancel_reason = request.order.description;
            order.events.Add(new StoredEvent
            {
                atUtc = DateTime.UtcNow.ToString("O"),
                @event = "order_cancelled",
                reason = request.order.description,
                requestId = HttpContext.Items["requestId"]?.ToString()
            });

            _store.Write(db);
            return Ok(new ShippingProviderCancelResponse
            {
                cancelled = true,
                error = null,
                errors = null
            });
        }

        [HttpGet("/api/v1/orders/{orderId}")]
        public IActionResult GetOrderInfo([FromRoute] string orderId)
        {
            var db = _store.Read();
            if (!db.orders.TryGetValue(orderId, out var order))
            {
                return NotFound(new
                {
                    error = "Order not found.",
                    id = orderId
                });
            }

            return Ok(new ShippingProviderOrderInfo
            {
                id = order.id,
                service_id = order.service_id,
                agency_id = order.agency_id,
                quote = order.quote,
                price = order.price,
                delivery_min = order.delivery_min,
                delivery_max = order.delivery_max,
                status = order.status,
                format = order.format,
                self_tracking = NormalizeTrackingCode(order.self_tracking) ?? order.self_tracking,
                tracking = BuildTrackingUrl(NormalizeTrackingCode(order.tracking) ?? NormalizeTrackingCode(order.self_tracking))
            });
        }

        [HttpGet("/tracking/{tracking}")]
        public IActionResult Track([FromRoute] string tracking)
        {
            var trackingCode = NormalizeTrackingCode(tracking);
            if (string.IsNullOrWhiteSpace(trackingCode))
                return NotFound(new { error = "Tracking not found.", tracking = tracking });

            var db = _store.Read();
            var order = db.orders.Values.FirstOrDefault(o =>
            {
                var selfCode = NormalizeTrackingCode(o.self_tracking);
                var orderCode = NormalizeTrackingCode(o.tracking);
                return string.Equals(selfCode, trackingCode, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(orderCode, trackingCode, StringComparison.OrdinalIgnoreCase);
            });

            if (order == null)
            {
                return NotFound(new
                {
                    error = "Tracking not found.",
                    tracking = trackingCode
                });
            }

            return Ok(new
            {
                tracking = trackingCode,
                order_id = order.id,
                status = order.status,
                self_tracking = NormalizeTrackingCode(order.self_tracking),
                tracking_url = BuildTrackingUrl(trackingCode)
            });
        }

        private string BuildTrackingUrl(string trackingCode)
        {
            if (string.IsNullOrWhiteSpace(trackingCode))
                return null;

            var code = NormalizeTrackingCode(trackingCode);
            if (string.IsNullOrWhiteSpace(code))
                return null;

            return $"{Request.Scheme}://{Request.Host}/tracking/{code}";
        }

        private static string NormalizeTrackingCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            const string prefix = "VendeloFake::";
            var normalized = value.Trim();
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(prefix.Length);

            var marker = "/tracking/";
            var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx + marker.Length < normalized.Length)
                normalized = normalized.Substring(idx + marker.Length);
            else
            {
                var legacyMarker = "/rastreio/";
                var legacyIdx = normalized.IndexOf(legacyMarker, StringComparison.OrdinalIgnoreCase);
                if (legacyIdx >= 0 && legacyIdx + legacyMarker.Length < normalized.Length)
                    normalized = normalized.Substring(legacyIdx + legacyMarker.Length);
            }

            return string.IsNullOrWhiteSpace(normalized) ? null : normalized.Trim('/');
        }

        private static ShippingProviderCompany PickCompany(string service)
        {
            var key = (service ?? "").ToLowerInvariant();
            if (key == "1" || key == "2")
                return new ShippingProviderCompany { id = "1", name = "Correios", picture = "https://fake.local/correios.png" };
            if (key == "3")
                return new ShippingProviderCompany { id = "2", name = "Jadlog", picture = "https://fake.local/jadlog.png" };
            if (key == "4")
                return new ShippingProviderCompany { id = "4", name = "ChinaLog", picture = "https://fake.local/chinalog.png" };
            return new ShippingProviderCompany { id = "9", name = "Transportadora Externa", picture = "https://fake.local/external.png" };
        }

        private static int? ParseServiceId(string service)
        {
            if (int.TryParse(service, out var parsed))
                return parsed;

            var normalized = (service ?? "").Trim().ToLowerInvariant();
            if (normalized == "sedex")
                return 1;
            if (normalized == "pac")
                return 2;
            if (normalized == "jadlog")
                return 3;
            if (normalized == "chinalog")
                return 4;

            return null;
        }

        private static decimal GetUnitPriceRef(List<ShippingProviderQuoteProduct> products, decimal fallbackPrice)
        {
            var refItem = products?.FirstOrDefault();
            if (refItem == null)
                return Math.Round(fallbackPrice, 2);

            return Math.Round(refItem.unit_price, 2);
        }

        private static decimal GetTotalValue(List<ShippingProviderQuoteProduct> products)
        {
            if (products == null || products.Count == 0)
                return 0m;

            decimal sum = 0m;
            foreach (var p in products)
            {
                sum += p.unit_price * p.quantity - p.discount_total;
            }

            return sum;
        }

        private List<ShippingProviderUserField> MaybeReturnUserFields(List<ShippingProviderUserField> fields, string scope, bool allowFake)
        {
            if (fields == null || fields.Count == 0)
                return null;

            var copy = fields
                .Where(f => f != null && !string.IsNullOrWhiteSpace(f.name))
                .Select(f => new ShippingProviderUserField
                {
                    name = f.name,
                    value = f.value
                })
                .ToList();

            if (copy.Count == 0)
                return null;

            // O fake só se aplica à cotação ChinaLog; nas demais os valores voltam
            // exatamente como chegaram.
            if (!allowFake)
                return copy;

            // Só alteramos os campos cujo nome está configurado em Behavior.FakeUserFieldNames.
            var allowed = _options.Behavior.FakeUserFieldNames;
            if (allowed == null || allowed.Count == 0)
                return copy;

            var fakeable = copy
                .Where(f => allowed.Contains(f.name, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (fakeable.Count == 0)
                return copy;

            // Faz a simulação "às vezes": em parte das respostas os valores vêm iguais,
            // em parte eles vêm alterados para testar sobrescrita no consumidor.
            if (Random.Shared.Next(2) == 0)
                return copy;

            var target = fakeable[Random.Shared.Next(fakeable.Count)];
            target.value = BuildDifferentValue(target.value, scope, target.name);

            return copy;
        }

        // Gera um valor fake diferente do original, mas preservando o "tipo" que o
        // valor aparenta ter. user_fields sempre trafegam como string, porém podem
        // carregar número/preço, data ou booleano serializados como texto — e o
        // consumidor pode fazer parse disso. Só concatenamos texto livre quando o
        // valor realmente é texto; caso contrário devolvemos um valor válido do
        // mesmo tipo, evitando quebrar o parse do lado do consumidor.
        private static string BuildDifferentValue(string currentValue, string scope, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(currentValue))
                return $"{scope}:{fieldName} (fake)";

            var trimmed = currentValue.Trim();

            // Numérico (inteiro ou decimal) -> outro número válido.
            if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                return (dec + 1).ToString(CultureInfo.InvariantCulture);

            // Hora pura (sem componente de data) -> outra hora válida em ISO (HH:mm:ss).
            if (trimmed.IndexOf('-') < 0 && trimmed.IndexOf('/') < 0 &&
                TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out var ts))
                return ts.Add(TimeSpan.FromMinutes(1)).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

            // Data/hora -> outra data válida no mesmo formato ISO. Preservamos o
            // "tipo": data pura volta como yyyy-MM-dd; data com hora volta em ISO
            // 8601 completo (com hora e, se houver, o offset/Z de origem).
            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                var next = dt.AddDays(1);
                var hasTimeComponent = trimmed.IndexOf('T') >= 0 || trimmed.IndexOf(':') >= 0;
                return hasTimeComponent
                    ? next.ToString("o", CultureInfo.InvariantCulture)
                    : next.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            // Booleano -> invertido.
            if (bool.TryParse(trimmed, out var b))
                return (!b).ToString().ToLowerInvariant();

            // Texto livre -> aí sim podemos concatenar.
            return $"{trimmed} (fake)";
        }

        private static string PickCarrierErpId(bool forceCarrier)
        {
            if (CarrierErpIds.Length == 0)
                return null;

            if (!forceCarrier && Random.Shared.Next(2) == 0)
                return null;

            return CarrierErpIds[Random.Shared.Next(CarrierErpIds.Length)];
        }
    }
}
