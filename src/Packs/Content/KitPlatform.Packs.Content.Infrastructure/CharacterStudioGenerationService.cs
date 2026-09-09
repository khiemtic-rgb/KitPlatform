using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterStudioGenerationService : ICharacterStudioGenerationService
{
    private readonly ICharacterStudioOrchestrator _studio;

    public CharacterStudioGenerationService(ICharacterStudioOrchestrator studio) =>
        _studio = studio;

    public IReadOnlyList<string> RunRegression() =>
        CharacterStudioUnifiedGenerationV1Regression.Run();

    public IReadOnlyList<string> RunAgeConsistencyRegression() =>
        CharacterAgeConsistencyV1Regression.Run();

    public IReadOnlyList<string> RunAgeGenerationIntegrationRegression() =>
        CharacterAgeGenerationIntegrationV1Regression.Run();

    public IReadOnlyList<string> RunAgeGateRegression() =>
        CharacterAgeGateV1Regression.Run();

    public IReadOnlyList<string> RunAppearanceProfileRegression() =>
        CharacterAppearanceProfileV1Regression.Run();

    public IReadOnlyList<string> RunCharacterDesignLanguageRegression() =>
        CharacterDesignLanguageV1Regression.Run();

    public IReadOnlyList<string> RunCharacterDesignLanguageV2Regression() =>
        CharacterDesignLanguageV2Regression.Run();

    public IReadOnlyList<string> RunVisualUniverseAuthorityRegression() =>
        FamixaVisualUniverseAuthorityV1Regression.Run();

    public IReadOnlyList<string> RunMasterRevisionRegression() =>
        CharacterMasterRevisionV1Regression.Run()
            .Concat(CharacterMasterRevisionDirectorReviewV1Regression.Run())
            .ToList();

    public IReadOnlyList<string> RunMasterRevisionDirectorReviewRegression() =>
        CharacterMasterRevisionDirectorReviewV1Regression.Run();

    public IReadOnlyList<string> RunFirstMasterVisualIngressRegression() =>
        CharacterFirstMasterVisualIngressV1Regression.Run();

    public async Task<CharacterStudioReferenceGenerationResultDto> GenerateCharacterReferenceSet(
        string characterId,
        CharacterStudioReferenceGenerationRequestDto request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var provider = string.IsNullOrWhiteSpace(request.Provider)
            ? CharacterStudioUnifiedGenerationV1Rules.DefaultProvider
            : request.Provider.Trim().ToUpperInvariant();
        var era = string.IsNullOrWhiteSpace(request.EraId)
            ? CharacterStudioV1Rules.DefaultEra
            : request.EraId.Trim().ToUpperInvariant();

        var current = await _studio.GetAsync(characterId, provider, era, cancellationToken);
        var action = new CharacterStudioActionRequestDto(
            request.Confirm, provider, era, null, null, true);

        CharacterStudioCharacterDto row;
        var staleCrp = current.MasterRevision?.CrpStale == true;
        if (staleCrp)
            action = action with { RegenerateAll = true };
        if (staleCrp || CharacterStudioV1Rules.MayRegenerate(
                current.Status,
                current.OfficialLocked,
                CharacterStudioV1Rules.IsRejected(current.Status),
                string.Equals(current.AgeConsistencyStatus, CharacterAgeConsistencyV1Rules.StatusFail,
                    StringComparison.OrdinalIgnoreCase),
                staleCrp))
            row = await _studio.RegenerateAsync(characterId, action, actor, cancellationToken);
        else
            row = await _studio.GenerateAsync(characterId, action, actor, cancellationToken);

        var duplicate = string.Equals(row.GateCode, CharacterStudioUnifiedGenerationV1Rules.GateDuplicate,
            StringComparison.OrdinalIgnoreCase);
        var artifacts = row.ArtifactIds
            ?? row.Slots.Where(s => s.Present && !string.IsNullOrWhiteSpace(s.Sha256))
                .Select(s => s.Sha256!)
                .ToList();

        return new CharacterStudioReferenceGenerationResultDto(
            CharacterStudioUnifiedGenerationV1Rules.DocumentId,
            row.Status,
            row.CharacterId,
            row.ReferenceSetId,
            row.GenerationExecutionId,
            artifacts,
            new CharacterStudioAuthorityShasDto(
                row.ProjectVisualStyleSha,
                row.IdentitySha256,
                row.MasterSha256,
                row.DnaSha256,
                row.PrpSha256,
                row.CrpSha256,
                row.Fingerprint),
            duplicate,
            row.GateCode,
            row.ProviderCalled,
            row.GeminiCalled,
            row.AutoApproved,
            row.AutoLocked,
            row.ProductionGeneration,
            row.VideoGeneration,
            row.LogicalGenerationCount,
            row.ConsistencyPass,
            row.ProviderRequestId,
            row.GateCode,
            row.StaffMessage,
            row);
    }
}
