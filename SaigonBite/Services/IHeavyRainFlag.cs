namespace SaigonBite.Services;

/// <summary>
/// The Admin's heavy-rain flag (BR5). The schema has no settings table (Ambiguity Log A-06), so the flag lives in
/// memory while the application runs. The value in force is copied into the order's price snapshot (BR17).
/// </summary>
public interface IHeavyRainFlag
{
    bool IsOn { get; }
    void Set(bool on);
}

public class HeavyRainFlag : IHeavyRainFlag
{
    private volatile bool _isOn;

    public bool IsOn => _isOn;

    public void Set(bool on) => _isOn = on;
}
