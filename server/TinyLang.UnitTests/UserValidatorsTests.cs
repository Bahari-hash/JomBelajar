using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;

namespace TinyLang.UnitTests;

public sealed class UserValidatorsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task WordStudySettingsShouldAcceptDocumentedBoundaries(int count)
    {
        var validator = new UpdateWordStudySettingsRequestValidator();

        var result = await validator.ValidateAsync(
            new UpdateWordStudySettingsRequest { DailyWordStudyCount = count },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task WordStudySettingsShouldRejectValuesOutsideDocumentedRange(int count)
    {
        var validator = new UpdateWordStudySettingsRequestValidator();

        var result = await validator.ValidateAsync(
            new UpdateWordStudySettingsRequest { DailyWordStudyCount = count },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ProfileShouldRejectEmptyAvatarResourceId()
    {
        var validator = new UpdateProfileRequestValidator();
        var request = new UpdateProfileRequest
        {
            AvatarMediaResourceId = Guid.Empty
        };

        var result = await validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ProfileShouldAcceptDocumentedFieldBoundaries()
    {
        var validator = new UpdateProfileRequestValidator();

        var result = await validator.ValidateAsync(
            new UpdateProfileRequest
            {
                Nickname = new string('n', 60),
                Bio = new string('b', 500),
                AvatarMediaResourceId = Guid.NewGuid()
            },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ProfileShouldRejectFieldsBeyondDocumentedBoundaries()
    {
        var validator = new UpdateProfileRequestValidator();

        var result = await validator.ValidateAsync(
            new UpdateProfileRequest
            {
                Nickname = new string('n', 61),
                Bio = new string('b', 501),
                AvatarMediaResourceId = null
            },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    public async Task RoleShouldAcceptExactSupportedRoleNames(string role)
    {
        var validator = new UpdateRoleRequestValidator();
        var result = await validator.ValidateAsync(
            new UpdateRoleRequest { Role = role },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("user")]
    [InlineData("admin")]
    [InlineData("Editor")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("SuperAdmin")]
    public async Task RoleShouldRejectNonCanonicalRoleNames(string role)
    {
        var validator = new UpdateRoleRequestValidator();
        var result = await validator.ValidateAsync(
            new UpdateRoleRequest { Role = role },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RoleShouldRejectMissingRoleNames(string? role)
    {
        var validator = new UpdateRoleRequestValidator();

        var result = await validator.ValidateAsync(
            new UpdateRoleRequest { Role = role! },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task AdminListShouldAcceptDefaultsAndKnownFilters()
    {
        var validator = new AdminUserListRequestValidator();

        var defaultResult = await validator.ValidateAsync(
            new AdminUserListRequest(),
            TestContext.Current.CancellationToken);
        var filteredResult = await validator.ValidateAsync(
            new AdminUserListRequest
            {
                Page = 2,
                PageSize = 100,
                Keyword = " user@example.test ",
                Role = "Admin",
                Status = AdminUserStatus.Banned
            },
            TestContext.Current.CancellationToken);

        defaultResult.IsValid.Should().BeTrue();
        filteredResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task AdminListShouldRejectInvalidPagination(int page, int pageSize)
    {
        var validator = new AdminUserListRequestValidator();

        var result = await validator.ValidateAsync(
            new AdminUserListRequest { Page = page, PageSize = pageSize },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task AdminListShouldRejectLongOrControlCharacterKeyword()
    {
        var validator = new AdminUserListRequestValidator();

        var longResult = await validator.ValidateAsync(
            new AdminUserListRequest { Keyword = $"  {new string('a', 201)}  " },
            TestContext.Current.CancellationToken);
        var controlResult = await validator.ValidateAsync(
            new AdminUserListRequest { Keyword = "user\nname" },
            TestContext.Current.CancellationToken);

        longResult.IsValid.Should().BeFalse();
        controlResult.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task AdminListShouldRejectInvalidRoleAndStatusFilters()
    {
        var validator = new AdminUserListRequestValidator();

        var result = await validator.ValidateAsync(
            new AdminUserListRequest
            {
                Role = "1",
                Status = (AdminUserStatus)999
            },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(" reason ", true)]
    public async Task BanReasonShouldEnforceTrimmedRequiredValue(
        string? reason,
        bool expectedValid)
    {
        var validator = new BanUserRequestValidator();

        var result = await validator.ValidateAsync(
            new BanUserRequest { Reason = reason! },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public async Task BanReasonShouldEnforceTrimmedMaximumLength(
        int length,
        bool expectedValid)
    {
        var validator = new BanUserRequestValidator();

        var result = await validator.ValidateAsync(
            new BanUserRequest { Reason = $"  {new string('a', length)}  " },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().Be(expectedValid);
    }
}
