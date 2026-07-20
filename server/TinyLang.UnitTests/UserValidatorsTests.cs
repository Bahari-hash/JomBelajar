using FluentAssertions;
using TinyLang.Dtos;

namespace TinyLang.UnitTests;

public sealed class UserValidatorsTests
{
    [Fact]
    public async Task ProfileShouldRejectNonHttpAvatarUrl()
    {
        var validator = new UpdateProfileRequestValidator();
        var request = new UpdateProfileRequest
        {
            AvatarUrl = "javascript:alert(1)"
        };

        var result = await validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RoleShouldAcceptKnownRoleNamesCaseInsensitively()
    {
        var validator = new UpdateRoleRequestValidator();
        var result = await validator.ValidateAsync(
            new UpdateRoleRequest { Role = "editor" },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RoleShouldRejectUnknownRoleNames()
    {
        var validator = new UpdateRoleRequestValidator();
        var result = await validator.ValidateAsync(
            new UpdateRoleRequest { Role = "SuperAdmin" },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }
}
