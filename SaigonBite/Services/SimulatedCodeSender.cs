using SaigonBite.Helpers;

namespace SaigonBite.Services;

public class SimulatedCodeSender : ICodeSender
{
    private readonly ILogger<SimulatedCodeSender> _logger;

    public SimulatedCodeSender(ILogger<SimulatedCodeSender> logger)
    {
        _logger = logger;
    }

    public string? Send(string identifier, string type, string code)
    {
        string channel = type == Roles.Local ? "OTP (SMS)" : "email code";
        _logger.LogInformation("Simulated {Channel} for {Identifier}: {Code}", channel, identifier, code);
        return code;
    }
}
