using System;
using Microsoft.AspNetCore.Mvc;
using Vendelo.FakeShippingProvider.Options;
using Vendelo.FakeShippingProvider.Services;

namespace Vendelo.FakeShippingProvider.Controllers
{
    [ApiController]
    public class SystemController : ControllerBase
    {
        private readonly IDataStore _store;
        private readonly AppOptions _options;

        public SystemController(IDataStore store, AppOptions options)
        {
            _store = store;
            _options = options;
        }

        [HttpGet("/health")]
        public IActionResult Health()
        {
            var db = _store.Read();
            return Ok(new
            {
                status = "ok",
                nowUtc = DateTime.UtcNow.ToString("O"),
                authMode = _options.Auth.Mode,
                ordersInStorage = db.orders.Count
            });
        }
    }
}

