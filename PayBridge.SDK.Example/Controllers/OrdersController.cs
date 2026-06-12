using Microsoft.AspNetCore.Mvc;
using PayBridge.SDK.Example.Models;
using PayBridge.SDK.Example.Services;

namespace PayBridge.SDK.Example.Controllers;

/// <summary>
/// Development-only helper — inspect the in-memory order store.
/// Remove or guard with an auth policy before deploying to production.
/// </summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orders;

    public OrdersController(OrderService orders) => _orders = orders;

    /// <summary>
    /// Returns all orders currently held in the in-memory store.
    /// Useful for confirming that webhook / redirect verification updated
    /// the order status correctly.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        var list = _orders.All().Select(o => new
        {
            o.OrderId,
            o.CustomerEmail,
            o.Amount,
            o.Currency,
            o.Description,
            status         = o.Status.ToString(),
            o.TransactionRef,
            paidAt         = o.PaidAt == default ? (DateTime?)null : o.PaidAt,
        });

        return Ok(ApiResponse<object>.Ok(list));
    }

    /// <summary>Gets a single order by its ID.</summary>
    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public IActionResult GetById(string orderId)
    {
        var order = _orders.GetById(orderId);
        if (order is null)
            return NotFound(ApiResponse<object>.Fail($"Order '{orderId}' not found.", "NOT_FOUND"));

        return Ok(ApiResponse<object>.Ok(new
        {
            order.OrderId,
            order.CustomerEmail,
            order.Amount,
            order.Currency,
            order.Description,
            status         = order.Status.ToString(),
            order.TransactionRef,
            paidAt         = order.PaidAt == default ? (DateTime?)null : order.PaidAt,
        }));
    }
}
