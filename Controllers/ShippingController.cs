using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Vendelo.FakeShippingProvider.Models;
using Vendelo.FakeShippingProvider.Options;
using Vendelo.FakeShippingProvider.Services;

namespace Vendelo.FakeShippingProvider.Controllers
{
    [ApiController]
    public class ShippingController : ControllerBase
    {
        private readonly IDataStore _store;
        private readonly AppOptions _options;

        public ShippingController(IDataStore store, AppOptions options)
        {
            _store = store;
            _options = options;
        }

        [HttpPost("/api/v1/shipment/calculate")]
        public IActionResult Calculate([FromBody] ShippingProviderQuoteRequest request)
        {
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
            var ids = new[] { "sedex", "pac", "jadlog" };
            var names = new[] { "SEDEX", "PAC", "Jadlog Package" };
            var days = new[] { 2, 5, 3 };
            var factors = new[] { 1.25m, 1.0m, 1.13m };

            var rows = new List<ShippingProviderQuoteResponse>();
            for (var i = 0; i < ids.Length; i++)
            {
                rows.Add(new ShippingProviderQuoteResponse
                {
                    id = ids[i],
                    name = names[i],
                    custom_price = Math.Round(basePrice * factors[i], 2),
                    discount = i == 0 ? 0m : Math.Round(basePrice * factors[i] * 0.05m, 2),
                    currency = "BRL",
                    custom_delivery_time = days[i],
                    company = PickCompany(ids[i]),
                    packages = new List<ShippingProviderPackage>
                    {
                        new ShippingProviderPackage
                        {
                            format = "box",
                            weight = totalWeight,
                            price = Math.Round(basePrice * factors[i], 2),
                            insurance_value = Math.Round(totalValue * 0.02m, 2),
                            dimensions = new ShippingProviderDimensions { width = 20, height = 10, length = 30 },
                            products = request.products.Select(x => new ShippingProviderPackageProduct
                            {
                                id = x.id,
                                quantity = x.quantity
                            }).ToList()
                        }
                    },
                    additional_services = new ShippingProviderAdditionalServices
                    {
                        receipt = true,
                        own_hand = false,
                        collect = false
                    },
                    error = null
                });
            }

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
                service_id = null,
                agency_id = null,
                quote = Math.Round(GetTotalValue(request.products) * 0.12m + 9.9m, 2),
                price = Math.Round(GetTotalValue(request.products) * 0.12m + 9.9m, 2),
                delivery_min = 2,
                delivery_max = 6,
                status = "created",
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

            return StatusCode(201, new ShippingProviderCartResponse
            {
                id = id,
                protocol = protocol,
                self_tracking = order.self_tracking,
                error = null,
                errors = new Dictionary<string, string[]>()
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
                        status = "not_found",
                        label_url = null,
                        tracking = null,
                        error = "Order not found."
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(order.tracking))
                    order.tracking = "VX" + Random.Shared.Next(1000000, 9999999) + "BR";

                order.self_tracking = NormalizeTrackingCode(order.self_tracking) ?? order.tracking;
                order.status = "generated";
                order.events.Add(new StoredEvent
                {
                    atUtc = DateTime.UtcNow.ToString("O"),
                    @event = "label_generated",
                    requestId = HttpContext.Items["requestId"]?.ToString()
                });

                output.Add(new ShippingProviderGenerateResponse
                {
                    id = orderId,
                    status = "generated",
                    label_url = "https://labels.fake-shipping.local/" + orderId + ".pdf",
                    tracking = NormalizeTrackingCode(order.tracking) ?? order.tracking,
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
                errors = new Dictionary<string, string[]>()
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
            if (key == "sedex" || key == "pac")
                return new ShippingProviderCompany { id = "1", name = "Correios", picture = "https://fake.local/correios.png" };
            if (key == "jadlog")
                return new ShippingProviderCompany { id = "2", name = "Jadlog", picture = "https://fake.local/jadlog.png" };
            return new ShippingProviderCompany { id = "9", name = "Transportadora Externa", picture = "https://fake.local/external.png" };
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
    }
}
