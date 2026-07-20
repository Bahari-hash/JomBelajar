using FluentValidation;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Nickname)
            .MaximumLength(60).WithErrKey(ErrorCodes.NicknameLengthLimit);

        RuleFor(x => x.Bio)
            .MaximumLength(500).WithErrKey(ErrorCodes.BioLengthLimit);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(500).WithErrKey(ErrorCodes.AvatarUrlLengthLimit)
            .Must(BeValidAvatarUrl).WithErrKey(ErrorCodes.AvatarUrlFormatInvalid);
    }

    private static bool BeValidAvatarUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithErrKey(ErrorCodes.RoleRequired)
            .Must(BeValidRole).WithErrKey(ErrorCodes.RoleInvalid);
    }

    private static bool BeValidRole(string role)
        => Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed);
}
