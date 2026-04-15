using backend.Application.Common.Interfaces;

namespace backend.Application.Tests.Commands.UpdateTest;

public record UpdateTestCommand : IRequest
{
    public int     Id          { get; init; }
    public string  Name        { get; init; } = default!;
    public string? Description { get; init; }
    public bool    IsPublished { get; init; }
}

public class UpdateTestCommandValidator : AbstractValidator<UpdateTestCommand>
{
    public UpdateTestCommandValidator(IApplicationDbContext context)
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(async (cmd, name, ct) =>
                !await context.Tests.AnyAsync(t => t.Name == name && t.Id != cmd.Id, ct))
            .WithMessage("A test with this name already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(2000)
            .When(c => c.Description is not null);
    }
}

public class UpdateTestCommandHandler : IRequestHandler<UpdateTestCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateTestCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(UpdateTestCommand request, CancellationToken cancellationToken)
    {
        var test = await _context.Tests
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, test);

        test.Name        = request.Name;
        test.Description = request.Description;
        test.IsPublished = request.IsPublished;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
