using backend.Application.Common.Interfaces;

namespace backend.Application.Questions.Commands.DeleteQuestion;

/// <summary>
/// Soft-deletes a Question. The row and all its child Options/OptionPoints remain
/// in the database but are hidden by the global query filter.
/// </summary>
public record DeleteQuestionCommand(int Id) : IRequest;

public class DeleteQuestionCommandHandler : IRequestHandler<DeleteQuestionCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteQuestionCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(DeleteQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _context.Questions
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, question);

        question.IsDeleted = true;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
