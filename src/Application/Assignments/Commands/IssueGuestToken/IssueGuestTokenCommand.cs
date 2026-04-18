using backend.Application.Common.Interfaces;
using backend.Domain.Enums;
using AppValidationException = backend.Application.Common.Exceptions.ValidationException;

namespace backend.Application.Assignments.Commands.IssueGuestToken;

/// <summary>
/// Exchanges an Assignment's magic-link AccessKey for a short-lived guest JWT.
/// The returned token carries <c>assignment_id</c> and <c>tenant_id</c> claims
/// and has no role — it can only be used against the execution endpoints.
/// </summary>
public record IssueGuestTokenCommand(string AccessKey) : IRequest<GuestTokenResponse>;

public record GuestTokenResponse(string AccessToken, int ExpiresInSeconds);

public class IssueGuestTokenCommandValidator : AbstractValidator<IssueGuestTokenCommand>
{
    public IssueGuestTokenCommandValidator()
    {
        RuleFor(c => c.AccessKey).NotEmpty();
    }
}

public class IssueGuestTokenCommandHandler
    : IRequestHandler<IssueGuestTokenCommand, GuestTokenResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService           _jwtService;

    private const int ExpiryMinutes = 90; // long enough for a full test session

    public IssueGuestTokenCommandHandler(IApplicationDbContext context, IJwtService jwtService)
    {
        _context    = context;
        _jwtService = jwtService;
    }

    public async Task<GuestTokenResponse> Handle(
        IssueGuestTokenCommand request, CancellationToken cancellationToken)
    {
        // Bypass global tenant + IsDeleted filters — the TenantEmployee is anonymous at this point.
        var assignment = await _context.Assignments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.AccessKey == request.AccessKey, cancellationToken);

        if (assignment is null || assignment.IsDeleted)
            throw new UnauthorizedAccessException("Invalid or expired access key.");

        if (assignment.Status == AssignmentStatus.Completed ||
            assignment.Status == AssignmentStatus.AwaitingManualGrading)
            throw new AppValidationException([
                new FluentValidation.Results.ValidationFailure(
                    nameof(request.AccessKey),
                    "This assignment has already been completed and cannot be reopened.")
                {
                    ErrorCode = "Assignment.AlreadySubmitted"
                }
            ]);

        var token = _jwtService.GenerateGuestToken(
            assignmentId:  assignment.Id,
            tenantId:      assignment.TenantId,
            expiryMinutes: ExpiryMinutes);

        return new GuestTokenResponse(token, ExpiryMinutes * 60);
    }
}
