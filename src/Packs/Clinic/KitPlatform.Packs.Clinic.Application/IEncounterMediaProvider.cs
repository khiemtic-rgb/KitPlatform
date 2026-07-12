namespace KitPlatform.Packs.Clinic;

/// <summary>
/// CL3 media layer — Null in A; WebRTC implementation registers for remote_video (B).
/// </summary>
public interface IEncounterMediaProvider
{
    string? ProviderCode { get; }

    Task<EncounterMediaStartResult> StartAsync(
        Guid visitId,
        string encounterModality,
        CancellationToken cancellationToken = default);

    Task EndAsync(Guid visitId, CancellationToken cancellationToken = default);
}

public sealed record EncounterMediaStartResult(
    string SessionStatus,
    string? MediaProvider,
    string? ProviderSessionId,
    string? JoinUrlPatient,
    string? JoinUrlClinician);

public sealed record EncounterSessionDto(
    Guid Id,
    Guid VisitId,
    Guid? AppointmentId,
    string SessionStatus,
    string? MediaProvider,
    string? ProviderSessionId,
    string? JoinUrlPatient,
    string? JoinUrlClinician,
    DateTime? StartedAt,
    DateTime? EndedAt);
