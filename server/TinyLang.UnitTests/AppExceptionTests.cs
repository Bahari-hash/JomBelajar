using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using TinyLang.Exceptions;

namespace TinyLang.UnitTests;

/// <summary>
/// Verifies application exceptions preserve stable error identity independently from client messages.
/// </summary>
public sealed class AppExceptionTests
{
    /// <summary>
    /// Verifies every application exception factory retains its supplied error code and HTTP status.
    /// </summary>
    [Fact]
    public void ApplicationExceptionsShouldPreserveErrorCodes()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Title"] = ["Title is required."]
        };
        BaseAppException[] exceptions =
        [
            ConflictException.Create(ErrorCodes.ArticleConcurrencyConflict),
            ForbiddenException.Create(ErrorCodes.ArticleMediaOwnershipMismatch),
            NotFoundException.Create(ErrorCodes.ArticleNotFound),
            new RequestValidationException(ErrorCodes.RequestValidationFailed, errors),
            TooManyRequestsException.Create(ErrorCodes.UnexpectedError),
            UnauthorizedException.Create(ErrorCodes.TokenInvalid),
            UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable)
        ];

        exceptions.Select(exception => exception.ErrorCode).Should().Equal(
            ErrorCodes.ArticleConcurrencyConflict,
            ErrorCodes.ArticleMediaOwnershipMismatch,
            ErrorCodes.ArticleNotFound,
            ErrorCodes.RequestValidationFailed,
            ErrorCodes.UnexpectedError,
            ErrorCodes.TokenInvalid,
            ErrorCodes.ObjectStorageUnavailable);
        exceptions.Select(exception => exception.StatusCode).Should().Equal(
            StatusCodes.Status409Conflict,
            StatusCodes.Status403Forbidden,
            StatusCodes.Status404NotFound,
            StatusCodes.Status400BadRequest,
            StatusCodes.Status429TooManyRequests,
            StatusCodes.Status401Unauthorized,
            StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Verifies a custom localized detail cannot replace the stable machine-readable error code.
    /// </summary>
    [Fact]
    public void CustomMessageShouldNotChangeErrorCode()
    {
        const string customMessage = "The article changed while it was being edited.";

        var exception = ConflictException.Create(
            ErrorCodes.ArticleConcurrencyConflict,
            customMessage);

        exception.ErrorCode.Should().Be(ErrorCodes.ArticleConcurrencyConflict);
        exception.ErrorMessages.Should().Be(customMessage);
        exception.Message.Should().Be(customMessage);
    }
}
