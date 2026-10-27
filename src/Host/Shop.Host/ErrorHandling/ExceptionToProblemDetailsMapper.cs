using FluentValidation;
using JasperFx;
using Marten.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Shop.SharedKernel;
using Wolverine.Persistence;

namespace Shop.Host.ErrorHandling;

/// <summary>
/// Jedno miejsce mapowania wyjątków na ProblemDetails z polem "code" (.claude/rules/api.md, docs/architecture.md §9).
/// </summary>
public sealed class ExceptionToProblemDetailsMapper(IProblemDetailsService problemDetailsService, IHostEnvironment environment)
    : IExceptionHandler
{
    private const string CodeExtension = "code";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = MapToProblem(exception);
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private ProblemDetails MapToProblem(Exception exception) => exception switch
    {
        ValidationException validation => ValidationProblem(validation),
        NotFoundException notFound => Problem(StatusCodes.Status404NotFound, notFound.Code, notFound.Message),
        RequiredDataMissingException missing => Problem(StatusCodes.Status404NotFound, "not-found", missing.Message),
        DuplicateException duplicate => Problem(StatusCodes.Status409Conflict, duplicate.Code, duplicate.Message),
        ConcurrencyException or ExistingStreamIdCollisionException =>
            Problem(StatusCodes.Status409Conflict, "concurrency-conflict", "Zasób został w międzyczasie zmieniony. Odśwież dane i spróbuj ponownie."),
        BusinessRuleViolationException rule => Problem(StatusCodes.Status422UnprocessableEntity, rule.Code, rule.Message),
        _ => UnexpectedProblem(exception)
    };

    private static ProblemDetails ValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        var problem = new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = "Dane wejściowe są niepoprawne."
        };
        problem.Extensions[CodeExtension] = "validation-failed";
        return problem;
    }

    // Szczegóły nieoczekiwanego błędu tylko w Development – na innych środowiskach nie ujawniamy wnętrza aplikacji.
    private ProblemDetails UnexpectedProblem(Exception exception)
    {
        var detail = environment.IsDevelopment()
            ? $"{exception.GetType().Name}: {exception.Message}"
            : "Wystąpił nieoczekiwany błąd.";

        return Problem(StatusCodes.Status500InternalServerError, "unexpected-error", detail);
    }

    private static ProblemDetails Problem(int status, string code, string detail)
    {
        var problem = new ProblemDetails { Status = status, Detail = detail };
        problem.Extensions[CodeExtension] = code;
        return problem;
    }
}
