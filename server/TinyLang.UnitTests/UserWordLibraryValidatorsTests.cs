using FluentAssertions;
using TinyLang.Dtos;

namespace TinyLang.UnitTests;

public sealed class UserWordLibraryValidatorsTests
{
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public async Task ListRequestShouldValidatePagination(
        int page,
        int pageSize,
        bool expectedValid)
    {
        var result = await new UserWordLibraryListRequestValidator()
            .ValidateAsync(
                new UserWordLibraryListRequest
                {
                    Page = page,
                    PageSize = pageSize
                },
                TestContext.Current.CancellationToken);

        result.IsValid.Should().Be(expectedValid);
    }
}
