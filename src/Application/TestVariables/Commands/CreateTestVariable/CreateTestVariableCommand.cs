using backend.Application.Common.Interfaces;
using backend.Application.TestVariables.Queries.GetTestVariables;
using backend.Domain.Entities;

namespace backend.Application.TestVariables.Commands.CreateTestVariable;

public record CreateTestVariableCommand : IRequest<TestVariableDto>
{
    public int    TestId       { get; init; }
    public string Name         { get; init; } = default!;
    public string Key          { get; init; } = default!;
    public double DefaultValue { get; init; } = 0;
}

public class CreateTestVariableCommandValidator : AbstractValidator<CreateTestVariableCommand>
{
    public CreateTestVariableCommandValidator(IApplicationDbContext context)
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(c => c.Key)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^[A-Za-z_][A-Za-z0-9_]*$")
            .WithMessage("Key must be a valid identifier (letters, digits, underscores; cannot start with a digit).")
            .MustAsync(async (cmd, key, ct) =>
                !await context.TestVariables
                    .AnyAsync(v => v.TestId == cmd.TestId && v.Key == key, ct))
            .WithMessage("A variable with this key already exists for this test.");
    }
}

public class CreateTestVariableCommandHandler : IRequestHandler<CreateTestVariableCommand, TestVariableDto>
{
    private readonly IApplicationDbContext _context;

    public CreateTestVariableCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<TestVariableDto> Handle(
        CreateTestVariableCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NotFound(
            request.TestId,
            await _context.Tests.FirstOrDefaultAsync(t => t.Id == request.TestId, cancellationToken));

        var variable = new TestVariable
        {
            TestId       = request.TestId,
            Name         = request.Name,
            Key          = request.Key,
            DefaultValue = request.DefaultValue,
            IsDeleted    = false
        };

        _context.TestVariables.Add(variable);
        await _context.SaveChangesAsync(cancellationToken);

        return new TestVariableDto(
            variable.Id, variable.TestId, variable.Name, variable.Key, variable.DefaultValue);
    }
}
