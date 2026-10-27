using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Contracts.Generated.Controllers;
using Shop.SharedKernel;

namespace Shop.Settlements.Api;

// Zaślepka – implementacja w etapie 4. Trasy i modele pochodzą z kontraktu (SettlementsApiController).
public sealed class SettlementsController : SettlementsApiController
{
    private const string NotImplementedYet = "Etap 4";

    [Authorize(Policy = AuthorizationPolicies.Accounting)]
    public override Task<IActionResult> ImportSettlementReport(IFormFile file) =>
        throw new NotImplementedException(NotImplementedYet);
}
