using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartHomeSystem.Domain.Exceptions;
using SmartHomeSystem.Services.Exceptions;

namespace SmartHomeSystem.API.Middleware;

/// <summary>
/// Єдине місце обробки винятків для всіх контролерів (тому в них немає try-catch).
/// Очікувані помилки (404, 409, 400) повертаються з повідомленням сервісу; усе інше — це 500
/// із загальним текстом: стек викликів і технічні деталі клієнту не передаються, а пишуться в журнал.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Клієнт сам перервав запит — це не помилка сервера.
            _logger.LogInformation("Запит {Method} {Path} скасовано клієнтом", context.Request.Method, context.Request.Path);
        }
        catch (Exception exception)
        {
            // PostgreSQL SQLSTATE класу 23 = порушено обмеження цілісності (23505 UNIQUE, 23514 CHECK, 23503 FOREIGN KEY, 23502 NOT NULL).
            var constraintViolation = exception is DbUpdateException { InnerException: PostgresException pg }
                                      && pg.SqlState.StartsWith("23", StringComparison.Ordinal);

            var (status, title) = exception switch
            {
                DbUpdateException when constraintViolation => (StatusCodes.Status409Conflict, "Порушено обмеження цілісності даних."),
                NotFoundException => (StatusCodes.Status404NotFound, "Ресурс не знайдено."),
                DuplicateRoomNameException => (StatusCodes.Status409Conflict, "Конфлікт даних."),
                RequestValidationException or DomainValidationException
                    => (StatusCodes.Status400BadRequest, "Некоректний запит."),
                _ => (StatusCodes.Status500InternalServerError, "Сталася внутрішня помилка сервера.")
            };

            if (status == StatusCodes.Status500InternalServerError)
            {
                // Повні деталі (з винятком і стеком) — лише в журнал сервера.
                _logger.LogError(
                    exception,
                    "Необроблений виняток під час обробки запиту {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);
            }
            else
            {
                _logger.LogWarning(
                    "Запит {Method} {Path} відхилено зі статусом {StatusCode}: {Reason}",
                    context.Request.Method,
                    context.Request.Path,
                    status,
                    exception.Message);
            }

            if (context.Response.HasStarted)
            {
                throw; // відповідь уже почали надсилати — змінити її неможливо
            }

            context.Response.Clear();
            context.Response.StatusCode = status;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Не вдалося обробити запит. Спробуйте пізніше."
                    : constraintViolation
                        ? "Дані суперечать правилам цілісності бази даних (унікальність або допустимі межі значень)."
                        : exception.Message,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;

            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }
}
