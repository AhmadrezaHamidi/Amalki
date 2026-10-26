namespace Amlak.Domain.Common.Exceptions;

/// <summary>
/// خطای اعتبارسنجی ورودی.
/// از BusinessException ارث می‌برد تا ProblemDetails فریم‌ورک آن را به ۴۲۲
/// با پیام فارسی خوانا تبدیل کند — نه ۵۰۰ ناشناخته.
/// </summary>
public sealed class InputValidationException(IReadOnlyDictionary<string, string[]> errors)
    : BusinessException(BuildMessage(errors))
{
    /// <summary>خطاها به تفکیک فیلد</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    private static string BuildMessage(IReadOnlyDictionary<string, string[]> errors)
        => string.Join(" ", errors.SelectMany(e => e.Value));
}
