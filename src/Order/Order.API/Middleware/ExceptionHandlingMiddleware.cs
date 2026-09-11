using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Order.Application.Exceptions;
using Order.Domain.Exceptions;

namespace Order.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, title) = ex switch
            {
                NotFoundException => (HttpStatusCode.NotFound, ex.Message),
                InvalidOrderStateException => (HttpStatusCode.Conflict, ex.Message),
                DomainValidationException => (HttpStatusCode.BadRequest, ex.Message),
                ValidationException validationEx => (HttpStatusCode.BadRequest, string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage))),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
            };

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                logger.LogError(ex, "Unhandled exception");
            }

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)statusCode;

            var problemDetails = new ProblemDetails
            {
                Status = (int)statusCode,
                Title = title
            };

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}
