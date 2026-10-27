using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Contracts.Generated.Controllers;
using Shop.Api.Contracts.Generated.Models;
using Shop.SharedKernel;

namespace Shop.DataHub.Api;

// Zaślepki – implementacja w etapie 5. Trasy i modele pochodzą z kontraktu (DataHubApiController).
public sealed class DataHubController : DataHubApiController
{
    private const string NotImplementedYet = "Etap 5";

    [Authorize(Policy = AuthorizationPolicies.SalesOrAccounting)]
    public override Task<IActionResult> ListOrderPayments(OrderPaymentStatus? status, string? search, int? page, int? pageSize) =>
        throw new NotImplementedException(NotImplementedYet);

    [Authorize(Policy = AuthorizationPolicies.SalesOrAccounting)]
    public override Task<IActionResult> GetOrderPayment(Guid orderId) =>
        throw new NotImplementedException(NotImplementedYet);

    [Authorize(Policy = AuthorizationPolicies.Accounting)]
    public override Task<IActionResult> ListSettlementReports(int? page, int? pageSize) =>
        throw new NotImplementedException(NotImplementedYet);

    [Authorize(Policy = AuthorizationPolicies.Accounting)]
    public override Task<IActionResult> ListSettlementDiscrepancies(Guid? reportId, DiscrepancyKind? kind, int? page, int? pageSize) =>
        throw new NotImplementedException(NotImplementedYet);
}
