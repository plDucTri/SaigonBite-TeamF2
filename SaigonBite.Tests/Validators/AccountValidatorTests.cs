using SaigonBite.Helpers;
using SaigonBite.Validators;

namespace SaigonBite.Tests.Validators;

public class AccountValidatorTests
{
    private readonly AccountValidator _validator = new();

    private static AccountRequest Local(string phone = "0312345678", string password = "password1") => new()
    {
        Type = Roles.Local,
        Identifier = phone,
        FullName = "Nguyen Van A",
        Password = password
    };

    private static AccountRequest Tourist(string passport = "AB1234", string email = "john@example.com",
        string nationality = "British", string password = "password1") => new()
    {
        Type = Roles.Tourist,
        Identifier = passport,
        FullName = "John Smith",
        Password = password,
        Nationality = nationality,
        Email = email,
        HotelName = "Sunrise Hotel",
        RoomNo = "305"
    };

    // ---- Phone number: 10 digits starting 03, 05, 07, 08 or 09 ----

    [Theory]
    [InlineData("031234567", false)]    // 9 digits
    [InlineData("0312345678", true)]    // 10 digits, prefix 03
    [InlineData("03123456789", false)]  // 11 digits
    [InlineData("0412345678", false)]   // prefix 04
    [InlineData("0512345678", true)]
    [InlineData("0712345678", true)]
    [InlineData("0812345678", true)]
    [InlineData("0912345678", true)]
    [InlineData("0312 45678", false)]   // space inside
    [InlineData("031234567a", false)]   // letter
    [InlineData("0312345678\n", false)] // trailing newline
    [InlineData("", false)]
    public void Local_phone_follows_BR18(string phone, bool valid)
    {
        var errors = _validator.Validate(Local(phone));

        if (valid)
            Assert.Empty(errors);
        else
            Assert.Contains(errors, e => e.Contains("Phone"));
    }

    // ---- Passport: 6 to 9 letters or digits ----

    [Theory]
    [InlineData("AB123", false)]        // 5
    [InlineData("AB1234", true)]        // 6
    [InlineData("AB1234567", true)]     // 9
    [InlineData("AB12345678", false)]   // 10
    [InlineData("ab1234", true)]        // lower case is allowed (stored in upper case later)
    [InlineData("AB-1234", false)]
    [InlineData("AB 1234", false)]
    [InlineData("", false)]
    public void Tourist_passport_follows_BR18(string passport, bool valid)
    {
        var errors = _validator.Validate(Tourist(passport: passport));

        if (valid)
            Assert.Empty(errors);
        else
            Assert.Contains(errors, e => e.Contains("Passport"));
    }

    // ---- Password: at least 8 characters ----

    [Theory]
    [InlineData("1234567", false)]  // 7
    [InlineData("12345678", true)]  // 8
    [InlineData("", false)]
    public void Password_needs_at_least_8_characters_for_both_types(string password, bool valid)
    {
        var local = _validator.Validate(Local(password: password));
        var tourist = _validator.Validate(Tourist(password: password));

        if (valid)
        {
            Assert.Empty(local);
            Assert.Empty(tourist);
        }
        else
        {
            Assert.Contains(local, e => e.Contains("Password"));
            Assert.Contains(tourist, e => e.Contains("Password"));
        }
    }

    [Fact]
    public void Edit_accepts_an_empty_password()
    {
        var errors = _validator.Validate(Local(password: ""), passwordRequired: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void Edit_still_rejects_a_short_new_password()
    {
        var errors = _validator.Validate(Local(password: "1234567"), passwordRequired: false);

        Assert.Contains(errors, e => e.Contains("Password"));
    }

    // ---- Email (part of T-44) ----

    [Theory]
    [InlineData("abc.com", false)]               // no @
    [InlineData("john@example", false)]          // no dot in the domain
    [InlineData("john@", false)]
    [InlineData("Name <john@example.com>", false)]
    [InlineData("", false)]
    [InlineData("john@example.com", true)]
    public void Tourist_email_must_be_valid(string email, bool valid)
    {
        var errors = _validator.Validate(Tourist(email: email));

        if (valid)
            Assert.Empty(errors);
        else
            Assert.Contains(errors, e => e.Contains("Email"));
    }

    // ---- Other BR18 rules ----

    [Fact]
    public void Tourist_nationality_is_required()
    {
        var errors = _validator.Validate(Tourist(nationality: " "));

        Assert.Contains(errors, e => e.Contains("Nationality"));
    }

    [Fact]
    public void Full_name_is_required()
    {
        var request = Local();
        request.FullName = "";

        var errors = _validator.Validate(request);

        Assert.Contains(errors, e => e.Contains("Full name"));
    }

    [Fact]
    public void Unknown_customer_type_is_rejected()
    {
        var request = Local();
        request.Type = Roles.Admin;

        var errors = _validator.Validate(request);

        Assert.Contains(errors, e => e.Contains("type"));
    }

    [Fact]
    public void All_violations_are_returned_not_just_the_first()
    {
        var request = new AccountRequest { Type = Roles.Tourist, Identifier = "AB1", Password = "123" };

        var errors = _validator.Validate(request);

        Assert.Equal(5, errors.Count); // passport, nationality, email, full name, password
    }

    [Fact]
    public void Local_rules_do_not_ask_for_tourist_fields()
    {
        var errors = _validator.Validate(Local()); // no nationality, no email

        Assert.Empty(errors);
    }
}
