using FluentValidation;

namespace AttendanceSystem.Api.Services;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Login).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}

public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator() => RuleFor(x => x.FcmToken).NotEmpty().MaximumLength(512);
}

public class CheckInRequestValidator : AbstractValidator<CheckInRequest>
{
    public CheckInRequestValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Latitude).NotNull().InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).NotNull().InclusiveBetween(-180, 180);
    }
}

public class StartSessionRequestValidator : AbstractValidator<StartSessionRequest>
{
    public StartSessionRequestValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ClassroomId).NotEmpty();
    }
}
