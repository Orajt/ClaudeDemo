using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using JasperFx;
using Marten.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shop.Host.ErrorHandling;
using Shop.SharedKernel;
using Wolverine.Persistence;

namespace Shop.IntegrationTests;

/// <summary>Globalne mapowanie wyjątków na application/problem+json z polem "code" (tabela reguł w specyfikacji 001, sekcja 6).</summary>
public sealed class ExceptionMappingTests(ShopFixture fixture)
{
    public static TheoryData<string, Exception, int, string> Cases() => new()
    {
        { "validation", new ValidationException([new ValidationFailure("Name", "Wymagane")]), 400, "validation-failed" },
        { "not-found", new NotFoundException("orders.not-found", "Brak zamówienia"), 404, "orders.not-found" },
        { "required-data-missing", new RequiredDataMissingException("Brak danych"), 404, "not-found" },
        { "concurrency", new ConcurrencyException("Konflikt"), 409, "concurrency-conflict" },
        { "stream-id-collision", new ExistingStreamIdCollisionException(Guid.CreateVersion7(), typeof(object)), 409, "concurrency-conflict" },
        { "duplicate", new DuplicateException("settlements.report-already-imported", "Raport już zaimportowany"), 409, "settlements.report-already-imported" },
        { "business-rule", new BusinessRuleViolationException("orders.refund-not-allowed", "Zwrot niedozwolony"), 422, "orders.refund-not-allowed" },
        { "unexpected", new InvalidOperationException("Awaria"), 500, "unexpected-error" }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("AC", "AC-7")]
    public async Task Exception_IsMappedToProblemDetailsWithStatusAndCode(string caseName, Exception exception, int expectedStatus, string expectedCode)
    {
        var (status, problem) = await MapAsync(exception);

        status.ShouldBe(expectedStatus, caseName);
        problem.GetProperty("code").GetString().ShouldBe(expectedCode);
    }

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task ValidationException_ListsErrorsPerProperty()
    {
        var exception = new ValidationException([new ValidationFailure("Name", "Wymagane"), new ValidationFailure("Name", "Za krótkie")]);

        var (_, problem) = await MapAsync(exception);

        problem.GetProperty("errors").GetProperty("Name").EnumerateArray().Select(error => error.GetString())
            .ShouldBe(["Wymagane", "Za krótkie"]);
    }

    private async Task<(int Status, JsonElement Problem)> MapAsync(Exception exception)
    {
        var mapper = ActivatorUtilities.CreateInstance<ExceptionToProblemDetailsMapper>(fixture.Host.Services);
        var httpContext = new DefaultHttpContext { RequestServices = fixture.Host.Services };
        httpContext.Response.Body = new MemoryStream();

        var handled = await mapper.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return (httpContext.Response.StatusCode, document.RootElement.Clone());
    }
}
