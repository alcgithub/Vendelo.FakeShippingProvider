using Microsoft.AspNetCore.Mvc;
using Vendelo.FakeShippingProvider.Options;
using Vendelo.FakeShippingProvider.Services;

namespace Vendelo.FakeShippingProvider.Controllers
{
    [ApiController]
    public class DebugController : ControllerBase
    {
        private readonly IDataStore _store;
        private readonly AppOptions _options;

        public DebugController(IDataStore store, AppOptions options)
        {
            _store = store;
            _options = options;
        }

        [HttpGet("/debug/orders")]
        public IActionResult Orders()
        {
            if (!_options.Behavior.EnableDebugRoutes)
                return NotFound();

            var db = _store.Read();
            return Ok(new
            {
                count = db.orders.Count,
                orders = db.orders
            });
        }

        [HttpPost("/debug/reset")]
        public IActionResult Reset()
        {
            if (!_options.Behavior.EnableDebugRoutes)
                return NotFound();

            var db = _store.Read();
            db.orders.Clear();
            _store.Write(db);

            return Ok(new
            {
                ok = true,
                message = "Orders reset."
            });
        }
    }
}

