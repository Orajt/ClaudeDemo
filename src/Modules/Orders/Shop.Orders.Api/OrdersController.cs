using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Contracts.Generated.Controllers;
using Shop.Api.Contracts.Generated.Models;
using Shop.SharedKernel;

namespace Shop.Orders.Api;

// Zaślepki – implementacja w etapie 3. Trasy i modele pochodzą z kontraktu (OrdersApiController).
public sealed class OrdersController : OrdersApiController
{
    private const string NotImplementedYet = "Etap 3";

    [Authorize(Policy = AuthorizationPolicies.Sales)]
    public override Task<IActionResult> PlaceOrder(PlaceOrderRequest placeOrderRequest) =>
        throw new NotImplementedException(NotImplementedYet);

    [Authorize(Policy = AuthorizationPolicies.Gateway)]
    public override Task<IActionResult> ConfirmPayment(Guid orderId, ConfirmPaymentRequest confirmPaymentRequest) =>
        throw new NotImplementedException(NotImplementedYet);

    [Authorize(Policy = AuthorizationPolicies.Sales)]
    public override Task<IActionResult> CancelOrder(Guid orderId, CancelOrderRequest cancelOrderRequest) =>
        throw new NotImplementedException(NotImplementedYet);

    [Authorize(Policy = AuthorizationPolicies.Sales)]
    public override Task<IActionResult> RefundOrder(Guid orderId, RefundOrderRequest refundOrderRequest) =>
        throw new NotImplementedException(NotImplementedYet);
}
