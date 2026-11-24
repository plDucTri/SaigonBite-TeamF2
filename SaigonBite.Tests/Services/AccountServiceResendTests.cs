using SaigonBite.Helpers;
using SaigonBite.Validators;

namespace SaigonBite.Tests.Services;

public class AccountServiceResendTests
{
    private static AccountRequest LocalRequest() => new()
    {
        Type = Roles.Local,
        Identifier = "0312345678",
        FullName = "Nguyen Van A",
        Password = "password1"
    };

    [Fact]
    public void Resend_without_a_registration_is_rejected()
    {
        using var setup = new RegistrationTestSetup();

        var result = setup.Service.ResendCode("0312345678");

        Assert.False(result.Ok);
        Assert.Empty(setup.Sender.Sent);
    }

    [Fact]
    public void Resend_within_30_seconds_is_rejected_and_sends_nothing()
    {
        using var setup = new RegistrationTestSetup();
        setup.Service.RegisterAccount(LocalRequest());
        setup.Clock.Advance(TimeSpan.FromSeconds(29));

        var result = setup.Service.ResendCode("0312345678");

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("30 seconds"));
        Assert.Single(setup.Sender.Sent);
    }

    [Fact]
    public void Resend_after_30_seconds_sends_a_new_code_that_creates_the_account()
    {
        using var setup = new RegistrationTestSetup();
        setup.Service.RegisterAccount(LocalRequest());
        setup.Clock.Advance(TimeSpan.FromSeconds(31));

        var resent = setup.Service.ResendCode("0312345678");

        Assert.True(resent.Ok);
        Assert.Equal(2, setup.Sender.Sent.Count);
        var created = setup.Service.ConfirmRegistration("0312345678", resent.Value!.DemoCode!);
        Assert.True(created.Ok);
        Assert.Equal(1, setup.Db.Users.Count());
    }

    [Fact]
    public void Resend_is_limited_again_right_after_a_resend()
    {
        using var setup = new RegistrationTestSetup();
        setup.Service.RegisterAccount(LocalRequest());
        setup.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.True(setup.Service.ResendCode("0312345678").Ok);

        var second = setup.Service.ResendCode("0312345678");

        Assert.False(second.Ok);
    }

    [Fact]
    public void Resend_after_the_account_is_created_is_rejected()
    {
        using var setup = new RegistrationTestSetup();
        var started = setup.Service.RegisterAccount(LocalRequest());
        setup.Service.ConfirmRegistration("0312345678", started.Value!.DemoCode!);
        setup.Clock.Advance(TimeSpan.FromSeconds(31));

        var result = setup.Service.ResendCode("0312345678");

        Assert.False(result.Ok);
    }
}
