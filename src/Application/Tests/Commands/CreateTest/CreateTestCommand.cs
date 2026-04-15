using backend.Application.Common.Interfaces;
using backend.Domain.Entities;

namespace backend.Application.Tests.Commands.CreateTest;

public record CreateTestCommand : IRequest<int>
{
    public string  Name        { get; init; } = default!;
    public string? Description { get; init; }
}

public class CreateTestCommandValidator : AbstractValidator<CreateTestCommand>
{
    public CreateTestCommandValidator(IApplicationDbContext context)
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(async (name, ct) =>
                !await context.Tests.AnyAsync(t => t.Name == name, ct))
            .WithMessage("A test with this name already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(2000)
            .When(c => c.Description is not null);
    }
}

public class CreateTestCommandHandler : IRequestHandler<CreateTestCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateTestCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        CreateTestCommand request, CancellationToken cancellationToken)
    {
        var test = new Test
        {
            Name        = request.Name,
            Description = request.Description,
            IsPublished = false,
            IsDeleted   = false
        };

        _context.Tests.Add(test);
        await _context.SaveChangesAsync(cancellationToken);

        return test.Id;
    }
}
