using FluentAssertions;
using TinyLang.Dtos;

namespace TinyLang.UnitTests;

public sealed class AuthValidatorsTests
{
    [Fact]
    public async Task RegisterShouldRejectWeakPasswordAndInvalidCode()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            Email = "learner@example.com",
            Password = "short",
            VerificationCode = "ABC123"
        };

        var result = await validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(RegisterRequest.Password));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(RegisterRequest.VerificationCode));
    }
}
