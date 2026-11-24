using SaigonBite.Helpers;
using SaigonBite.Validators;

namespace SaigonBite.Tests.Services;

public class AccountLoginTests
{
    // Makes a real Local account (register, then confirm with the code).
    private static void CreateLocal(RegistrationTestSetup setup)
    {
        var started = setup.Service.RegisterAccount(new AccountRequest
        {
            Type = Roles.Local,
            Identifier = "0312345678",
            FullName = "Nguyen Van A",
            Password = "password1"
        });
        setup.Service.ConfirmRegistration("0312345678", started.Value!.DemoCode!);
    }

    [Fact]
    public void Right_phone_and_password_sign_in()
    {
        using var setup = new RegistrationTestSetup();
        CreateLocal(setup);

        var result = setup.Service.Login("0312345678", "password1");

        Assert.True(result.Ok);
        Assert.Equal(Roles.Local, result.Value!.Role);
        Assert.Equal("Nguyen Van A", result.Value.FullName);
    }

    [Fact]
    public void Passport_login_ignores_letter_case()
    {
        using var setup = new RegistrationTestSetup();
        var started = setup.Service.RegisterAccount(new AccountRequest
        {
            Type = Roles.Tourist,
            Identifier = "ab1234567",
            FullName = "John Smith",
            Password = "password1",
            Nationality = "British",
            Email = "john@example.com"
        });
        setup.Service.ConfirmRegistration("ab1234567", started.Value!.DemoCode!);

        var result = setup.Service.Login("AB1234567", "password1");

        Assert.True(result.Ok);
        Assert.Equal(Roles.Tourist, result.Value!.Role);
    }

    [Fact]
    public void Wrong_password_is_refused()
    {
        using var setup = new RegistrationTestSetup();
        CreateLocal(setup);

        var result = setup.Service.Login("0312345678", "wrongpass1");

        Assert.False(result.Ok);
    }

    [Fact]
    public void Unknown_identifier_gets_the_same_message_as_a_wrong_password()
    {
        using var setup = new RegistrationTestSetup();
        CreateLocal(setup);

        var wrongPassword = setup.Service.Login("0312345678", "wrongpass1");
        var unknown = setup.Service.Login("0399999999", "password1");

        Assert.False(unknown.Ok);
        Assert.Equal(wrongPassword.Errors[0], unknown.Errors[0]);
    }

    [Fact]
    public void Deactivated_account_is_refused()
    {
        using var setup = new RegistrationTestSetup();
        CreateLocal(setup);
        var user = setup.Db.Users.Single();
        user.IsActive = false;
        setup.Db.SaveChanges();

        var result = setup.Service.Login("0312345678", "password1");

        Assert.False(result.Ok);
    }

    [Fact]
    public void Empty_values_are_refused()
    {
        using var setup = new RegistrationTestSetup();

        Assert.False(setup.Service.Login("", "").Ok);
        Assert.False(setup.Service.Login(null!, null!).Ok);
    }
}
