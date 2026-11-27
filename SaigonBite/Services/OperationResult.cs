namespace SaigonBite.Services;

/// <summary>
/// Outcome of an order action that changes data (edit, cancel, change status). Success means Errors is empty.
/// AccountService uses ServiceResult&lt;T&gt; because it returns a value; these actions return nothing but the verdict.
/// </summary>
public class OperationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Success => Errors.Count == 0;

    public static OperationResult Ok() => new OperationResult();

    public static OperationResult Fail(params string[] errors)
    {
        var r = new OperationResult();
        r.Errors.AddRange(errors);
        return r;
    }

    public static OperationResult Fail(IEnumerable<RuleViolation> violations)
    {
        var r = new OperationResult();
        r.Errors.AddRange(violations.Select(v => v.Rule + ": " + v.Message));
        return r;
    }
}
