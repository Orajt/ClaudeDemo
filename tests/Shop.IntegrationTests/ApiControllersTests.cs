using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Shop.Api.Contracts.Generated.Controllers;
using Shop.SharedKernel;

namespace Shop.IntegrationTests;

/// <summary>Pokrycie kontraktu OpenAPI: każda operacja ma nadpisaną metodę z polityką zgodną z rolą z kontraktu (zaślepki etapów 3–5).</summary>
public sealed class ApiControllersTests
{
    // Role z shop-api.yaml (docs/specs/001, sekcja 8).
    private static readonly Dictionary<string, string> ExpectedPolicyByOperation = new()
    {
        ["PlaceOrder"] = AuthorizationPolicies.Sales,
        ["CancelOrder"] = AuthorizationPolicies.Sales,
        ["RefundOrder"] = AuthorizationPolicies.Sales,
        ["ConfirmPayment"] = AuthorizationPolicies.Gateway,
        ["ImportSettlementReport"] = AuthorizationPolicies.Accounting,
        ["ListOrderPayments"] = AuthorizationPolicies.SalesOrAccounting,
        ["GetOrderPayment"] = AuthorizationPolicies.SalesOrAccounting,
        ["ListSettlementReports"] = AuthorizationPolicies.Accounting,
        ["ListSettlementDiscrepancies"] = AuthorizationPolicies.Accounting
    };

    private static readonly Dictionary<Type, string> StageByContract = new()
    {
        [typeof(OrdersApiController)] = "Etap 3",
        [typeof(SettlementsApiController)] = "Etap 4",
        [typeof(DataHubApiController)] = "Etap 5"
    };

    public static TheoryData<string> OperationNames() => new(ExpectedPolicyByOperation.Keys);

    [Fact]
    [Trait("AC", "AC-5")]
    public void EveryContractOperation_IsCoveredByExpectedPolicyTable()
    {
        var contractOperations = StageByContract.Keys.SelectMany(AbstractOperations).Select(method => method.Name);

        contractOperations.OrderBy(name => name).ShouldBe(ExpectedPolicyByOperation.Keys.OrderBy(name => name));
    }

    [Theory]
    [MemberData(nameof(OperationNames))]
    [Trait("AC", "AC-5")]
    public void Operation_IsOverriddenWithExpectedPolicy(string operationName)
    {
        var (contract, controller) = FindOverride(operationName);

        var overriddenMethod = controller.GetMethod(operationName)!;
        overriddenMethod.DeclaringType.ShouldBe(controller, $"{operationName} musi być nadpisana w {controller.Name}, a nie dziedziczona z {contract.Name}.");

        // Bazowa metoda z kontraktu ma gołe [Authorize] (wymóg zalogowania) – politykę dodaje dopiero nadpisanie, więc nie dziedziczymy atrybutów.
        var policyAttribute = overriddenMethod.GetCustomAttributes<AuthorizeAttribute>(inherit: false).Single();
        policyAttribute.Policy.ShouldBe(ExpectedPolicyByOperation[operationName]);
    }

    [Theory]
    [MemberData(nameof(OperationNames))]
    [Trait("AC", "AC-5")]
    public void Operation_ThrowsNotImplementedWithStageName(string operationName)
    {
        var (contract, controller) = FindOverride(operationName);
        var instance = Activator.CreateInstance(controller)!;
        var method = controller.GetMethod(operationName)!;
        var arguments = method.GetParameters().Select(parameter => DefaultValueOf(parameter.ParameterType)).ToArray();

        var thrown = Should.Throw<TargetInvocationException>(() => method.Invoke(instance, arguments));

        thrown.InnerException.ShouldBeOfType<NotImplementedException>().Message.ShouldBe(StageByContract[contract]);
    }

    private static IEnumerable<MethodInfo> AbstractOperations(Type contract) =>
        contract.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(method => method.IsAbstract);

    // Kontrolery modułów leżą w assembly *.Api, odnajdujemy je po tym, że dziedziczą po wygenerowanym kontrakcie.
    private static (Type Contract, Type Controller) FindOverride(string operationName)
    {
        var contract = StageByContract.Keys.Single(type => AbstractOperations(type).Any(method => method.Name == operationName));
        var controller = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Single(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(contract));

        return (contract, controller);
    }

    private static object? DefaultValueOf(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
