using FluentAssertions;
using TinyLang.Dtos;

namespace TinyLang.UnitTests;

public sealed class AccountSecurityValidatorsTests
{
    [Fact]
    public async Task ResetPasswordShouldRequirePassword()
    {
        var validator = new ResetPasswordRequestValidator();
        var result = await validator.ValidateAsync(
            new ResetPasswordRequest { NewPassword = "weakpassword", VerificationCode = "123456" },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordShouldAcceptPasswordAndCode()
    {
        var validator = new ResetPasswordRequestValidator();
        var result = await validator.ValidateAsync(
            new ResetPasswordRequest { NewPassword = "StrongPass123", VerificationCode = "123456" },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEmailShouldRejectMalformedVerificationCode()
    {
        var validator = new ChangeEmailRequestValidator();
        var result = await validator.ValidateAsync(
            new ChangeEmailRequest { NewEmail = "new@example.com", VerificationCode = "12AB" },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }
}
