using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Care;

namespace KitPlatform.Packs.Care.Infrastructure;

internal sealed class CareEventService : ICareEventService
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "medication_reminder_scheduled",
        "medication_adherence_recorded",
        "medication_adherence_missed",
        "repurchase_suggested",
        "repurchase_converted",
        "chronic_cohort_assigned",
        "chronic_cohort_removed",
        "follow_up_due",
        "follow_up_completed",
        "referral_linked",
        "referral_completed",
        "booking_linked",
        "shift_quality_flagged",
        "academy_progress",
        "health_education_delivered",
        "care_note",
        "other",
    };

    private readonly CareEventRepository _repo;
    private readonly ITenantContext _tenant;

    public CareEventService(CareEventRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public Task<IReadOnlyList<CareEventDto>> ListEventsAsync(
        Guid? customerId,
        string? eventType,
        int limit,
        CancellationToken cancellationToken = default) =>
        _repo.ListAsync(customerId, eventType, limit, cancellationToken);

    public async Task<CareEventDto> CreateEventAsync(
        CreateCareEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EventType) || !AllowedTypes.Contains(request.EventType.Trim()))
            throw new ArgumentException("eventType không hợp lệ.");

        var normalized = request with { EventType = request.EventType.Trim() };
        return await _repo.InsertAsync(normalized, _tenant.UserId, cancellationToken);
    }
}
