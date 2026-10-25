using Amlak.Domain.Common.Exceptions;

namespace Amlak.Application.Validators;

/// <summary>
/// Pipeline behavior که قبل از هر command handler اجرا می‌شود.
/// اگر validator ای برای command ثبت شده باشد، اجرا می‌شود.
///
/// خطاها به InputValidationException تبدیل می‌شوند تا ProblemDetails فریم‌ورک
/// پاسخ ۴۲۲ با پیام فارسی بدهد؛ ValidationException خام به ۵۰۰ تبدیل می‌شد.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next)
    {
        if (!validators.Any())
            return await next();

        var context  = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            var errors = failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

            throw new InputValidationException(errors);
        }

        return await next();
    }
}
