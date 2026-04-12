using backend.Application.Common.Interfaces;

namespace backend.Application.InviteCodes.Queries.GetActiveInviteCodes;

public record GetActiveInviteCodesQuery(int TenantId, int DepartmentId)
    : IRequest<List<InviteCodeDto>>;

public record InviteCodeDto(
    int Id,
    int DepartmentId,
    string Code,
    int UsageCount,
    int? MaxUses,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset Created);

public class GetActiveInviteCodesQueryHandler
    : IRequestHandler<GetActiveInviteCodesQuery, List<InviteCodeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetActiveInviteCodesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<InviteCodeDto>> Handle(
        GetActiveInviteCodesQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        return await _context.InviteCodes
            .AsNoTracking()
            .Where(c =>
                c.TenantId     == request.TenantId &&
                c.DepartmentId == request.DepartmentId &&
                !c.IsRevoked &&
                (c.ExpiresAt == null || c.ExpiresAt > now) &&
                (c.MaxUses == null || c.UsageCount < c.MaxUses))
            .OrderByDescending(c => c.Created)
            .Select(c => new InviteCodeDto(
                c.Id,
                c.DepartmentId,
                c.Code,
                c.UsageCount,
                c.MaxUses,
                c.ExpiresAt,
                c.Created))
            .ToListAsync(cancellationToken);
    }
}
