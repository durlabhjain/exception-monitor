using FluentValidation.Results;

namespace ExceptionMonitor.Api.Common;

public static class EndpointResults
{
    public static IResult ValidationProblem(ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
    }
}
