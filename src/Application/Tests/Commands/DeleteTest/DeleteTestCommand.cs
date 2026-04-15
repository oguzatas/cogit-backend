using backend.Application.Common.Interfaces;

namespace backend.Application.Tests.Commands.DeleteTest;

/// <summary>
/// Soft-deletes a Test by setting IsDeleted = true.
/// The row is retained in the database; global query filters hide it from all future queries.
/// </summary>
public record DeleteTestCommand(int Id) : IRequest;

public class DeleteTestCommandHandler : IRequestHandler<DeleteTestCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteTestCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(DeleteTestCommand request, CancellationToken cancellationToken)
    {
        var test = await _context.Tests
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, test);

        test.IsDeleted = true;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
