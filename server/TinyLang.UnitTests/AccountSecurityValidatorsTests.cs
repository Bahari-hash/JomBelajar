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

    [Fact]
    public async Task ForgotPasswordShouldValidateEmailPasswordAndCode()
    {
        var validator = new ForgotPasswordRequestValidator();
        var result = await validator.ValidateAsync(
            new ForgotPasswordRequest
            {
                Email = "invalid",
                NewPassword = "short",
                VerificationCode = "12AB"
            },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(ForgotPasswordRequest.Email));
        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(ForgotPasswordRequest.NewPassword));
        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(ForgotPasswordRequest.VerificationCode));
    }

    [Fact]
    public async Task ForgotPasswordShouldAcceptValidRequest()
    {
        var validator = new ForgotPasswordRequestValidator();
        var result = await validator.ValidateAsync(
            new ForgotPasswordRequest
            {
                Email = "learner@example.test",
                NewPassword = "new-password",
                VerificationCode = "123456"
            },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }
}
