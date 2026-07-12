using SplitwiseCLI.Configuration;
using Xunit;

namespace SplitwiseCLI.Tests;

public class SecretProtectorTests
{
    [Fact]
    public void Protect_ThenUnprotect_RoundTrips()
    {
        var protectedValue = SecretProtector.Protect("hello world");

        Assert.Equal("hello world", SecretProtector.Unprotect(protectedValue));
    }

    [Fact]
    public void Protect_DoesNotReturnPlaintext()
    {
        var protectedValue = SecretProtector.Protect("hello world");

        Assert.DoesNotContain("hello world", protectedValue);
    }
}
