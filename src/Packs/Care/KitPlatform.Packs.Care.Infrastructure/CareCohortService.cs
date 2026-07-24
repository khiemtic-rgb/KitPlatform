using System.Text.Json;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Care;

namespace KitPlatform.Packs.Care.Infrastructure;

internal sealed class CareCohortService : ICareCohortService
{
    private readonly CareCohortRepository _repo;
    private readonly CareEventRepository _events;
    private readonly ITenantContext _tenant;

    public CareCohortService(
        CareCohortRepository repo,
        CareEventRepository events,
        ITenantContext tenant)
    {
        _repo = repo;
        _events = events;
        _tenant = tenant;
    }

    public Task<IReadOnlyList<CareCohortDefinitionDto>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListDefinitionsAsync(cancellationToken);

    public Task<IReadOnlyList<CareCohortMembershipDto>> ListMembershipsAsync(
        Guid? cohortId,
        CancellationToken cancellationToken = default) =>
        _repo.ListMembershipsAsync(cohortId, cancellationToken);

    public async Task<CareCohortMembershipDto> AssignAsync(
        AssignCareCohortRequest request,
        CancellationToken cancellationToken = default)
    {
        var membership = await _repo.AssignAsync(request, _tenant.UserId, cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            cohortCode = membership.CohortCode,
            source = membership.Source,
        });
        await _events.InsertAsync(
            new CreateCareEventRequest(
                EventType: "chronic_cohort_assigned",
                Tier: 2,
                CustomerId: request.CustomerId,
                SourceSystem: "care_os",
                SourceRefType: "care_cohort_membership",
                SourceRefId: membership.Id,
                PayloadJson: payload),
            _tenant.UserId,
            cancellationToken);
        return membership;
    }
}
