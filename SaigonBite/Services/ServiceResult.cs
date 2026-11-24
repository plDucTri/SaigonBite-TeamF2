namespace SaigonBite.Services;

/// <summary>Outcome of a service call: Ok with a Value, or not Ok with a list of Errors.</summary>
public class ServiceResult<T>
{
    private ServiceResult(bool ok, T? value, IReadOnlyList<string> errors)
    {
        Ok = ok;
        Value = value;
        Errors = errors;
    }

    public bool Ok { get; }
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    public static ServiceResult<T> Success(T value) => new(true, value, Array.Empty<string>());

    public static ServiceResult<T> Fail(string error) => new(false, default, new[] { error });

    public static ServiceResult<T> Fail(IEnumerable<string> errors) => new(false, default, errors.ToList());
}
