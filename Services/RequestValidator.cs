using System.Collections.Generic;
using Vendelo.FakeShippingProvider.Models;

namespace Vendelo.FakeShippingProvider.Services
{
    public static class RequestValidator
    {
        public static Dictionary<string, string[]> ValidateQuote(ShippingProviderQuoteRequest req)
        {
            var errors = new Dictionary<string, string[]>();
            if (req == null)
            {
                errors["body"] = new[] { "Request body is required." };
                return errors;
            }

            if (req.from == null || string.IsNullOrWhiteSpace(req.from.postal_code))
                errors["from.postal_code"] = new[] { "from.postal_code is required." };
            if (req.to == null || string.IsNullOrWhiteSpace(req.to.postal_code))
                errors["to.postal_code"] = new[] { "to.postal_code is required." };

            if (req.products == null || req.products.Count == 0)
            {
                errors["products"] = new[] { "At least one product is required." };
            }
            else
            {
                for (var i = 0; i < req.products.Count; i++)
                {
                    var p = req.products[i];
                    if (string.IsNullOrWhiteSpace(p.id))
                        errors[$"products[{i}].id"] = new[] { "id is required." };
                    if (p.width <= 0 || p.height <= 0 || p.length <= 0 || p.weight <= 0 || p.quantity <= 0)
                        errors[$"products[{i}]"] = new[] { "width, height, length, weight and quantity must be > 0." };
                    if (p.unit_price < 0)
                        errors[$"products[{i}].unit_price"] = new[] { "unit_price must be >= 0." };
                }
            }

            return errors;
        }

        public static Dictionary<string, string[]> ValidateCart(ShippingProviderCartRequest req)
        {
            var errors = new Dictionary<string, string[]>();
            if (req == null)
            {
                errors["body"] = new[] { "Request body is required." };
                return errors;
            }

            if (string.IsNullOrWhiteSpace(req.service))
                errors["service"] = new[] { "service is required." };
            if (req.from == null)
                errors["from"] = new[] { "from is required." };
            if (req.to == null)
                errors["to"] = new[] { "to is required." };
            if (req.products == null || req.products.Count == 0)
                errors["products"] = new[] { "At least one product is required." };

            return errors;
        }

        public static Dictionary<string, string[]> ValidateGenerate(ShippingProviderGenerateRequest req)
        {
            var errors = new Dictionary<string, string[]>();
            if (req == null || req.orders == null || req.orders.Count == 0)
                errors["orders"] = new[] { "orders must have at least one id." };
            return errors;
        }

        public static Dictionary<string, string[]> ValidateCancel(ShippingProviderCancelRequest req)
        {
            var errors = new Dictionary<string, string[]>();
            if (req?.order == null)
            {
                errors["order"] = new[] { "order is required." };
                return errors;
            }

            if (string.IsNullOrWhiteSpace(req.order.id))
                errors["order.id"] = new[] { "order.id is required." };
            if (string.IsNullOrWhiteSpace(req.order.description))
                errors["order.description"] = new[] { "order.description is required." };
            return errors;
        }
    }
}

