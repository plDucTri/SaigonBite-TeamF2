using SaigonBite.Helpers;
using SaigonBite.Validators;

namespace SaigonBite.Tests.Services;

public class AccountServiceRegistrationTests
{
    private static AccountRequest LocalRequest(string phone = "0312345678") => new()
    {
        Type = Roles.Local,
        Identifier = phone,
        FullName = "Nguyen Van A",
        Password = "password1"
    };

    private static AccountRequest TouristRequest(string passport = "AB1234", string email = "john@example.com") => new()
    {
        Type = Roles.Tourist,
        Identifier = passport,
        FullName = "John Smith",
        Password = "password1",
        Nationality = "British",
        Email = email,
        HotelName = "Sunrise Hotel",
        RoomNo = "305"
    };

    private static void Register(RegistrationTestSetup setup, AccountRequest request)
    {
        var started = setup.Service.RegisterAccount(request);
        Assert.True(started.Ok);
        var created = setup.Service.ConfirmRegistration(started.Value!.Identifier, started.Value.DemoCode!);
        Assert.True(created.Ok);
    }

    // ---- Good registrations ----

    [Fact]
    public void Local_registration_creates_one_user_and_one_local_profile()
    {
        using var setup = new RegistrationTestSetup();

        var started = setup.Service.RegisterAccount(LocalRequest());
        Assert.True(started.Ok);
        Assert.Equal(0, setup.Db.Users.Count()); // nothing is saved before the code is confirmed

        var created = setup.Service.ConfirmRegistration("0312345678", started.Value!.DemoCode!);

        Assert.True(created.Ok);
        var user = Assert.Single(setup.Db.Users);
        Assert.Equal(Roles.Local, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal("0312345678", user.Identifier);
        Assert.NotEqual("password1", user.PasswordHash);
        Assert.DoesNotContain("password1", user.PasswordHash);
        Assert.True(setup.Hasher.Verify("password1", user.PasswordHash));
        Assert.Equal(1, setup.Db.LocalProfiles.Count());
        Assert.Equal(0, setup.Db.TouristProfiles.Count());
    }

    [Fact]
    public void Tourist_registration_creates_one_user_and_one_tourist_profile_with_upper_case_passport()
    {
        using var setup = new RegistrationTestSetup();

        Register(setup, TouristRequest(passport: "ab1234"));

        var user = Assert.Single(setup.Db.Users);
        Assert.Equal(Roles.Tourist, user.Role);
        Assert.Equal("AB1234", user.Identifier);
        var profile = Assert.Single(setup.Db.TouristProfiles);
        Assert.Equal("AB1234", profile.PassportNo);
        Assert.Equal("British", profile.Nationality);
        Assert.Equal(0, setup.Db.LocalProfiles.Count());
    }

    // ---- Invalid data ----

    [Fact]
    public void Invalid_data_is_rejected_with_all_reasons_and_no_code_is_sent()
    {
        using var setup = new RegistrationTestSetup();
        var request = LocalRequest(phone: "0412345678");
        request.Password = "123";

        var result = setup.Service.RegisterAccount(request);

        Assert.False(result.Ok);
        Assert.Equal(2, result.Errors.Count);
        Assert.Empty(setup.Sender.Sent);
        Assert.Equal(0, setup.Db.Users.Count());
    }

    // ---- T-43: duplicate identifier ----

    [Fact]
    public void Duplicate_phone_is_rejected_and_no_second_row_is_created()
    {
        using var setup = new RegistrationTestSetup();
        Register(setup, LocalRequest());
        int codesBefore = setup.Sender.Sent.Count;

        var result = setup.Service.RegisterAccount(LocalRequest());

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("already registered"));
        Assert.Equal(codesBefore, setup.Sender.Sent.Count);
        Assert.Equal(1, setup.Db.Users.Count());
    }

    [Fact]
    public void Duplicate_passport_is_rejected_even_with_different_letter_case()
    {
        using var setup = new RegistrationTestSetup();
        Register(setup, TouristRequest(passport: "AB1234"));

        var result = setup.Service.RegisterAccount(TouristRequest(passport: "ab1234"));

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("already registered"));
        Assert.Equal(1, setup.Db.Users.Count());
        Assert.Equal(1, setup.Db.TouristProfiles.Count());
    }

    [Fact]
    public void Identifier_of_a_deactivated_account_cannot_be_registered_again()
    {
        using var setup = new RegistrationTestSetup();
        Register(setup, LocalRequest());
        setup.Db.Users.Single().IsActive = false;
        setup.Db.SaveChanges();

        var result = setup.Service.RegisterAccount(LocalRequest());

        Assert.False(result.Ok);
        Assert.Equal(1, setup.Db.Users.Count());
    }

    // ---- T-44: invalid email, wrong code, expired code ----

    [Fact]
    public void Tourist_with_invalid_email_is_rejected()
    {
        using var setup = new RegistrationTestSetup();

        var result = setup.Service.RegisterAccount(TouristRequest(email: "abc.com"));

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("Email"));
        Assert.Equal(0, setup.Db.Users.Count());
    }

    [Theory]
    [InlineData(Roles.Local)]
    [InlineData(Roles.Tourist)]
    public void Wrong_code_leaves_no_account(string type)
    {
        using var setup = new RegistrationTestSetup();
        var request = type == Roles.Local ? LocalRequest() : TouristRequest();
        var started = setup.Service.RegisterAccount(request);
        string code = started.Value!.DemoCode!;
        string wrong = code == "000000" ? "000001" : "000000";

        var result = setup.Service.ConfirmRegistration(started.Value.Identifier, wrong);

        Assert.False(result.Ok);
        Assert.Equal(0, setup.Db.Users.Count());
        Assert.Equal(0, setup.Db.LocalProfiles.Count());
        Assert.Equal(0, setup.Db.TouristProfiles.Count());
    }

    [Fact]
    public void Expired_code_leaves_no_account()
    {
        using var setup = new RegistrationTestSetup();
        var started = setup.Service.RegisterAccount(LocalRequest());
        setup.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var result = setup.Service.ConfirmRegistration(started.Value!.Identifier, started.Value.DemoCode!);

        Assert.False(result.Ok);
        Assert.Equal(0, setup.Db.Users.Count());
    }

    [Fact]
    public void Confirm_without_a_registration_is_rejected()
    {
        using var setup = new RegistrationTestSetup();

        var result = setup.Service.ConfirmRegistration("0312345678", "123456");

        Assert.False(result.Ok);
        Assert.Equal(0, setup.Db.Users.Count());
    }

    [Fact]
    public void A_correct_code_cannot_create_the_account_twice()
    {
        using var setup = new RegistrationTestSetup();
        var started = setup.Service.RegisterAccount(LocalRequest());
        string code = started.Value!.DemoCode!;

        Assert.True(setup.Service.ConfirmRegistration("0312345678", code).Ok);
        Assert.False(setup.Service.ConfirmRegistration("0312345678", code).Ok);
        Assert.Equal(1, setup.Db.Users.Count());
    }
}
