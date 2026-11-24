using SaigonBite.Helpers;

namespace SaigonBite.Tests.Helpers;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Stored_value_does_not_contain_the_plain_password()
    {
        const string password = "MySecret123";

        string stored = _hasher.Hash(password);

        Assert.DoesNotContain(password, stored);
        Assert.StartsWith("v1.", stored);
    }

    [Fact]
    public void Stored_value_fits_the_PasswordHash_column()
    {
        string stored = _hasher.Hash("MySecret123");

        Assert.True(stored.Length <= 200);
    }

    [Fact]
    public void Verify_is_true_for_the_right_password()
    {
        string stored = _hasher.Hash("MySecret123");

        Assert.True(_hasher.Verify("MySecret123", stored));
    }

    [Fact]
    public void Verify_is_false_for_a_wrong_password()
    {
        string stored = _hasher.Hash("MySecret123");

        Assert.False(_hasher.Verify("mysecret123", stored));
        Assert.False(_hasher.Verify("", stored));
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ()
    {
        string first = _hasher.Hash("MySecret123");
        string second = _hasher.Hash("MySecret123");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("MySecret123", first));
        Assert.True(_hasher.Verify("MySecret123", second));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("v1.abc.def.ghi")]
    [InlineData("v2.100000.AAAA.AAAA")]
    [InlineData("v1.100000.not-base64!.AAAA")]
    [InlineData("v1.0.AAAA.AAAA")]
    public void Verify_is_false_for_a_malformed_stored_value(string stored)
    {
        Assert.False(_hasher.Verify("MySecret123", stored));
    }

    [Fact]
    public void Hash_rejects_an_empty_password()
    {
        Assert.Throws<ArgumentException>(() => _hasher.Hash(""));
    }
}
