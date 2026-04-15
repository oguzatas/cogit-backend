using backend.Application.Common.Interfaces;

namespace backend.Application.TestVariables.Commands.UpdateTestVariable;

public record UpdateTestVariableCommand : IRequest
{
    public int    Id           { get; init; }
    public string Name         { get; init; } = default!;
    public string Key          { get; init; } = default!;
    public double DefaultValue { get; init; }
}

public class UpdateTestVariableCommandValidator : AbstractValidator<UpdateTestVariableCommand>
{
    public UpdateTestVariableCommandValidator(IApplicationDbContext context)
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(c => c.Key)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^[A-Za-z_][A-Za-z0-9_]*$")
            .WithMessage("Key must be a valid identifier.")
            // Unique per test, excluding self.
            .MustAsync(async (cmd, key, ct) =>
            {
                var variable = await context.TestVariables
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == cmd.Id, ct);

                if (variable is null) return true; // NotFound guard fires in handler

                return !await context.TestVariables
                    .AnyAsync(v => v.TestId == variable.TestId
                               && v.Key    == key
                               && v.Id     != cmd.Id, ct);
            })
            .WithMessage("A variable with this key already exists for this test.");
    }
}

public class UpdateTestVariableCommandHandler : IRequestHandler<UpdateTestVariableCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateTestVariableCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(UpdateTestVariableCommand request, CancellationToken cancellationToken)
    {
        var variable = await _context.TestVariables
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, variable);

        variable.Name         = request.Name;
        variable.Key          = request.Key;
        variable.DefaultValue = request.DefaultValue;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
