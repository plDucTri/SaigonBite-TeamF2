using SaigonBite.Helpers;

namespace SaigonBite.Tests.Services;

public class VerificationServiceTests
{
    private const string Id = "0312345678";

    [Fact]
    public void Code_has_six_digits()
    {
        using var setup = new RegistrationTestSetup();

        string? code = setup.Verification.SendCode(Id, Roles.Local);

        Assert.NotNull(code);
        Assert.Matches(@"^[0-9]{6}\z", code);
    }

    [Fact]
    public void Right_code_is_accepted_once()
    {
        using var setup = new RegistrationTestSetup();
        string code = setup.Verification.SendCode(Id, Roles.Local)!;

        Assert.True(setup.Verification.VerifyCode(Id, code));
        Assert.False(setup.Verification.VerifyCode(Id, code));
    }

    [Fact]
    public void Wrong_code_is_rejected()
    {
        using var setup = new RegistrationTestSetup();
        string code = setup.Verification.SendCode(Id, Roles.Local)!;
        string wrong = code == "000000" ? "000001" : "000000";

        Assert.False(setup.Verification.VerifyCode(Id, wrong));
    }

    [Fact]
    public void Code_for_another_identifier_is_rejected()
    {
        using var setup = new RegistrationTestSetup();
        string code = setup.Verification.SendCode(Id, Roles.Local)!;

        Assert.False(setup.Verification.VerifyCode("0987654321", code));
    }

    [Fact]
    public void Code_works_before_five_minutes_and_not_after()
    {
        using var setup = new RegistrationTestSetup();
        string early = setup.Verification.SendCode(Id, Roles.Local)!;
        setup.Clock.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        Assert.True(setup.Verification.VerifyCode(Id, early));

        string late = setup.Verification.SendCode(Id, Roles.Local)!;
        setup.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.False(setup.Verification.VerifyCode(Id, late));
    }

    [Fact]
    public void Code_is_locked_after_five_wrong_tries()
    {
        using var setup = new RegistrationTestSetup();
        string code = setup.Verification.SendCode(Id, Roles.Local)!;
        string wrong = code == "000000" ? "000001" : "000000";

        for (int i = 0; i < 5; i++)
            Assert.False(setup.Verification.VerifyCode(Id, wrong));

        Assert.False(setup.Verification.VerifyCode(Id, code));
    }
}
