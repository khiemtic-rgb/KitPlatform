using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed partial class ProductionCreateVideoService
{
    public async Task<CreateVideoSliceResult> CreateImageAsync(ProductionActionApiCommand command, CancellationToken ct = default)
    {
        ProductionIdentityRules.ForbidUrlIdentity(command.IdempotencyKey);
        var imported = command.MediaBytes is { Length: >= 32 };
        ProductionResolvedAi? tenantImage = null;
        if (!imported && _tenantAi is not null && await _tenantAi.GovernsAsync(command.Scope, ct))
            tenantImage = await _tenantAi.ResolveAsync(command.Scope, ProductionTenantAiCapabilities.Image, ct);
        var tenantStill = tenantImage is null
            && command.AuthorizePaidNetwork
            && ProductionScopeAnchorRules.IsTenantOwnedEp01Build(command.Scope.BuildId, command.Scope.EpisodeCode)
            && !imported;
        var credential = command.Credential;
        IImageExecutionAdapter adapter;
        string providerId;
        string modelId;
        if (imported)
        {
            adapter = _image;
            providerId = adapter.ProviderId;
            modelId = adapter.ModelId;
        }
        else if (tenantImage is not null)
        {
            if (_tenantImage is null
                || !string.Equals(tenantImage.ProviderId, _tenantImage.ProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(tenantImage.ModelId, _tenantImage.ModelId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionTenantAiReasons.ModelNotExecutable);
            if (tenantImage.Credential is null)
                throw new InvalidOperationException(ProductionTenantAiReasons.CredentialRequired);
            if (_officialPermits is null
                || !command.AuthorizePaidNetwork
                || !ProductionScopeAnchorRules.IsTenantOwnedEp01Build(command.Scope.BuildId, command.Scope.EpisodeCode))
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            adapter = _tenantImage;
            providerId = tenantImage.ProviderId;
            modelId = tenantImage.ModelId;
            credential = TenantCredential(command.Credential, tenantImage);
        }
        else if (tenantStill)
        {
            if (_geminiImage is null || _officialPermits is null)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            var model = await _geminiImage.RequireModelAsync(ct);
            adapter = _geminiImage;
            providerId = _geminiImage.ProviderId;
            modelId = model;
        }
        else
        {
            adapter = new FixtureImageAdapter();
            providerId = adapter.ProviderId;
            modelId = adapter.ModelId;
        }
        var paidStill = tenantStill || tenantImage is not null;
        if (adapter.IsPaid || adapter.RequiresNetwork)
        {
            if (!paidStill)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        }
        if (paidStill
            && ShortProductionQualityPipelineV1.Ep01Contract(command.Scope.ShotCode) is { DirectDialogue: true, LineComplete: false })
            throw new InvalidOperationException(ShortProductionQualityPipelineV1.SpokenLineIncomplete);
        var input = WithVoiceToken(ApplyFutureImageSpec(command.Scope.ShotCode, command.Input));
        if (paidStill && _lockedMasters is not null && command.MediaBytes is not { Length: >= 32 })
            input = await BindLockedMastersAsync(command.Scope.ShotCode, input, ct);
        var token = command.MediaBytes is { Length: >= 32 }
            ? KitVideoIntegrityRules.Sha256Hex(command.MediaBytes)
            : modelId + ":" + input.CharacterReferences;
        var idem = Idem(command, ProductionActions.CreateImage, token);
        if (paidStill && input.CharacterReferences.Contains(':', StringComparison.Ordinal))
            idem = FitIdem(idem, input.CharacterReferences);
        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Keyframe, ct);
        if (replay is not null) return replay;
        var imageSize = paidStill ? ProductionQualityTiers.NormalizeImage(command.ImageSize) : ProductionQualityTiers.ImageDraft;
        var userImageRetry = paidStill
            && command.AuthorizePaidNetwork
            && ProductionScopeAnchorRules.IsTenantOwnedEp01Build(command.Scope.BuildId, command.Scope.EpisodeCode);
        if (paidStill && _lockedMasters is not null)
        {
            var current = await GetCurrentKeyframeAsync(command.Scope, ct);
            if (!current.IsEmpty)
                throw new InvalidOperationException(ProductionLockedStillReference.Current);
        }
        if (paidStill)
        {
            var imageUsd = ProductionQualityTiers.ImageUsd(imageSize);
            await GuardTenantPaidStageAsync(
                command.Scope,
                ProductionActions.CreateImage,
                ProductionPaidStageLedger.ImageFingerprint(
                    imageSize, input.CharacterReferences, input.ActingBeat, input.VisualContract),
                imageUsd,
                ct);
            if (userImageRetry)
                await RequireTenantImageAuthorizationAsync(command.Scope, providerId, modelId, ct);
            else
                await RequireTenantIssuedPermitAsync(
                    command.Scope, ProductionActions.CreateImage, providerId, modelId, ct);
        }
        var (request, task, snap, attempt) = await OpenFrozenAsync(
            command.Scope, ProductionActions.CreateImage, input, command.CreatedBy, idem,
            providerId, modelId, null, ct, credential);
        if (tenantImage is not null
            && (snap.CredentialId != tenantImage.Credential!.CredentialId
                || snap.CredentialVersionId != tenantImage.Credential.CredentialVersionId))
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        if (paidStill)
        {
            var issued = await _officialPermits!.GetIssuedAsync(
                command.Scope.BuildId, command.Scope.ShotCode, ProductionActions.CreateImage, providerId, modelId, ct);
            if (issued is not null)
            {
                var consumed = await _officialPermits.TryConsumeAsync(
                    command.Scope.BuildId, command.Scope.ShotCode, ProductionActions.CreateImage,
                    providerId, modelId, ct);
                if (!consumed)
                    throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            }
            else if (!userImageRetry)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        }
        ProductionMediaResult executed;
        try
        {
            using (ProductionImageSizeChoice.Use(imageSize))
                executed = await adapter.ExecuteFrozenAsync(snap, command.MediaBytes, command.MediaMime, ct);
        }
        catch (Exception ex) when (paidStill)
        {
            await _os.FailAttemptAsync(attempt.AttemptId, ex.Message, ct);
            throw;
        }
        return await MaterializeMediaAsync(request, task, attempt, executed, keyframe: true, voice: false, bindVisible: false, ct);
    }

    async Task<ShotExecutionInput> BindLockedMastersAsync(string shotCode, ShotExecutionInput input, CancellationToken ct)
    {
        var people = ProductionLockedStillReference.Characters(input.VisualContract, shotCode);
        if (people.Count == 0) return input;
        var rows = new List<(string Id, string Sha)>(people.Count);
        foreach (var (id, name) in people)
        {
            var master = await _lockedMasters!.ReadOfficialAsync(id, name, ct);
            if (master is null || master.Bytes is not { Length: > 32 })
                throw new InvalidOperationException(ProductionLockedStillReference.Missing);
            rows.Add((id, master.Sha256));
        }
        return input with { CharacterReferences = ProductionLockedStillReference.Token(rows) };
    }

    public async Task<CreateVideoSliceResult> CreateVoiceAsync(ProductionActionApiCommand command, CancellationToken ct = default)
    {
        ProductionIdentityRules.ForbidUrlIdentity(
            command.Input.LineId, command.Input.VoiceId, command.Input.SpeakerCharacterId, command.Input.Story, command.IdempotencyKey);
        var officialFilm = ProductionScopeAnchorRules.IsOfficialFilm(command.Scope.BuildId);
        var tenantPaidVoice = !officialFilm
            && command.AuthorizePaidNetwork
            && ProductionScopeAnchorRules.IsTenantOwnedEp01Build(command.Scope.BuildId, command.Scope.EpisodeCode);
        var testIdentity = officialFilm && IsTestVoiceIdentity(command.Input);
        if (!officialFilm && !tenantPaidVoice)
        {
            if (string.IsNullOrWhiteSpace(command.Input.LineId) || string.IsNullOrWhiteSpace(command.Input.VoiceId))
                throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
            if (_voice.IsPaid || _voice.RequiresNetwork)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        }

        CreateVideoReadinessClaim? voiceClaim = officialFilm || testIdentity
            ? null
            : CreateVideoActionContractGate.ClaimFrom(command.Input) with
            {
                LineText = command.Input.Story,
                CharacterId = command.Input.SpeakerCharacterId,
            };
        var voiceVerdict = await _os.EvaluateCreateVoiceAsync(
            command.Scope, voiceClaim, ct, requireIssuedPermit: !officialFilm);
        if (!voiceVerdict.Available)
        {
            if (officialFilm && (testIdentity
                || string.IsNullOrWhiteSpace(command.Input.LineId)
                || string.IsNullOrWhiteSpace(command.Input.VoiceId)
                || string.IsNullOrWhiteSpace(command.Input.SpeakerCharacterId)))
                throw new InvalidOperationException(ProductionFailureReasons.CanonicalVoiceInputNotBound);
            if (officialFilm
                && (CreateVideoActionContractGate.IsPresent(command.Input.LineId)
                    || CreateVideoActionContractGate.IsPresent(command.Input.Story))
                && string.Equals(voiceVerdict.BlockReason, ProductionFailureReasons.MissingDependency, StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);
            throw new InvalidOperationException(voiceVerdict.BlockReason ?? ProductionFailureReasons.MissingDependency);
        }

        IVoiceExecutionAdapter adapter = _voice;
        var frozenCredential = command.Credential;
        ShotExecutionInput input;
        if (officialFilm)
        {
            if (string.IsNullOrWhiteSpace(voiceVerdict.LineId)
                || string.IsNullOrWhiteSpace(voiceVerdict.LineText)
                || string.IsNullOrWhiteSpace(voiceVerdict.CharacterId)
                || string.IsNullOrWhiteSpace(voiceVerdict.VoiceId)
                || string.IsNullOrWhiteSpace(voiceVerdict.ProviderId)
                || string.IsNullOrWhiteSpace(voiceVerdict.ModelId)
                || ProductionVoiceFreeze.IsTestIdentity(voiceVerdict.LineId)
                || ProductionVoiceFreeze.IsTestIdentity(voiceVerdict.CharacterId)
                || ProductionVoiceFreeze.IsTestIdentity(voiceVerdict.VoiceId))
                throw new InvalidOperationException(ProductionFailureReasons.CanonicalVoiceInputNotBound);
            adapter = _officialVoice
                ?? throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
            if (!string.Equals(adapter.ProviderId, voiceVerdict.ProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(adapter.ModelId, voiceVerdict.ModelId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(adapter.ProviderId, ProductionVoiceCatalog.ElevenLabsProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(adapter.ModelId, ProductionVoiceCatalog.ElevenV3, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
            if (CreateVideoActionContractGate.IsPresent(command.Input.VoiceId)
                && !ProductionVoiceFreeze.IsTestIdentity(command.Input.VoiceId)
                && !string.Equals(command.Input.VoiceId!.Trim(), voiceVerdict.VoiceId, StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
            if (CreateVideoActionContractGate.IsPresent(command.Input.LineId)
                && !ProductionVoiceFreeze.IsTestIdentity(command.Input.LineId)
                && !string.Equals(command.Input.LineId!.Trim(), voiceVerdict.LineId, StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);
            if (adapter.IsPaid && adapter.RequiresNetwork && !command.AuthorizePaidNetwork)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            var token = ProductionVoiceFreeze.Token(
                voiceVerdict.LineId, voiceVerdict.CharacterId, voiceVerdict.VoiceId, voiceVerdict.LineText);
            input = WithVoiceToken(command.Input with
            {
                LineId = voiceVerdict.LineId,
                SpeakerCharacterId = voiceVerdict.CharacterId,
                VoiceId = voiceVerdict.VoiceId,
                Story = voiceVerdict.LineText,
                VoiceRequirements = token,
                MotionRequirements = token,
                DurationSec = ProductionEp01Sh02VoiceIngressCanon.MatchesScope(command.Scope)
                    ? ProductionEp01Sh02VoiceIngressCanon.DurationSec
                    : (command.Input.DurationSec > 0 ? command.Input.DurationSec : 1),
                PerformanceSnapshotId = voiceVerdict.PerformanceSnapshotId ?? command.Input.PerformanceSnapshotId,
                PerformanceHash = voiceVerdict.PerformanceHash ?? command.Input.PerformanceHash,
                PerformancePayloadJson = voiceVerdict.PerformancePayloadJson ?? command.Input.PerformancePayloadJson,
            });
            if (ProductionEp01Sh02VoiceIngressCanon.MatchesScope(command.Scope))
            {
                var take = await _os.GetAuthoritativeVideoTakeAsync(command.Scope, ct);
                if (take is null
                    || take.ArtifactId != ProductionEp01Sh02VoiceIngressCanon.VideoParentArtifactId
                    || take.ArtifactRole != ProductionArtifactRoles.VideoTake
                    || take.AcceptanceStatus != OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus
                    || !string.Equals(take.ByteSha256, ProductionEp01Sh02VoiceIngressCanon.VideoParentSha, StringComparison.OrdinalIgnoreCase)
                    || take.ByteLength != ProductionEp01Sh02VoiceIngressCanon.VideoParentBytes)
                    throw new InvalidOperationException(ProductionFailureReasons.OutputArtifactNotFound);
                input = input with
                {
                    ParentArtifactIds = [take.ArtifactId],
                    MotionArtifactId = take.ArtifactId,
                };
            }
        }
        else if (tenantPaidVoice)
        {
            adapter = _officialVoice
                ?? throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
            if (!string.Equals(adapter.ProviderId, ProductionVoiceCatalog.ElevenLabsProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(adapter.ModelId, ProductionVoiceCatalog.ElevenV3, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
            if (_tenantAi is null || !await _tenantAi.GovernsAsync(command.Scope, ct))
                throw new InvalidOperationException(ProductionTenantAiReasons.ConfigurationRequired);
            var resolved = await _tenantAi.ResolveAsync(command.Scope, ProductionTenantAiCapabilities.Voice, ct);
            if (!string.Equals(resolved.ProviderId, adapter.ProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(resolved.ModelId, adapter.ModelId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
            if (resolved.Credential is null)
                throw new InvalidOperationException(ProductionTenantAiReasons.CredentialRequired);
            frozenCredential = TenantCredential(command.Credential, resolved);
            if (string.IsNullOrWhiteSpace(command.Input.LineId)
                || string.IsNullOrWhiteSpace(command.Input.VoiceId)
                || string.IsNullOrWhiteSpace(command.Input.Story))
                throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
            var voiceToken = ProductionVoiceFreeze.Token(
                command.Input.LineId, command.Input.SpeakerCharacterId, command.Input.VoiceId, command.Input.Story);
            input = WithVoiceToken(command.Input with
            {
                VoiceRequirements = voiceToken,
                MotionRequirements = voiceToken,
                DurationSec = command.Input.DurationSec > 0 ? command.Input.DurationSec : 1,
                PerformanceSnapshotId = voiceVerdict.PerformanceSnapshotId ?? command.Input.PerformanceSnapshotId,
                PerformanceHash = voiceVerdict.PerformanceHash ?? command.Input.PerformanceHash,
                PerformancePayloadJson = voiceVerdict.PerformancePayloadJson ?? command.Input.PerformancePayloadJson,
            });
        }
        else
        {
            input = WithVoiceToken(command.Input with
            {
                VoiceRequirements = ProductionVoiceFreeze.Token(
                    command.Input.LineId, command.Input.SpeakerCharacterId, command.Input.VoiceId, command.Input.Story),
                DurationSec = command.Input.DurationSec > 0 ? command.Input.DurationSec : 1,
                PerformanceSnapshotId = voiceVerdict.PerformanceSnapshotId ?? command.Input.PerformanceSnapshotId,
                PerformanceHash = voiceVerdict.PerformanceHash ?? command.Input.PerformanceHash,
                PerformancePayloadJson = voiceVerdict.PerformancePayloadJson ?? command.Input.PerformancePayloadJson,
            });
        }

        var spokenDraft = ShortProductionQualityPipelineV1.PrepareVoiceDraft(command.Scope.ShotCode, input.Story);
        if (!spokenDraft.Permitted)
            throw new InvalidOperationException(spokenDraft.Blocker ?? ShortProductionQualityPipelineV1.SpokenLineIncomplete);

        var idem = Idem(command, ProductionActions.CreateVoice, ShotExecutionCompiler.Hash(input.VoiceRequirements));
        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Voice, ct);
        if (replay is not null) return replay;
        if (officialFilm)
        {
            var existing = await _store.GetRequestByIdempotencyAsync(idem, ct);
            if (existing is not null)
            {
                var recovered = await TryRecoverOfficialVoiceAsync(existing, command.Scope, ct);
                if (recovered is not null) return recovered;
                var prior = (await _store.ListTasksByRequestAsync(existing.RequestId, ct)).LastOrDefault();
                throw new InvalidOperationException(
                    prior?.FailureCode ?? ProductionFailureReasons.InFlightReplay);
            }
            if (adapter.IsPaid)
                await RequireOfficialIssuedPermitAsync(
                    command.Scope, ProductionActions.CreateVoice, adapter.ProviderId, adapter.ModelId, ct);
        }
        else if (tenantPaidVoice && adapter.IsPaid)
            await RequireTenantIssuedPermitAsync(
                command.Scope, ProductionActions.CreateVoice, adapter.ProviderId, adapter.ModelId, ct);

        var (request, task, snap, attempt) = await OpenFrozenAsync(
            command.Scope, ProductionActions.CreateVoice, input, command.CreatedBy, idem,
            adapter.ProviderId, adapter.ModelId, snapHash: null, ct, frozenCredential);
        if (ProductionEp01Sh02VoiceIngressCanon.MatchesScope(command.Scope)
            && input.MotionArtifactId is { } videoParent)
            AssertFrozenParents(snap, videoParent);
        if ((officialFilm || tenantPaidVoice) && adapter.IsPaid)
            await ConsumeOfficialPermitBeforeStartAsync(
                command.Scope, ProductionActions.CreateVoice, adapter.ProviderId, adapter.ModelId, adapter.IsPaid, ct,
                snap.SnapshotHash, task.TaskId);
        ProductionMediaResult executed;
        try
        {
            if (adapter is ElevenLabsVoiceExecutionAdapter eleven)
            {
                executed = await eleven.ExecuteFrozenAsync(
                    snap, new VoiceStartContext(attempt.AttemptId, command.Scope, command.Scope.ShotCode), ct);
            }
            else
                executed = await adapter.ExecuteFrozenAsync(snap, ct);
        }
        catch (Exception ex) when (tenantPaidVoice && adapter.IsPaid)
        {
            await _os.FailAttemptAsync(attempt.AttemptId, ex.Message, ct);
            throw;
        }
        return await MaterializeMediaAsync(request, task, attempt, executed, keyframe: false, voice: true, bindVisible: false, ct);
    }

    public async Task<CreateVideoSliceResult> CreateLipsyncAsync(ProductionActionApiCommand command, CancellationToken ct = default)
    {
        var strategyRefusal = NovixaShortProductionStrategyV1.LipsyncRefusal(command.Input);
        if (strategyRefusal is not null)
            throw new InvalidOperationException(strategyRefusal);
        var pictureRefusal = await PictureRefusalAsync(command.Scope, ct);
        if (pictureRefusal is not null)
            throw new InvalidOperationException(pictureRefusal);
        ProductionIdentityRules.ForbidUrlIdentity(
            command.IdempotencyKey, command.Input.VoiceRequirements, command.Input.LineId, command.Input.Story);
        var officialFilm = ProductionScopeAnchorRules.IsOfficialFilm(command.Scope.BuildId);
        var tenantEp01 = !officialFilm
            && ProductionScopeAnchorRules.IsTenantOwnedEp01Build(command.Scope.BuildId, command.Scope.EpisodeCode);
        var officialSh02 = officialFilm && ProductionEp01Sh02LipsyncCanon.MatchesScope(command.Scope);
        AuthoritativeVideoTake? officialTake = null;
        if (officialSh02)
        {
            officialTake = await _os.GetAuthoritativeVideoTakeAsync(command.Scope, ct);
            if (officialTake is null)
                throw new InvalidOperationException(ProductionFailureReasons.MissingVideoArtifact);
            if (!ProductionEp01Sh02LipsyncCanon.MatchesVideo(officialTake))
                throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        }
        var currentVideo = (officialFilm && !officialSh02) || tenantEp01
            ? await CurrentTypedArtifactAsync(command.Scope, ProductionArtifactRoles.VideoTake, ct)
            : null;
        var currentVoice = officialFilm || tenantEp01
            ? await CurrentTypedArtifactAsync(command.Scope, ProductionArtifactRoles.Voice, ct)
            : null;
        if (officialSh02 && currentVoice is null)
            currentVoice = await AcceptedTypedArtifactAsync(command.Scope, ProductionArtifactRoles.Voice, ct);
        var motionId = officialSh02
            ? officialTake!.ArtifactId
            : command.Input.MotionArtifactId ?? currentVideo?.ArtifactId;
        var voiceId = officialSh02
            ? currentVoice?.ArtifactId
            : command.Input.VoiceArtifactId ?? currentVoice?.ArtifactId;
        if (motionId is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingVideoArtifact);
        if (voiceId is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingVoiceArtifact);
        if (officialSh02 && currentVoice is not null && !ProductionEp01Sh02LipsyncCanon.MatchesVoice(currentVoice))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        if (officialSh02 && command.Input.MotionArtifactId is { } claimedVideo && claimedVideo != motionId)
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        if (officialSh02 && command.Input.VoiceArtifactId is { } claimedVoice && claimedVoice != voiceId)
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        var motion = await RequireParentAsync(command.Scope, motionId.Value, ProductionArtifactRoles.VideoTake, ct);
        var voice = await RequireParentAsync(command.Scope, voiceId.Value, ProductionArtifactRoles.Voice, ct);
        if (IsTenantSh02OfficialVideoCopy(command.Scope, motion))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        RequirePhysicalParent(motion);
        RequirePhysicalParent(voice);
        if (tenantEp01 && command.AuthorizePaidNetwork)
        {
            if (IsHistoricalProductionParent(motion.ArtifactId) || IsHistoricalProductionParent(voice.ArtifactId))
                throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
            if (currentVideo is null || currentVideo.ArtifactId != motion.ArtifactId
                || currentVoice is null || currentVoice.ArtifactId != voice.ArtifactId)
                throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        }
        if (officialFilm && !officialSh02 && currentVideo is not null && currentVideo.ArtifactId != motion.ArtifactId)
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        if (officialFilm && currentVoice is not null && currentVoice.ArtifactId != voice.ArtifactId)
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        if (officialFilm && ProductionEp01Sh01LipsyncCanon.MatchesScope(command.Scope)
            && (motion.ArtifactId != ProductionEp01Sh01LipsyncCanon.VideoArtifactId
                || !string.Equals(motion.ByteSha256, ProductionEp01Sh01LipsyncCanon.VideoSha256, StringComparison.OrdinalIgnoreCase)
                || voice.ArtifactId != ProductionEp01Sh01LipsyncCanon.VoiceArtifactId
                || !string.Equals(voice.ByteSha256, ProductionEp01Sh01LipsyncCanon.VoiceSha256, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        if (officialSh02
            && (motion.ArtifactId != ProductionEp01Sh02LipsyncCanon.VideoArtifactId
                || !string.Equals(motion.ByteSha256, ProductionEp01Sh02LipsyncCanon.VideoSha256, StringComparison.OrdinalIgnoreCase)
                || motion.SizeBytes != ProductionEp01Sh02LipsyncCanon.VideoBytes
                || voice.ArtifactId != ProductionEp01Sh02LipsyncCanon.VoiceArtifactId
                || !string.Equals(voice.ByteSha256, ProductionEp01Sh02LipsyncCanon.VoiceSha256, StringComparison.OrdinalIgnoreCase)
                || voice.SizeBytes != ProductionEp01Sh02LipsyncCanon.VoiceBytes))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        if (officialSh02)
        {
            var acceptedVoice = await _store.GetActiveBindingAsync(
                command.Scope.BuildId, command.Scope.ShotCode,
                ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice), ct);
            if (acceptedVoice is null || acceptedVoice.ArtifactId != voice.ArtifactId)
                throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        }
        var lineId = officialFilm && ProductionEp01Sh01LipsyncCanon.MatchesScope(command.Scope)
            ? ProductionEp01Sh01LipsyncCanon.LineId
            : officialSh02
                ? ProductionEp01Sh02LipsyncCanon.LineId
                : command.Input.LineId;
        if (!string.IsNullOrWhiteSpace(lineId))
        {
            var voiceSnap = await _store.GetSnapshotByTaskAsync(voice.TaskId, ct);
            if (!ProductionVoiceLineIdentity.Matches(voiceSnap, lineId))
                throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);
        }
        var speaker = officialFilm && ProductionEp01Sh01LipsyncCanon.MatchesScope(command.Scope)
            ? ProductionEp01Sh01LipsyncCanon.SpeakerCharacterId
            : officialSh02
                ? ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId
                : command.Input.SpeakerCharacterId;
        if (NovixaShortProductionStrategyV1.Engaged(command.Input))
        {
            var mouth = ProductionLipsyncSpeakerTarget.TryResolveFrozen(lineId, speaker, out _)
                || officialSh02;
            var assetBlock = NovixaShortProductionStrategyV1.LipsyncBlock(
                command.Input, motion is not null, voice is not null, mouth);
            if (assetBlock is not null)
                throw new InvalidOperationException(assetBlock);
        }
        ILipSyncExecutionAdapter adapter = _lipsync;
        var frozenCredential = command.Credential;
        string voiceRequirements;
        string motionRequirements;
        ProductionLipsyncSpeakerPin pin = default;
        if (officialFilm)
        {
            if (officialSh02)
            {
                lineId = ProductionEp01Sh02LipsyncCanon.LineId;
                speaker = ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId;
                pin = new ProductionLipsyncSpeakerPin(
                    ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId,
                    ProductionEp01Sh02LipsyncCanon.PinX01,
                    ProductionEp01Sh02LipsyncCanon.PinY01,
                    ProductionEp01Sh02LipsyncCanon.PinWidth,
                    ProductionEp01Sh02LipsyncCanon.PinHeight,
                    ProductionEp01Sh02LipsyncCanon.PinFrame);
            }
            else if (!ProductionLipsyncSpeakerTarget.TryResolveFrozen(lineId, speaker, out pin))
                throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);
            adapter = _officialLipsync
                ?? throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
            if (!string.Equals(adapter.ProviderId, ProductionLipsyncCatalog.FalProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(adapter.ModelId, ProductionLipsyncCatalog.SyncLipsyncV3, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
            if (adapter.IsPaid && adapter.RequiresNetwork && !command.AuthorizePaidNetwork)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            var token = officialSh02
                ? ProductionLipsyncFreeze.Token(
                    lineId ?? "", speaker ?? "", motion.ArtifactId, voice.ArtifactId, pin,
                    ProductionEp01Sh02LipsyncCanon.FaceBoxXyxy)
                : ProductionLipsyncFreeze.Token(lineId ?? "", speaker ?? "", motion.ArtifactId, voice.ArtifactId, pin);
            voiceRequirements = token;
            motionRequirements = token;
        }
        else if (tenantEp01 && command.AuthorizePaidNetwork)
        {
            adapter = _officialLipsync
                ?? throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
            if (!string.Equals(adapter.ProviderId, ProductionLipsyncCatalog.FalProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(adapter.ModelId, ProductionLipsyncCatalog.SyncLipsyncV3, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
            if (_tenantAi is not null && await _tenantAi.GovernsAsync(command.Scope, ct))
            {
                var resolved = await _tenantAi.ResolveAsync(command.Scope, ProductionTenantAiCapabilities.Lipsync, ct);
                if (!string.Equals(resolved.ProviderId, adapter.ProviderId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(resolved.ModelId, adapter.ModelId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
                if (resolved.Credential is null)
                    throw new InvalidOperationException(ProductionTenantAiReasons.CredentialRequired);
                frozenCredential = TenantCredential(command.Credential, resolved);
            }
            if (string.IsNullOrWhiteSpace(lineId))
            {
                var voiceSnap = await _store.GetSnapshotByTaskAsync(voice.TaskId, ct);
                lineId = ProductionVoiceLineIdentity.LineIdFromSnapshot(voiceSnap);
            }
            if (string.IsNullOrWhiteSpace(speaker))
                speaker = SpeakerForRecoveredLine(lineId);
            if (!ProductionLipsyncSpeakerTarget.TryResolveFrozen(lineId, speaker, out pin))
                throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);
            int[]? tenantMouth = null;
            if (string.Equals(command.Scope.ShotCode, "EP01-SC01-SH01", StringComparison.Ordinal))
            {
                if (!ProductionLipsyncSpeakerTarget.TryResolveTenantSh01Linh(out pin))
                    throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);
                tenantMouth = ProductionLipsyncSpeakerTarget.TenantSh01HeadBox;
            }
            if (string.Equals(command.Scope.ShotCode, "EP01-SC01-SH02", StringComparison.Ordinal))
            {
                if (!ProductionLipsyncSpeakerTarget.TryResolveTenantSh02Minh(out pin))
                    throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);
                tenantMouth = ProductionLipsyncSpeakerTarget.TenantSh02HeadBox;
            }
            var token = ProductionLipsyncFreeze.Token(
                lineId ?? "", speaker ?? "", motion.ArtifactId, voice.ArtifactId, pin, tenantMouth);
            voiceRequirements = token;
            motionRequirements = token;
        }
        else
        {
            if (adapter.IsPaid || adapter.RequiresNetwork)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            voiceRequirements = string.Join('|',
                motion.ArtifactId.ToString("N"), voice.ArtifactId.ToString("N"), voice.ByteSha256,
                command.Input.LineId, command.Input.SpeakerCharacterId);
            motionRequirements = motion.ByteSha256;
        }
        var lipsyncClaim = CreateVideoActionContractGate.ClaimFrom(command.Input) with
        {
            MotionArtifactId = motion.ArtifactId,
            VoiceArtifactId = voice.ArtifactId,
            LineId = lineId,
            CharacterId = speaker,
        };
        var lipsyncVerdict = await _os.EvaluateCreateLipsyncAsync(command.Scope, lipsyncClaim, ct);
        if (!lipsyncVerdict.Available)
        {
            if (lipsyncVerdict.BlockReason == FamixaEp01UpstreamHold.LipsyncBlocked)
                await RecordStageHoldAsync(
                    command.Scope, lipsyncVerdict.VideoArtifactId, ProductionActions.CreateLipsync, lipsyncVerdict.BlockReason, ct);
            throw new InvalidOperationException(lipsyncVerdict.BlockReason ?? ProductionFailureReasons.MissingDependency);
        }
        DirectorSpeechInterval? approvedSpeech = null;
        // Tenant EP01 Fal input is the approved speech interval. Official film keeps its frozen full take.
        if (adapter is FalLipSyncExecutionAdapter
            && ProductionScopeAnchorRules.IsTenantOwnedEp01Build(command.Scope.BuildId, command.Scope.EpisodeCode))
            approvedSpeech = await RequireLipsyncSpeechAsync(command.Scope, motion.DurationSec, ct);
        var input = command.Input with
        {
            ParentArtifactIds = [motion.ArtifactId, voice.ArtifactId],
            MotionArtifactId = motion.ArtifactId,
            VoiceArtifactId = voice.ArtifactId,
            LineId = lineId ?? command.Input.LineId,
            SpeakerCharacterId = speaker ?? command.Input.SpeakerCharacterId,
            DurationSec = motion.DurationSec > 0 ? motion.DurationSec : 5,
            VoiceRequirements = voiceRequirements,
            MotionRequirements = motionRequirements,
            PerformanceSnapshotId = lipsyncVerdict.PerformanceSnapshotId ?? command.Input.PerformanceSnapshotId,
            PerformanceHash = lipsyncVerdict.PerformanceHash ?? command.Input.PerformanceHash,
            PerformancePayloadJson = lipsyncVerdict.PerformancePayloadJson ?? command.Input.PerformancePayloadJson,
        };
        var lipsyncIdemToken = motion.ArtifactId.ToString("N") + voice.ArtifactId.ToString("N")
            + (officialSh02
                ? "+" + ProductionEp01Sh02LipsyncCanon.FaceBoxIdemToken
                : "")
            + (approvedSpeech is { } speechWindow
                ? "+" + ProductionMixContract.Dec(speechWindow.StartSec) + "-" + ProductionMixContract.Dec(speechWindow.EndSec)
                : "");
        var idem = officialSh02
            ? $"{command.Scope.BuildId:N}:{command.Scope.ShotCode}:{ProductionActions.CreateLipsync}:{lipsyncIdemToken}"
            : Idem(command, ProductionActions.CreateLipsync, lipsyncIdemToken);
        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Lipsync, ct);
        if (replay is not null)
        {
            if (officialSh02 && replay.Artifact.ArtifactId == ProductionEp01Sh02LipsyncCanon.HistoricalCk23LipsyncArtifactId)
                throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
            return replay;
        }
        if (officialFilm)
        {
            var existingLipsync = await _store.GetActiveBindingAsync(
                command.Scope.BuildId, command.Scope.ShotCode,
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), ct);
            if (existingLipsync is not null
                && !(officialSh02
                    && existingLipsync.ArtifactId == ProductionEp01Sh02LipsyncCanon.HistoricalCk23LipsyncArtifactId))
                throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
            var existing = await _store.GetRequestByIdempotencyAsync(idem, ct);
            if (existing is not null)
            {
                var recovered = await TryRecoverOfficialLipsyncAsync(existing, command.Scope, ct);
                if (recovered is not null) return recovered;
                var prior = (await _store.ListTasksByRequestAsync(existing.RequestId, ct)).LastOrDefault();
                throw new InvalidOperationException(
                    prior?.FailureCode ?? ProductionFailureReasons.InFlightReplay);
            }
            if (adapter.IsPaid)
                await RequireOfficialIssuedPermitAsync(
                    command.Scope, ProductionActions.CreateLipsync, adapter.ProviderId, adapter.ModelId, ct);
        }
        else if (tenantEp01 && adapter.IsPaid)
            await RequireTenantIssuedPermitAsync(
                command.Scope, ProductionActions.CreateLipsync, adapter.ProviderId, adapter.ModelId, ct);
        byte[]? trimmedSpeechVideo = null;
        if (approvedSpeech is { } speech)
        {
            var sourceBytes = _files.Read(motion.StorageKey);
            if (sourceBytes is not { Length: >= 32 })
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            var sourceSha = KitVideoIntegrityRules.Sha256Hex(sourceBytes);
            trimmedSpeechVideo = await ProductionLipsyncSpeechSource.TrimAsync(
                sourceBytes, speech.StartSec, speech.EndSec, motion.DurationSec, ct);
            var reread = _files.Read(motion.StorageKey);
            if (reread is null || !string.Equals(KitVideoIntegrityRules.Sha256Hex(reread), sourceSha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        }
        var (request, task, snap, attempt) = await OpenFrozenAsync(
            command.Scope, ProductionActions.CreateLipsync, input, command.CreatedBy, idem,
            adapter.ProviderId, adapter.ModelId, null, ct, frozenCredential);
        if ((officialFilm || tenantEp01) && adapter.IsPaid)
            await ConsumeOfficialPermitBeforeStartAsync(
                command.Scope, ProductionActions.CreateLipsync, adapter.ProviderId, adapter.ModelId, adapter.IsPaid, ct,
                snap.SnapshotHash, task.TaskId);
        AssertFrozenParents(snap, motion.ArtifactId, voice.ArtifactId);
        ProductionMediaResult executed;
        try
        {
            if (adapter is FalLipSyncExecutionAdapter fal)
            {
                var videoBytes = trimmedSpeechVideo ?? _files.Read(motion.StorageKey);
                var voiceBytes = _files.Read(voice.StorageKey);
                if (videoBytes is not { Length: >= 32 } || voiceBytes is not { Length: >= 32 })
                    throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
                var transportSnap = approvedSpeech is { } placed
                    ? snap with { DurationSec = placed.EndSec - placed.StartSec }
                    : snap;
                executed = await fal.ExecuteFrozenAsync(
                    transportSnap,
                    new LipsyncStartContext(attempt.AttemptId, command.Scope, command.Scope.ShotCode, videoBytes, voiceBytes),
                    ct);
            }
            else
                executed = await adapter.ExecuteFrozenAsync(snap, ct);
        }
        catch (Exception ex) when (tenantEp01 && adapter.IsPaid)
        {
            await _os.FailAttemptAsync(attempt.AttemptId, ex.Message, ct);
            throw;
        }
        return await MaterializeMediaAsync(
            request, task, attempt, executed, keyframe: false, voice: false,
            bindVisible: !officialSh02, ct, bindCurrent: !officialSh02);
    }

    public async Task<CreateVideoSliceResult> RecoverOfficialExistingLipsyncAsync(
        Guid attemptId, string providerJobId, CancellationToken ct = default)
    {
        var job = (providerJobId ?? "").Trim();
        if (attemptId == Guid.Empty || string.IsNullOrWhiteSpace(job))
            throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        var attempt = await _store.GetAttemptAsync(attemptId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        var task = await _store.GetTaskAsync(attempt.TaskId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        if (task.Action != ProductionActions.CreateLipsync
            || !(ProductionScopeAnchorRules.IsOfficialFilm(task.Scope.BuildId)
                || ProductionScopeAnchorRules.IsTenantOwnedEp01Build(task.Scope.BuildId, task.Scope.EpisodeCode)))
            throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        var request = await _store.GetRequestAsync(task.RequestId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);

        var existingArt = (await _store.ListArtifactsByTaskAsync(task.TaskId, ct))
            .LastOrDefault(a => a.Role == ProductionArtifactRoles.Lipsync
                && a.VerificationStatus == ProductionArtifactStatuses.Verified
                && a.AttemptId == attempt.AttemptId);
        if (existingArt is not null)
        {
            await TryBindNamedProtectedAsync(existingArt.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), ct);
            await TryBindProtectedAsync(() => _os.BindVisibleProtectedAsync(existingArt.ArtifactId, ct), task, ProductionBindingRoles.Visible, ct);
            return await SliceOfAsync(request, task, attempt, existingArt, ct);
        }

        if (string.IsNullOrWhiteSpace(attempt.ProviderJobId))
            attempt = await _os.SubmitProviderJobAsync(attempt.AttemptId, job, ct);
        else if (!SameProviderJob(attempt.ProviderJobId, job))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        var recovered = await TryRecoverOfficialLipsyncAsync(request, task.Scope, ct);
        if (recovered is not null) return recovered;
        await _os.FailAttemptAsync(attempt.AttemptId, ProductionFailureReasons.RecoveryFailed, ct);
        throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
    }

    public async Task<ProductionRequest> CloseEmptyOfficialLipsyncRequestAsync(
        string idempotencyKey, CancellationToken ct = default)
    {
        var key = (idempotencyKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        var request = await _store.GetRequestByIdempotencyAsync(key, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        if (request.UserAction != ProductionActions.CreateLipsync)
            throw new InvalidOperationException(ProductionFailureReasons.ActionRemapForbidden);
        var task = (await _store.ListTasksByRequestAsync(request.RequestId, ct)).LastOrDefault();
        if (task is null)
        {
            if (request.Status != ProductionRequestStatuses.Failed
                && ProductionStateMachines.CanTransitionRequest(request.Status, ProductionRequestStatuses.Failed))
            {
                request = request with { Status = ProductionRequestStatuses.Failed };
                await _store.UpdateRequestAsync(request, ct);
            }
            return request;
        }
        var attempts = await _store.ListAttemptsAsync(task.TaskId, ct);
        if (attempts.Count > 0)
            return request;
        if (task.Status != ProductionTaskStatuses.Superseded
            && ProductionStateMachines.CanTransitionTask(task.Status, ProductionTaskStatuses.Superseded))
        {
            task = task with
            {
                Status = ProductionTaskStatuses.Superseded,
                CompletedAt = DateTimeOffset.UtcNow,
                FailureCode = ProductionFailureReasons.RecoveryFailed,
                FailureReason = "empty execution; no attempt; no provider call",
            };
            await _store.UpdateTaskAsync(task, ct);
        }
        if (request.Status != ProductionRequestStatuses.Failed
            && ProductionStateMachines.CanTransitionRequest(request.Status, ProductionRequestStatuses.Failed))
        {
            request = request with { Status = ProductionRequestStatuses.Failed };
            await _store.UpdateRequestAsync(request, ct);
        }
        return await _store.GetRequestAsync(request.RequestId, ct) ?? request;
    }

    async Task<DirectorSpeechInterval> RequireLipsyncSpeechAsync(
        ProductionScope scope, decimal takeDurationSec, CancellationToken ct)
    {
        if (_performanceSnapshots is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingTimeline);
        var snap = await _performanceSnapshots.GetByScopeAsync(scope.BuildId, scope.ShotCode, ct);
        return ProductionLipsyncSpeechSource.Require(snap?.Source, takeDurationSec);
    }

    public async Task<CreateVideoSliceResult> MixVideoAsync(ProductionActionApiCommand command, CancellationToken ct = default)
    {
        ProductionIdentityRules.ForbidUrlIdentity(
            command.IdempotencyKey, command.Input.VoiceRequirements, command.Input.LineId, command.Input.Story);
        if (NovixaShortProductionStrategyV1.IsConfirmedStaticVoiceOver(command.Input))
            return await MixVoiceOverStillAsync(command, ct);
        var officialFilm = ProductionScopeAnchorRules.IsOfficialFilm(command.Scope.BuildId);
        var tenantEp01 = ProductionScopeAnchorRules.IsTenantOwnedEp01Build(
            command.Scope.BuildId, command.Scope.EpisodeCode);
        if (tenantEp01
            && ShortProductionQualityPipelineV1.Ep01Contract(command.Scope.ShotCode) is { DirectDialogue: false, LineComplete: true })
            return await MixSilentTakeAsync(command, ct);
        var currentLipsync = officialFilm || tenantEp01
            ? await CurrentTypedArtifactAsync(command.Scope, ProductionArtifactRoles.Lipsync, ct)
            : null;
        var lipsyncId = command.Input.LipsyncArtifactId ?? currentLipsync?.ArtifactId;
        if (lipsyncId is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingLipsyncArtifact);
        var lipsync = await RequireParentAsync(command.Scope, lipsyncId.Value, ProductionArtifactRoles.Lipsync, ct);
        RequirePhysicalParent(lipsync);
        if ((officialFilm || tenantEp01) && currentLipsync is not null && currentLipsync.ArtifactId != lipsync.ArtifactId)
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        if (officialFilm && ProductionEp01Sh01MixCanon.MatchesScope(command.Scope)
            && (lipsync.ArtifactId != ProductionEp01Sh01MixCanon.LipsyncArtifactId
                || !string.Equals(lipsync.ByteSha256, ProductionEp01Sh01MixCanon.LipsyncSha256, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);

        if (ProductionMixContract.AppliesTo(command.Scope))
            return await MixComposeSegmentsAsync(command, lipsync, ct);

        IMixExecutionAdapter adapter = _mix;
        string freeze;
        if (officialFilm)
        {
            adapter = _officialMix
                ?? throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
            if (!string.Equals(adapter.ProviderId, ProductionMixCatalog.ServerProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(adapter.ModelId, ProductionMixCatalog.FfmpegModelId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
            if (adapter.IsPaid || adapter.RequiresNetwork)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            freeze = ProductionMixFreeze.Token(lipsync.ArtifactId, lipsync.ByteSha256);
        }
        else
        {
            if (adapter.IsPaid || adapter.RequiresNetwork)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            freeze = "";
        }

        var mixClaim = CreateVideoActionContractGate.ClaimFrom(command.Input) with { LipsyncArtifactId = lipsync.ArtifactId };
        var mixVerdict = await _os.EvaluateCreateMixAsync(command.Scope, mixClaim, ct);
        if (!mixVerdict.Available)
            throw new InvalidOperationException(mixVerdict.BlockReason ?? ProductionFailureReasons.MissingLipsyncArtifact);
        var input = command.Input with
        {
            ParentArtifactIds = [lipsync.ArtifactId],
            LipsyncArtifactId = lipsync.ArtifactId,
            DurationSec = lipsync.DurationSec > 0 ? lipsync.DurationSec : 5,
            VoiceRequirements = officialFilm ? freeze : string.Join('|', lipsync.ArtifactId.ToString("N"), lipsync.ByteSha256),
            MotionRequirements = officialFilm ? freeze : lipsync.ByteSha256,
        };
        var idem = Idem(command, ProductionActions.MixVideo, lipsync.ArtifactId.ToString("N"));
        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Mix, ct);
        if (replay is not null) return replay;
        if (officialFilm)
        {
            var existingMix = await _store.GetActiveBindingAsync(
                command.Scope.BuildId, command.Scope.ShotCode,
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix), ct);
            if (existingMix is not null)
                throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        }
        var (request, task, snap, attempt) = await OpenFrozenAsync(
            command.Scope, ProductionActions.MixVideo, input, command.CreatedBy, idem,
            adapter.ProviderId, adapter.ModelId, null, ct, command.Credential);
        AssertFrozenParents(snap, lipsync.ArtifactId);
        ProductionMediaResult executed;
        if (adapter is FfmpegMixExecutionAdapter ffmpeg)
        {
            var lipsyncBytes = _files.Read(lipsync.StorageKey);
            if (lipsyncBytes is not { Length: >= 32 }
                || KitVideoIntegrityRules.Sha256Hex(lipsyncBytes) != lipsync.ByteSha256)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            executed = await ExecuteLocalProcessAsync(
                attempt.AttemptId,
                token => ffmpeg.ExecuteFrozenAsync(snap, new MixStartContext(attempt.AttemptId, lipsyncBytes), token),
                ct);
        }
        else
            executed = await adapter.ExecuteFrozenAsync(snap, ct);
        return await MaterializeMediaAsync(request, task, attempt, executed, keyframe: false, voice: false, bindVisible: true, ct);
    }

    async Task<CreateVideoSliceResult> MixSilentTakeAsync(
        ProductionActionApiCommand command, CancellationToken ct)
    {
        var video = await CurrentTypedArtifactAsync(command.Scope, ProductionArtifactRoles.VideoTake, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.MissingVideoArtifact);
        video = await RequireParentAsync(command.Scope, video.ArtifactId, ProductionArtifactRoles.VideoTake, ct);
        RequirePhysicalParent(video);
        if (FamixaEp01UpstreamHold.BlocksLipsync(video.ArtifactId))
            throw new InvalidOperationException(FamixaEp01UpstreamHold.LipsyncBlocked);
        var adapter = _officialMix
            ?? throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        if (adapter.IsPaid || adapter.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        var contract = ProductionMixContract.BuildSilent(
            video,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake),
            ShotExecutionCompiler.Hash(command.Input.Timing ?? ""));
        var freeze = ProductionMixFreeze.TokenV1(contract);
        if (!ProductionMixFreeze.TryParseV1(freeze, out var frozen) || frozen.ContractHash != contract.ContractHash)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        var input = command.Input with
        {
            ParentArtifactIds = [video.ArtifactId],
            MotionArtifactId = video.ArtifactId,
            DurationSec = contract.OutputDurationSec,
            VoiceRequirements = freeze,
            MotionRequirements = freeze,
        };
        var idem = Idem(command, ProductionActions.MixVideo, contract.ContractHash);
        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Mix, ct);
        if (replay is not null) return replay;
        var (request, task, snap, attempt) = await OpenFrozenAsync(
            command.Scope, ProductionActions.MixVideo, input, command.CreatedBy, idem,
            adapter.ProviderId, adapter.ModelId, null, ct, command.Credential);
        AssertFrozenParents(snap, video.ArtifactId);
        if (adapter is not FfmpegMixExecutionAdapter ffmpeg)
            throw new InvalidOperationException(ProductionFailureReasons.UnsupportedCompositionMode);
        var videoBytes = _files.Read(video.StorageKey);
        if (videoBytes is not { Length: >= 32 }
            || KitVideoIntegrityRules.Sha256Hex(videoBytes) != video.ByteSha256)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var executed = await ExecuteLocalProcessAsync(
            attempt.AttemptId,
            token => ffmpeg.ExecuteFrozenAsync(
                snap, new MixStartContext(attempt.AttemptId, [], videoBytes), token),
            ct);
        if (ProductionMixContract.Dec(executed.DurationSec) != ProductionMixContract.Dec(contract.OutputDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
        return await MaterializeMediaAsync(
            request, task, attempt, executed, keyframe: false, voice: false, bindVisible: true, ct);
    }

    async Task<CreateVideoSliceResult> MixVoiceOverStillAsync(
        ProductionActionApiCommand command, CancellationToken ct)
    {
        var keyframe = await AcceptedTypedArtifactAsync(command.Scope, ProductionArtifactRoles.Keyframe, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);
        var voice = await AcceptedTypedArtifactAsync(command.Scope, ProductionArtifactRoles.Voice, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.MissingVoiceArtifact);
        keyframe = await RequireParentAsync(command.Scope, keyframe.ArtifactId, ProductionArtifactRoles.Keyframe, ct);
        voice = await RequireParentAsync(command.Scope, voice.ArtifactId, ProductionArtifactRoles.Voice, ct);
        RequirePhysicalParent(keyframe);
        RequirePhysicalParent(voice);
        var adapter = _officialMix;
        if (adapter is null || adapter.IsPaid || adapter.RequiresNetwork || adapter is not FfmpegMixExecutionAdapter ffmpeg)
            throw new InvalidOperationException(ProductionFailureReasons.UnsupportedCompositionMode);
        var contract = ProductionMixContract.BuildVoiceOverStill(
            keyframe,
            voice,
            ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Keyframe),
            ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice));
        var freeze = ProductionMixFreeze.TokenV1(contract);
        if (!ProductionMixFreeze.TryParseV1(freeze, out var frozen) || frozen.ContractHash != contract.ContractHash)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        var input = command.Input with
        {
            ParentArtifactIds = [keyframe.ArtifactId, voice.ArtifactId],
            KeyframeArtifactId = keyframe.ArtifactId,
            VoiceArtifactId = voice.ArtifactId,
            DurationSec = contract.OutputDurationSec,
            VoiceRequirements = freeze,
            MotionRequirements = freeze,
        };
        var idem = Idem(command, ProductionActions.MixVideo, contract.ContractHash);
        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Mix, ct);
        if (replay is not null) return replay;
        var (request, task, snap, attempt) = await OpenFrozenAsync(
            command.Scope, ProductionActions.MixVideo, input, command.CreatedBy, idem,
            adapter.ProviderId, adapter.ModelId, null, ct, command.Credential);
        AssertFrozenParents(snap, keyframe.ArtifactId);
        AssertFrozenParents(snap, voice.ArtifactId);
        var keyframeBytes = _files.Read(keyframe.StorageKey);
        var voiceBytes = _files.Read(voice.StorageKey);
        if (keyframeBytes is not { Length: >= 32 }
            || voiceBytes is not { Length: >= 32 }
            || KitVideoIntegrityRules.Sha256Hex(keyframeBytes) != keyframe.ByteSha256
            || KitVideoIntegrityRules.Sha256Hex(voiceBytes) != voice.ByteSha256)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var executed = await ExecuteLocalProcessAsync(
            attempt.AttemptId,
            token => ffmpeg.ExecuteFrozenAsync(
                snap, new MixStartContext(attempt.AttemptId, [], null, keyframeBytes, voiceBytes), token),
            ct);
        if (ProductionMixContract.Dec(executed.DurationSec) != ProductionMixContract.Dec(contract.OutputDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
        return await MaterializeMediaAsync(
            request, task, attempt, executed, keyframe: false, voice: false, bindVisible: true, ct);
    }

    async Task<CreateVideoSliceResult> MixComposeSegmentsAsync(
        ProductionActionApiCommand command,
        ProductionArtifact lipsync,
        CancellationToken ct)
    {
        if (ProductionEp01Sh02MixCanon.IsHistoricalLipsync(lipsync.ArtifactId))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        var composeClaim = CreateVideoActionContractGate.ClaimFrom(command.Input) with { LipsyncArtifactId = lipsync.ArtifactId };
        var composeVerdict = await _os.EvaluateCreateMixAsync(command.Scope, composeClaim, ct);
        if (!composeVerdict.Available)
            throw new InvalidOperationException(composeVerdict.BlockReason ?? ProductionFailureReasons.MissingLipsyncArtifact);
        var videoId = composeVerdict.VideoArtifactId
            ?? throw new InvalidOperationException(ProductionFailureReasons.MissingVideoArtifact);
        var video = await RequireParentAsync(command.Scope, videoId, ProductionArtifactRoles.VideoTake, ct);
        if (IsTenantSh02OfficialVideoCopy(command.Scope, video))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        RequirePhysicalParent(video);
        if (ProductionEp01Sh02MixCanon.MatchesScope(command.Scope))
        {
            var locked = ProductionEp01Sh02MixCanon.ParentLockError(video, lipsync);
            if (locked is not null)
                throw new InvalidOperationException(locked);
        }

        var tenantEp01 = ProductionScopeAnchorRules.IsTenantOwnedEp01Build(
            command.Scope.BuildId, command.Scope.EpisodeCode);
        if (tenantEp01)
            await RequireTenantMixParentsAsync(command.Scope, video, lipsync, ct);

        var adapter = _officialMix
            ?? throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        if (!string.Equals(adapter.ProviderId, ProductionMixCatalog.ServerProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(adapter.ModelId, ProductionMixCatalog.FfmpegModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (adapter.IsPaid || adapter.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);

        async Task<DirectorSpeechInterval?> DirectorSpeechForMixAsync()
        {
            if (_performanceSnapshots is null) return null;
            var snap = await _performanceSnapshots.GetByScopeAsync(command.Scope.BuildId, command.Scope.ShotCode, ct);
            return ProductionDirectorTiming.ApprovedSpeech(snap?.Source);
        }
        var contract = ProductionMixContract.Build(
            video, lipsync,
            OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            ShotExecutionCompiler.Hash(command.Input.Timing ?? ""),
            await DirectorSpeechForMixAsync());
        var freeze = ProductionMixFreeze.TokenV1(contract);
        if (!ProductionMixFreeze.TryParseV1(freeze, out var frozen))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        var freezeMismatch = ProductionMixContract.MatchesFrozen(frozen, video, lipsync);
        if (freezeMismatch is not null)
            throw new InvalidOperationException(freezeMismatch);

        var input = command.Input with
        {
            ParentArtifactIds = [video.ArtifactId, lipsync.ArtifactId],
            MotionArtifactId = video.ArtifactId,
            LipsyncArtifactId = lipsync.ArtifactId,
            DurationSec = contract.OutputDurationSec,
            VoiceRequirements = freeze,
            MotionRequirements = freeze,
        };
        var idem = Idem(command, ProductionActions.MixVideo, contract.ContractHash);
        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Mix, ct);
        if (replay is not null) return replay;
        var existingMix = await _store.GetActiveBindingAsync(
            command.Scope.BuildId, command.Scope.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix), ct);
        if (existingMix is not null
            && (!tenantEp01 || await ExistingMixContractMatchesAsync(existingMix.ArtifactId, contract.ContractHash, ct)))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);

        if (!tenantEp01)
            await RequireOfficialIssuedPermitAsync(
                command.Scope, ProductionActions.MixVideo, adapter.ProviderId, adapter.ModelId, ct);
        var (request, task, snap, attempt) = await OpenFrozenAsync(
            command.Scope, ProductionActions.MixVideo, input, command.CreatedBy, idem,
            adapter.ProviderId, adapter.ModelId, null, ct, command.Credential);
        if (!ProductionMixFreeze.TryParseV1(snap.ShotContract, out var snapContract)
            || snapContract.ContractHash != contract.ContractHash
            || snap.DurationSec != contract.OutputDurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        AssertFrozenParents(snap, video.ArtifactId, lipsync.ArtifactId);

        if (!tenantEp01)
        {
            var consumed = _officialPermits is not null
                && await _officialPermits.TryConsumeAsync(
                    command.Scope.BuildId, command.Scope.ShotCode,
                    ProductionActions.MixVideo, adapter.ProviderId, adapter.ModelId, ct);
            if (!consumed)
                throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        }

        var videoBytes = _files.Read(video.StorageKey);
        var lipsyncBytes = _files.Read(lipsync.StorageKey);
        if (videoBytes is not { Length: >= 32 } || lipsyncBytes is not { Length: >= 32 }
            || KitVideoIntegrityRules.Sha256Hex(videoBytes) != video.ByteSha256
            || KitVideoIntegrityRules.Sha256Hex(lipsyncBytes) != lipsync.ByteSha256)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (adapter is not FfmpegMixExecutionAdapter ffmpeg)
            throw new InvalidOperationException(ProductionFailureReasons.UnsupportedCompositionMode);
        var executed = await ExecuteLocalProcessAsync(
            attempt.AttemptId,
            token => ffmpeg.ExecuteFrozenAsync(snap, new MixStartContext(attempt.AttemptId, lipsyncBytes, videoBytes), token),
            ct);
        if (ProductionMixContract.Dec(executed.DurationSec) != ProductionMixContract.Dec(contract.OutputDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
        if (KitVideoIntegrityRules.Sha256Hex(executed.Bytes) == video.ByteSha256
            || KitVideoIntegrityRules.Sha256Hex(executed.Bytes) == lipsync.ByteSha256)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return await MaterializeMediaAsync(
            request, task, attempt, executed, keyframe: false, voice: false, bindVisible: true, ct);
    }

    public async Task<CreateVideoSliceResult> AcceptOsAsync(ProductionActionApiCommand command, CancellationToken ct = default)
    {
        ProductionIdentityRules.ForbidUrlIdentity(command.IdempotencyKey);
        var artifactId = command.Input.AcceptArtifactId ?? throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        var artifact = await RequireParentAsync(command.Scope, artifactId, artifactRole: null, ct);
        if (ProductionScopeAnchorRules.IsTenantOwnedEp01Build(command.Scope.BuildId, command.Scope.EpisodeCode))
        {
            var produced = await _store.GetTaskAsync(artifact.TaskId, ct);
            if (produced is not null
                && string.Equals(produced.ProviderId, ProductionMotionCatalog.FixtureProviderId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        }
        var input = command.Input with
        {
            ParentArtifactIds = [artifact.ArtifactId],
            AcceptArtifactId = artifact.ArtifactId,
            VoiceRequirements = artifact.ArtifactId.ToString("N"),
        };
        var idem = Idem(command, ProductionActions.Accept, artifact.ArtifactId.ToString("N"));
        var existingAccept = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existingAccept is not null)
        {
            var accepted = await _store.GetActiveBindingAsync(command.Scope.BuildId, command.Scope.ShotCode, ProductionBindingRoles.Accepted, ct);
            var priorTasks = await _store.ListTasksByRequestAsync(existingAccept.RequestId, ct);
            var priorTask = priorTasks.LastOrDefault();
            if (accepted is not null
                && accepted.ArtifactId == artifact.ArtifactId
                && priorTask is { Status: ProductionTaskStatuses.Succeeded })
            {
                var priorAttempts = await _store.ListAttemptsAsync(priorTask.TaskId, ct);
                var priorAttempt = priorAttempts.LastOrDefault() ?? throw new InvalidOperationException("ATTEMPT_MISSING");
                return await SliceOfAsync(existingAccept, priorTask, priorAttempt, artifact, ct);
            }
        }
        var (request, task, snap, attempt) = await OpenFrozenAsync(
            command.Scope, ProductionActions.Accept, input, command.CreatedBy, idem,
            "os", "accept-v1", null, ct, command.Credential);
        AssertFrozenParents(snap, artifact.ArtifactId);
        await _os.SubmitProviderJobAsync(attempt.AttemptId, "accept-" + artifact.ArtifactId.ToString("N")[..12], ct);
        await _os.AcceptArtifactAsync(artifact.ArtifactId, ct);
        await _os.BindNamedAsync(artifact.ArtifactId, ProductionBindingRoles.AcceptedFor(artifact.Role), ct);
        await _os.CompleteDecisionAsync(attempt.AttemptId, ct);
        task = await _store.GetTaskAsync(task.TaskId, ct) ?? task;
        attempt = await _store.GetAttemptAsync(attempt.AttemptId, ct) ?? attempt;
        return await SliceOfAsync(request, task, attempt, artifact, ct);
    }

    public async Task<CreateVideoSliceResult> RegenerateOsAsync(ProductionActionApiCommand command, CancellationToken ct = default)
    {
        ProductionIdentityRules.ForbidUrlIdentity(command.IdempotencyKey, command.Input.VoiceRequirements);
        var priorId = command.Input.MotionArtifactId ?? throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        var prior = await RequireParentAsync(command.Scope, priorId, ProductionArtifactRoles.VideoTake, ct);
        var priorTask = await _store.GetTaskAsync(prior.TaskId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        IMotionExecutionAdapter adapter = new FixtureMotionAdapter();
        if (adapter.IsPaid || adapter.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        var parents = new List<Guid> { prior.ArtifactId };
        if (command.Input.KeyframeArtifactId is { } kf)
            parents.Add((await RequireParentAsync(command.Scope, kf, ProductionArtifactRoles.Keyframe, ct)).ArtifactId);
        var input = WithVoiceToken(command.Input with
        {
            ParentArtifactIds = parents,
            MotionArtifactId = prior.ArtifactId,
            DurationSec = prior.DurationSec > 0 ? prior.DurationSec : 5,
            VoiceRequirements = prior.ArtifactId.ToString("N") + "|" + priorTask.TaskId.ToString("N"),
            MotionRequirements = prior.ByteSha256,
        });
        var idem = Idem(command, ProductionActions.Regenerate, prior.ArtifactId.ToString("N"));
        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.VideoTake, ct);
        if (replay is not null)
        {
            if (replay.Artifact.ArtifactId == prior.ArtifactId)
                throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
            return replay;
        }
        var (request, task, snap, attempt) = await OpenFrozenAsync(
            command.Scope, ProductionActions.Regenerate, input, command.CreatedBy, idem,
            adapter.ProviderId, adapter.ModelId, null, ct, command.Credential);
        if (request.UserAction != ProductionActions.Regenerate || task.Action != ProductionActions.Regenerate)
            throw new InvalidOperationException(ProductionFailureReasons.ActionRemapForbidden);
        AssertFrozenParents(snap, prior.ArtifactId);
        var executed = await adapter.ExecuteFrozenAsync(snap, ct);
        var media = new ProductionMediaResult(executed.ProviderJobId, executed.Bytes, executed.MimeType, executed.DurationSec);
        var slice = await MaterializeMediaAsync(request, task, attempt, media, keyframe: false, voice: false, bindVisible: true, ct);
        if (slice.Artifact.ArtifactId == prior.ArtifactId)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var old = await _store.GetArtifactAsync(prior.ArtifactId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (old.ByteSha256 != prior.ByteSha256)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return slice;
    }

    public async Task<FilmAssemblySliceResult> AssembleOsAsync(ProductionActionApiCommand command, CancellationToken ct = default)
    {
        var shots = command.AssemblyShots ?? throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        if (shots.Count == 0)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        ProductionIdentityRules.ForbidUrlIdentity(command.IdempotencyKey);
        var historicalOfficialFilm = ProductionScopeAnchorRules.IsOfficialFilm(command.Scope.BuildId);
        var tenantOwnedEp01 = ProductionScopeAnchorRules.IsTenantOwnedEp01Build(
            command.Scope.BuildId, command.Scope.EpisodeCode);
        var officialFilm = historicalOfficialFilm || tenantOwnedEp01;
        var resolved = new List<(string ShotCode, ProductionArtifact Art, decimal DurationSec)>();
        foreach (var row in shots)
        {
            ProductionIdentityRules.ForbidUrlIdentity(row.ShotCode);
            var art = await RequireParentOnShotAsync(command.Scope.BuildId, row.ShotCode, row.ArtifactId, ct);
            RequirePhysicalParent(art);
            var accepted = await _store.GetActiveBindingAsync(command.Scope.BuildId, row.ShotCode, ProductionBindingRoles.Accepted, ct);
            if (accepted is null || accepted.ArtifactId != art.ArtifactId)
                throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
            if (officialFilm && art.Role != ProductionArtifactRoles.Mix)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            resolved.Add((row.ShotCode, art, art.DurationSec > 0 ? art.DurationSec : row.DurationSec));
        }
        var ids = resolved.Select(r => r.Art.ArtifactId).ToList();
        if (historicalOfficialFilm && ProductionEp01Sh01AssemblyCanon.MatchesScope(command.Scope)
            && (resolved.Count != 1
                || resolved[0].Art.ArtifactId != ProductionEp01Sh01AssemblyCanon.MixArtifactId
                || !string.Equals(resolved[0].Art.ByteSha256, ProductionEp01Sh01AssemblyCanon.MixSha256, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        if (historicalOfficialFilm && ProductionEp01Sh02AssembleCanon.MatchesScope(command.Scope))
        {
            if (resolved.Count != 1)
                throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
            var locked = ProductionEp01Sh02AssembleCanon.ParentLockError(resolved[0].Art);
            if (locked is not null)
                throw new InvalidOperationException(locked);
            var acceptedMix = await _store.GetActiveBindingAsync(
                command.Scope.BuildId, command.Scope.ShotCode,
                ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Mix), ct);
            if (acceptedMix is null || acceptedMix.ArtifactId != ProductionEp01Sh02AssembleCanon.MixArtifactId
                || acceptedMix.ArtifactId != resolved[0].Art.ArtifactId)
                throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        }
        if (officialFilm && resolved.Count != 1)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);

        var idemToken = string.Join('|', ids.Select(id => id.ToString("N")));
        var idem = Idem(command, ProductionActions.Assemble, idemToken);
        if (officialFilm)
            return await AssembleOfficialAsync(command, resolved, ids, idem, ct);

        var existing = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existing is not null)
        {
            var tasks = await _store.ListTasksByRequestAsync(existing.RequestId, ct);
            var task = tasks.LastOrDefault() ?? throw new InvalidOperationException("TASK_MISSING");
            var attempts = await _store.ListAttemptsAsync(task.TaskId, ct);
            var attempt = attempts.LastOrDefault() ?? throw new InvalidOperationException("ATTEMPT_MISSING");
            var assemblies = await _store.ListAssembliesByBuildAsync(command.Scope.BuildId, ct);
            var assembly = assemblies.LastOrDefault() ?? throw new InvalidOperationException("ASSEMBLY_MISSING");
            return new FilmAssemblySliceResult(existing, task, attempt, assembly, ids);
        }
        if (_assembly.IsPaid || _assembly.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        var input = command.Input with
        {
            ParentArtifactIds = ids,
            VoiceRequirements = idemToken,
        };
        var opened = await OpenFrozenAsync(
            command.Scope, ProductionActions.Assemble, input, command.CreatedBy, idem,
            _assembly.ProviderId, _assembly.ModelId, null, ct, command.Credential);
        var job = await _assembly.PlanFrozenAsync(opened.Snap, ids, ct);
        await _os.SubmitProviderJobAsync(opened.Attempt.AttemptId, job, ct);
        var film = await _os.CreateAssemblyAsync(command.Scope.BuildId, "TEST-ASSEMBLY", shots, ct);
        await _os.CompleteDecisionAsync(opened.Attempt.AttemptId, ct);
        var doneTask = await _store.GetTaskAsync(opened.Task.TaskId, ct) ?? opened.Task;
        var doneAttempt = await _store.GetAttemptAsync(opened.Attempt.AttemptId, ct) ?? opened.Attempt;
        return new FilmAssemblySliceResult(opened.Request, doneTask, doneAttempt, film, ids);
    }

    async Task<FilmAssemblySliceResult> AssembleOfficialAsync(
        ProductionActionApiCommand command,
        IReadOnlyList<(string ShotCode, ProductionArtifact Art, decimal DurationSec)> resolved,
        IReadOnlyList<Guid> ids,
        string idem,
        CancellationToken ct)
    {
        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Assembly, ct);
        if (replay is not null)
        {
            var reused = await FindAssemblyByMembershipAsync(command.Scope.BuildId, ids, ct)
                ?? throw new InvalidOperationException("ASSEMBLY_MISSING");
            return new FilmAssemblySliceResult(replay.Request, replay.Task, replay.Attempt, reused, ids, replay.Artifact);
        }

        var existing = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existing is not null)
        {
            var resumed = await TryResumeOfficialAssembleAsync(existing, command, resolved, ids, ct);
            if (resumed is not null)
                return resumed;
        }

        var adapter = _officialAssembly
            ?? throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        if (!string.Equals(adapter.ProviderId, ProductionAssemblyCatalog.ServerProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(adapter.ModelId, ProductionAssemblyCatalog.FfmpegModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (adapter.IsPaid || adapter.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        if (string.IsNullOrWhiteSpace(command.Input.Story))
            throw new InvalidOperationException(ProductionFailureReasons.MissingStory);

        var film = await FindAssemblyByMembershipAsync(command.Scope.BuildId, ids, ct)
            ?? await _os.CreateAssemblyAsync(
                command.Scope.BuildId,
                string.IsNullOrWhiteSpace(command.Scope.EpisodeCode) ? "EP01" : command.Scope.EpisodeCode.Trim(),
                resolved.Select(r => (r.ShotCode, r.Art.ArtifactId, r.DurationSec)).ToArray(),
                ct);
        var freezeShots = resolved.Select(r => new ProductionAssemblyFreeze.Shot(
            r.ShotCode, r.Art.Role, r.Art.ArtifactId, r.Art.ByteSha256, r.Art.StorageKey, r.DurationSec)).ToArray();
        var freeze = ProductionAssemblyFreeze.Token(film.AssemblyId, command.Scope.BuildId, freezeShots);
        var input = command.Input with
        {
            ParentArtifactIds = ids,
            VoiceRequirements = freeze,
            MotionRequirements = freeze,
            DurationSec = resolved[0].DurationSec,
        };
        var opened = await OpenFrozenAsync(
            command.Scope, ProductionActions.Assemble, input, command.CreatedBy, idem,
            adapter.ProviderId, adapter.ModelId, null, ct, command.Credential);
        AssertFrozenParents(opened.Snap, ids.ToArray());
        if (!ProductionAssemblyFreeze.TryParse(opened.Snap.ShotContract, out var frozenAssembly, out var frozenBuild, out var frozenShots)
            || frozenAssembly != film.AssemblyId
            || frozenBuild != command.Scope.BuildId
            || frozenShots.Count != 1
            || frozenShots[0].ArtifactId != resolved[0].Art.ArtifactId)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);

        var source = resolved[0].Art;
        var bytes = _files.Read(source.StorageKey);
        if (bytes is not { Length: >= 32 } || KitVideoIntegrityRules.Sha256Hex(bytes) != source.ByteSha256)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (ProductionEp01Sh02AssembleCanon.MatchesScope(command.Scope)
            && KitVideoIntegrityRules.Sha256Hex(bytes) != ProductionEp01Sh02AssembleCanon.MixSha256)
            throw new InvalidOperationException(ProductionFailureReasons.HashChanged);
        var executed = await ExecuteLocalProcessAsync(
            opened.Attempt.AttemptId,
            token => adapter.ExecuteFrozenAsync(opened.Snap, new AssemblyStartContext(opened.Attempt.AttemptId, bytes), token),
            ct);
        if (ProductionEp01Sh02AssembleCanon.MatchesScope(command.Scope)
            && ProductionMixContract.Dec(executed.DurationSec)
                != ProductionMixContract.Dec(ProductionEp01Sh02AssembleCanon.MixDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
        var slice = await MaterializeMediaAsync(
            opened.Request, opened.Task, opened.Attempt, executed, keyframe: false, voice: false, bindVisible: true, ct);
        if (slice.Artifact.Role != ProductionArtifactRoles.Assembly
            || slice.Artifact.ArtifactId == source.ArtifactId)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return new FilmAssemblySliceResult(slice.Request, slice.Task, slice.Attempt, film, ids, slice.Artifact);
    }

    async Task<FilmAssemblySliceResult?> TryResumeOfficialAssembleAsync(
        ProductionRequest existing,
        ProductionActionApiCommand command,
        IReadOnlyList<(string ShotCode, ProductionArtifact Art, decimal DurationSec)> resolved,
        IReadOnlyList<Guid> ids,
        CancellationToken ct)
    {
        var task = (await _store.ListTasksByRequestAsync(existing.RequestId, ct)).LastOrDefault();
        if (task is null
            || task.Action != ProductionActions.Assemble
            || task.Status is not (ProductionTaskStatuses.Running or ProductionTaskStatuses.ProviderFrozen))
            return null;
        var attempt = (await _store.ListAttemptsAsync(task.TaskId, ct)).LastOrDefault();
        if (attempt is null
            || attempt.Status != ProductionAttemptStatuses.Started
            || !string.IsNullOrWhiteSpace(attempt.ProviderJobId))
            return null;
        var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct);
        if (snap is null)
            return null;
        var adapter = _officialAssembly
            ?? throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        var film = await FindAssemblyByMembershipAsync(command.Scope.BuildId, ids, ct)
            ?? throw new InvalidOperationException("ASSEMBLY_MISSING");
        var source = resolved[0].Art;
        var bytes = _files.Read(source.StorageKey);
        if (bytes is not { Length: >= 32 } || KitVideoIntegrityRules.Sha256Hex(bytes) != source.ByteSha256)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var executed = await adapter.ExecuteFrozenAsync(
            snap, new AssemblyStartContext(attempt.AttemptId, bytes), ct);
        var slice = await MaterializeMediaAsync(
            existing, task, attempt, executed, keyframe: false, voice: false, bindVisible: true, ct);
        if (slice.Artifact.Role != ProductionArtifactRoles.Assembly
            || slice.Artifact.ArtifactId == source.ArtifactId)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return new FilmAssemblySliceResult(slice.Request, slice.Task, slice.Attempt, film, ids, slice.Artifact);
    }

    async Task<FilmAssembly?> FindAssemblyByMembershipAsync(Guid buildId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var assemblies = await _store.ListAssembliesByBuildAsync(buildId, ct);
        foreach (var assembly in assemblies)
        {
            var rows = await _store.ListAssemblyShotsAsync(assembly.AssemblyId, ct);
            if (rows.Count == ids.Count && rows.Select(r => r.ArtifactId).SequenceEqual(ids))
                return assembly;
        }
        return null;
    }

    public async Task<FilmAssemblySliceResult> AssembleEpisodeOsAsync(
        ProductionActionApiCommand command, CancellationToken ct = default)
    {
        var legacy = ProductionEpisodeAssemblyContract.RejectLegacy(
            string.Join(' ', command.Input.Timing, command.Input.MotionRequirements, command.Input.VoiceRequirements));
        if (legacy is not null)
            throw new InvalidOperationException(legacy);
        if (!ProductionEpisodeAssemblyContract.IsEpisodeScope(command.Scope))
            throw new InvalidOperationException(ProductionFailureReasons.CrossScopeForbidden);
        var shots = command.AssemblyShots ?? throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        if (shots.Count < 2)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        var shotCodes = shots.Select(s => s.ShotCode.Trim()).ToArray();
        var fullPermit = ShortProductionQualityPipelineV1.FullEpisodeConcatPermitted(shotCodes, command.QualityVerdicts);
        if (shotCodes.SequenceEqual(ShortProductionQualityPipelineV1.Ep01ShotOrder) && !fullPermit)
            throw new InvalidOperationException(ShortProductionQualityPipelineV1.ShotNotAccepted);
        ProductionIdentityRules.ForbidUrlIdentity(command.IdempotencyKey);

        var resolved = new List<ProductionEpisodeAssemblyResolved>();
        var liveRows = new List<ProductionEpisodeAssemblyInput>();
        var parentBytes = new List<byte[]>();
        foreach (var row in shots)
        {
            ProductionIdentityRules.ForbidUrlIdentity(row.ShotCode);
            var current = await _store.GetActiveBindingAsync(
                command.Scope.BuildId, row.ShotCode,
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly), ct);
            if (current is null)
                throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
            if (current.ArtifactId != row.ArtifactId)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            var art = await RequireParentOnShotAsync(command.Scope.BuildId, row.ShotCode, row.ArtifactId, ct);
            if (art.Role != ProductionArtifactRoles.Assembly)
                throw new InvalidOperationException(ProductionFailureReasons.ArtifactRoleMismatch);
            var visible = await _store.GetActiveBindingAsync(
                command.Scope.BuildId, row.ShotCode,
                ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly), ct);
            var bytes = _files.Read(art.StorageKey);
            if (bytes is not { Length: >= 32 })
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            var fileSha = KitVideoIntegrityRules.Sha256Hex(bytes);
            if (ProductionMixContract.NormSha(fileSha) != ProductionMixContract.NormSha(art.ByteSha256))
                throw new InvalidOperationException(ProductionFailureReasons.HashChanged);
            var owner = await _store.GetTaskAsync(art.TaskId, ct)
                ?? throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
            resolved.Add(new ProductionEpisodeAssemblyResolved(
                row.ShotCode, art, current.ArtifactId, visible?.ArtifactId,
                fileSha, art.StorageKey, owner.Scope.BuildId, owner.Scope.ShotCode));
            liveRows.Add(new ProductionEpisodeAssemblyInput(
                liveRows.Count, row.ShotCode.Trim(), ProductionArtifactRoles.Assembly,
                art.ArtifactId, art.ByteSha256, art.StorageKey, art.DurationSec, 0, 0));
            parentBytes.Add(bytes);
        }

        var eval = ProductionEpisodeAssemblyContract.Evaluate(
            command.Scope,
            shots.Select(s => (s.ShotCode, s.ArtifactId)).ToArray(),
            resolved,
            streamCompatible: true,
            legacy,
            fullPermit);
        if (eval is not null)
            throw new InvalidOperationException(eval);

        var contract = ProductionEpisodeAssemblyContract.Build(
            command.Scope.BuildId, command.Scope.EpisodeCode, liveRows, fullPermit);
        var freeze = ProductionEpisodeAssemblyContract.Token(contract);
        var ids = contract.OrderedInputs.Select(r => r.ArtifactId).ToList();
        var idem = EpisodeIdem(contract);

        var replay = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Assembly, ct);
        if (replay is not null)
        {
            var reused = await EnsureEpisodeMembershipAsync(command.Scope, contract, ct);
            return new FilmAssemblySliceResult(replay.Request, replay.Task, replay.Attempt, reused, ids, replay.Artifact);
        }

        var adapter = _officialEpisode
            ?? throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        if (adapter.IsPaid || adapter.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        if (string.IsNullOrWhiteSpace(command.Input.Story))
            throw new InvalidOperationException(ProductionFailureReasons.MissingStory);

        var input = command.Input with
        {
            ParentArtifactIds = ids,
            VoiceRequirements = freeze,
            MotionRequirements = freeze,
            DurationSec = contract.OutputDurationSec,
        };
        (ProductionRequest Request, ProductionTask Task, ProductionTaskInputSnapshot Snap, ProductionAttempt Attempt) opened;
        try
        {
            opened = await OpenFrozenAsync(
                command.Scope, ProductionActions.AssembleEpisode, input, command.CreatedBy, idem,
                adapter.ProviderId, adapter.ModelId, null, ct, command.Credential);
        }
        catch (InvalidOperationException ex) when (
            ex.Message == "IDEMPOTENCY_CONFLICT" || ex.Message == ProductionFailureReasons.InFlightReplay)
        {
            var again = await AwaitOfficialEpisodeResultAsync(idem, ct);
            if (again is null)
                throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
            var raced = await EnsureEpisodeMembershipAsync(command.Scope, contract, ct);
            return new FilmAssemblySliceResult(again.Request, again.Task, again.Attempt, raced, ids, again.Artifact);
        }
        AssertFrozenParents(opened.Snap, ids.ToArray());
        if (!ProductionEpisodeAssemblyContract.TryParse(opened.Snap.ShotContract, out var frozen)
            || ProductionEpisodeAssemblyContract.MatchesFrozen(frozen, contract.OrderedInputs) is not null
            || ProductionMixContract.Dec(opened.Snap.DurationSec) != ProductionMixContract.Dec(contract.OutputDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);

        var executed = await ExecuteLocalProcessAsync(
            opened.Attempt.AttemptId,
            token => adapter.ExecuteFrozenAsync(opened.Snap, new EpisodeAssemblyStartContext(opened.Attempt.AttemptId, parentBytes), token),
            ct);
        if (ProductionMixContract.Dec(executed.DurationSec) != ProductionMixContract.Dec(contract.OutputDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
        var slice = await MaterializeMediaAsync(
            opened.Request, opened.Task, opened.Attempt, executed, keyframe: false, voice: false, bindVisible: true, ct);
        if (slice.Artifact.Role != ProductionArtifactRoles.Assembly
            || ids.Contains(slice.Artifact.ArtifactId))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var film = await EnsureEpisodeMembershipAsync(command.Scope, contract, ct);
        return new FilmAssemblySliceResult(slice.Request, slice.Task, slice.Attempt, film, ids, slice.Artifact);
    }

    static string EpisodeIdem(ProductionEpisodeAssemblyContractV1 contract) =>
        string.Join(':',
            ProductionActions.AssembleEpisode,
            contract.ContractVersion,
            contract.BuildId.ToString("N"),
            contract.EpisodeCode,
            contract.ContractHash);

    async Task<FilmAssembly> EnsureEpisodeMembershipAsync(
        ProductionScope scope,
        ProductionEpisodeAssemblyContractV1 contract,
        CancellationToken ct)
    {
        var ids = contract.OrderedInputs.Select(r => r.ArtifactId).ToArray();
        var existing = await FindAssemblyByMembershipAsync(scope.BuildId, ids, ct);
        if (existing is not null)
            return existing;
        return await _os.CreateAssemblyAsync(
            scope.BuildId,
            scope.EpisodeCode.Trim(),
            contract.OrderedInputs.Select(r => (r.ShotCode, r.ArtifactId, r.DurationSec)).ToArray(),
            ct,
            sortOrigin: 1);
    }

    async Task<CreateVideoSliceResult> MaterializeMediaAsync(
        ProductionRequest request,
        ProductionTask task,
        ProductionAttempt attempt,
        ProductionMediaResult executed,
        bool keyframe,
        bool voice,
        bool bindVisible,
        CancellationToken ct,
        bool bindCurrent = true)
    {
        if (executed.Bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var folder = voice
            ? await VoiceFolderAsync(task.Scope, ct)
            : await StorageFolderAsync(task.Scope, keyframe, ct);
        var storage = _files.Write(folder, attempt.AttemptId.ToString("N"), executed.Bytes);
        await _os.SubmitProviderJobAsync(attempt.AttemptId, executed.ProviderJobId, ct);
        await _os.CompleteAttemptAsync(attempt.AttemptId, KitVideoIntegrityRules.Sha256Hex(executed.Bytes), ct);
        var artifact = await _os.VerifyArtifactAsync(attempt.AttemptId, executed.Bytes, executed.MimeType, executed.DurationSec, storage, ct);
        if (bindCurrent)
        {
            if (voice)
                await TryBindNamedProtectedAsync(artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice), ct);
            else if (keyframe)
                await TryBindProtectedAsync(() => _os.BindCurrentProtectedAsync(artifact.ArtifactId, ct), task, ProductionBindingRoles.Current, ct);
            else
                await TryBindNamedProtectedAsync(artifact.ArtifactId, ProductionBindingRoles.CurrentFor(artifact.Role), ct);
        }
        if (bindVisible)
            await TryBindProtectedAsync(() => _os.BindVisibleProtectedAsync(artifact.ArtifactId, ct), task, ProductionBindingRoles.Visible, ct);
        return await SliceOfAsync(request, await _store.GetTaskAsync(task.TaskId, ct) ?? task,
            await _store.GetAttemptAsync(attempt.AttemptId, ct) ?? attempt, artifact, ct);
    }

    async Task<(ProductionRequest Request, ProductionTask Task, ProductionTaskInputSnapshot Snap, ProductionAttempt Attempt)>
        OpenFrozenAsync(
            ProductionScope scope,
            string action,
            ShotExecutionInput input,
            string createdBy,
            string idem,
            string providerId,
            string modelId,
            string? snapHash,
            CancellationToken ct,
            ProductionCredentialReference? credential = null)
    {
        var existing = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existing is not null)
        {
            var tasks = await _store.ListTasksByRequestAsync(existing.RequestId, ct);
            var task = tasks.LastOrDefault();
            if (task is not null && task.Status is ProductionTaskStatuses.Succeeded)
                throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
            if (task is not null && task.Status is ProductionTaskStatuses.Running or ProductionTaskStatuses.ProviderFrozen)
                throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
            if (task is not null && task.Status is ProductionTaskStatuses.Failed or ProductionTaskStatuses.FailedIdentity)
            {
                if (TenantPilotDeniesSameFingerprintRetry(task))
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(task.FailureCode)
                        ? ProductionFailureReasons.PaidNetworkForbidden
                        : task.FailureCode);
                var frozen = await _store.GetSnapshotByTaskAsync(task.TaskId, ct)
                    ?? throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
                var retried = await _os.BeginOfficialProviderStartAsync(task.TaskId, snapHash ?? frozen.SnapshotHash, null, ct);
                return (existing, task, frozen, retried);
            }
        }
        var request = await _os.CreateRequestAsync(scope, action, createdBy, idem, ct: ct);
        var already = await _store.ListTasksByRequestAsync(request.RequestId, ct);
        if (already.Count > 0)
            throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
        var taskNew = await _os.CreateTaskAsync(request.RequestId, ct);
        var snap = await _os.FreezeInputAsync(taskNew.TaskId, input, providerId, modelId, ct, credential);
        var attempt = await _os.StartAttemptAsync(taskNew.TaskId, snapHash ?? snap.SnapshotHash, ct);
        return (request, taskNew, snap, attempt);
    }

    static bool IsLocalExecutionProcessFailure(Exception ex) =>
        ex is InvalidOperationException
        && ex.Message.StartsWith(ProductionFailureReasons.FailedIdentity + ":ffmpeg", StringComparison.Ordinal);

    async Task<ProductionMediaResult> ExecuteLocalProcessAsync(
        Guid attemptId,
        Func<CancellationToken, Task<ProductionMediaResult>> execute,
        CancellationToken ct)
    {
        try
        {
            return await execute(ct);
        }
        catch (InvalidOperationException ex) when (IsLocalExecutionProcessFailure(ex))
        {
            await _os.RecordExecutionProcessFailedAsync(attemptId, ex.Message, ct);
            throw;
        }
    }

    async Task<CreateVideoSliceResult?> AwaitOfficialEpisodeResultAsync(string idem, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var again = await ReplaySucceededAsync(idem, ProductionArtifactRoles.Assembly, ct);
            if (again is not null)
                return again;
            var existing = await _store.GetRequestByIdempotencyAsync(idem, ct);
            if (existing is null)
                return null;
            var task = (await _store.ListTasksByRequestAsync(existing.RequestId, ct)).LastOrDefault();
            if (task is not null && task.Status is ProductionTaskStatuses.Failed or ProductionTaskStatuses.FailedIdentity)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(task.FailureCode) ? task.Status : task.FailureCode);
            if (DateTime.UtcNow >= deadline)
                return null;
            await Task.Delay(50, ct);
        }
    }

    async Task<CreateVideoSliceResult?> ReplaySucceededAsync(string idem, string role, CancellationToken ct)
    {
        var existing = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existing is null) return null;
        var prior = await ExistingArtifactAsync(existing.RequestId, role, ct);
        if (prior is null) return null;
        var tasks = await _store.ListTasksByRequestAsync(existing.RequestId, ct);
        var task = tasks.LastOrDefault() ?? throw new InvalidOperationException("TASK_MISSING");
        var attempts = await _store.ListAttemptsAsync(task.TaskId, ct);
        var attempt = attempts.LastOrDefault() ?? throw new InvalidOperationException("ATTEMPT_MISSING");
        return await SliceOfAsync(existing, task, attempt, prior, ct);
    }

    async Task<ProductionArtifact> RequireParentAsync(ProductionScope scope, Guid artifactId, string? artifactRole, CancellationToken ct)
    {
        var art = await _store.GetArtifactAsync(artifactId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        if (art.VerificationStatus != ProductionArtifactStatuses.Verified)
            throw new InvalidOperationException(ProductionFailureReasons.FailedVerification);
        if (string.IsNullOrWhiteSpace(art.ByteSha256)
            || string.IsNullOrWhiteSpace(art.StorageKey)
            || art.StorageKey.Contains("://", StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (artifactRole is not null && art.Role != artifactRole)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var owner = await _store.GetTaskAsync(art.TaskId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        if (!string.Equals(owner.Scope.ShotCode, scope.ShotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.CrossShotForbidden);
        if (owner.Scope.BuildId != scope.BuildId)
            throw new InvalidOperationException(ProductionFailureReasons.CrossScopeForbidden);
        if (_scopes is not null)
        {
            var requestKind = ProductionScopeGuard.Resolve(await _scopes.GetAnchorAsync(scope.BuildId, ct), scope.BuildId);
            var ownerKind = ProductionScopeGuard.Resolve(await _scopes.GetAnchorAsync(owner.Scope.BuildId, ct), owner.Scope.BuildId);
            ProductionScopeGuard.RequireSameScope(scope.BuildId, requestKind, owner.Scope.BuildId, ownerKind);
        }
        return art;
    }

    async Task<ProductionArtifact> RequireParentOnShotAsync(Guid buildId, string shotCode, Guid artifactId, CancellationToken ct)
        => await RequireParentAsync(new ProductionScope(buildId, "", "", shotCode), artifactId, null, ct);

    void RequirePhysicalParent(ProductionArtifact art)
    {
        var bytes = _files.Read(art.StorageKey);
        if (bytes is null || bytes.Length < 32 || KitVideoIntegrityRules.Sha256Hex(bytes) != art.ByteSha256)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
    }

    static void AssertFrozenParents(ProductionTaskInputSnapshot snap, params Guid[] required)
    {
        var have = snap.ParentArtifactIds ?? [];
        foreach (var id in required)
        {
            if (!have.Contains(id))
                throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        }
    }

    static bool IsTestVoiceIdentity(ShotExecutionInput input) =>
        string.Equals((input.VoiceId ?? "").Trim(), "VOICE-TEST", StringComparison.Ordinal)
        || string.Equals((input.SpeakerCharacterId ?? "").Trim(), "CHAR-TEST", StringComparison.Ordinal);

    static ShotExecutionInput WithVoiceToken(ShotExecutionInput input) =>
        input with
        {
            Story = string.IsNullOrWhiteSpace(input.Story) ? ProductionTestCreateVideoContext.Story : input.Story,
            ActingBeat = string.IsNullOrWhiteSpace(input.ActingBeat) ? ProductionTestCreateVideoContext.ActingBeat : input.ActingBeat,
            VisualContract = string.IsNullOrWhiteSpace(input.VisualContract) ? ProductionTestCreateVideoContext.VisualContract : input.VisualContract,
            Timing = string.IsNullOrWhiteSpace(input.Timing) ? ProductionTestCreateVideoContext.Timing : input.Timing,
        };

    static bool TenantPilotDeniesSameFingerprintRetry(ProductionTask task) =>
        ProductionScopeAnchorRules.IsTenantOwnedEp01Build(task.Scope.BuildId, task.Scope.EpisodeCode)
        && IsPilotPaidProvider(task.ProviderId);

    static bool IsPilotPaidProvider(string? providerId)
    {
        var id = (providerId ?? "").Trim();
        return id.Equals(ProductionImageCatalog.GeminiProviderId, StringComparison.OrdinalIgnoreCase)
            || id.Equals(FamixaProviderIds.Google, StringComparison.OrdinalIgnoreCase)
            || id.Equals(FamixaProviderIds.ElevenLabs, StringComparison.OrdinalIgnoreCase)
            || id.Equals(FamixaProviderIds.Fal, StringComparison.OrdinalIgnoreCase);
    }

    static string Idem(ProductionActionApiCommand command, string action, string token)
        => string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? $"{command.Scope.BuildId:N}:{command.Scope.ShotCode}:{action}:{token}"
            : command.IdempotencyKey.Trim();

    /// <summary>production_requests.idempotency_key is VARCHAR(180). The master token does not fit beside the UI key.</summary>
    static string FitIdem(string idem, string references)
    {
        var stamp = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(references)))[..16].ToLowerInvariant();
        var suffix = ":ref:" + stamp;
        var room = 180 - suffix.Length;
        var head = idem.Length <= room ? idem : idem[..room];
        return head + suffix;
    }

    async Task RequireTenantMixParentsAsync(
        ProductionScope scope, ProductionArtifact video, ProductionArtifact lipsync, CancellationToken ct)
    {
        if (IsTenantSh02OfficialVideoCopy(scope, video)
            || IsHistoricalProductionParent(video.ArtifactId)
            || IsHistoricalProductionParent(lipsync.ArtifactId)
            || lipsync.ArtifactId == ProductionEp01Sh01MixCanon.LipsyncArtifactId
            || lipsync.ArtifactId == ProductionEp01Sh02MixCanon.LipsyncArtifactId)
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
        var lipTask = await _store.GetTaskAsync(lipsync.TaskId, ct);
        if (lipTask is not null && ProductionLipsyncCatalog.IsFixtureProvider(lipTask.ProviderId))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var voice = await CurrentTypedArtifactAsync(scope, ProductionArtifactRoles.Voice, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.MissingVoiceArtifact);
        voice = await RequireParentAsync(scope, voice.ArtifactId, ProductionArtifactRoles.Voice, ct);
        RequirePhysicalParent(voice);
        if (IsHistoricalProductionParent(voice.ArtifactId))
            throw new InvalidOperationException(ProductionFailureReasons.BindingChanged);
    }

    static bool IsTenantSh02OfficialVideoCopy(ProductionScope scope, ProductionArtifact video) =>
        ProductionEp01Sh02LipsyncCanon.IsTenantByteCopy(scope, video.ByteSha256);

    async Task<bool> ExistingMixContractMatchesAsync(Guid artifactId, string contractHash, CancellationToken ct)
    {
        var art = await _store.GetArtifactAsync(artifactId, ct);
        if (art is null)
            return true;
        var snap = await _store.GetSnapshotByTaskAsync(art.TaskId, ct);
        if (snap is null || !ProductionMixFreeze.TryParseV1(snap.ShotContract, out var prior))
            return true;
        return string.Equals(prior.ContractHash, contractHash, StringComparison.Ordinal);
    }

    static bool IsHistoricalProductionParent(Guid artifactId) =>
        artifactId == ProductionEp01Sh01LipsyncCanon.VideoArtifactId
        || artifactId == ProductionEp01Sh01LipsyncCanon.VoiceArtifactId
        || artifactId == ProductionEp01Sh02LipsyncCanon.VideoArtifactId
        || artifactId == ProductionEp01Sh02LipsyncCanon.VoiceArtifactId;

    static string SpeakerForRecoveredLine(string? lineId)
    {
        var line = (lineId ?? "").Trim();
        if (string.Equals(line, ProductionEp01Sh01LipsyncCanon.LineId, StringComparison.Ordinal))
            return ProductionEp01Sh01LipsyncCanon.SpeakerCharacterId;
        if (string.Equals(line, ProductionEp01Sh02LipsyncCanon.LineId, StringComparison.Ordinal))
            return ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId;
        return "";
    }

    async Task<ProductionArtifact?> CurrentTypedArtifactAsync(
        ProductionScope scope, string artifactRole, CancellationToken ct)
    {
        var bind = await _store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(artifactRole), ct);
        if (bind is null) return null;
        return await _store.GetArtifactAsync(bind.ArtifactId, ct);
    }

    async Task<ProductionArtifact?> AcceptedTypedArtifactAsync(
        ProductionScope scope, string artifactRole, CancellationToken ct)
    {
        var bind = await _store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.AcceptedFor(artifactRole), ct);
        if (bind is null) return null;
        return await _store.GetArtifactAsync(bind.ArtifactId, ct);
    }

    async Task<CreateVideoSliceResult?> TryRecoverOfficialLipsyncAsync(
        ProductionRequest existing, ProductionScope scope, CancellationToken ct)
    {
        var task = (await _store.ListTasksByRequestAsync(existing.RequestId, ct)).LastOrDefault();
        if (task is null || task.Action != ProductionActions.CreateLipsync) return null;
        var attempt = (await _store.ListAttemptsAsync(task.TaskId, ct)).LastOrDefault();
        if (attempt is null || string.IsNullOrWhiteSpace(attempt.ProviderJobId)) return null;
        var existingArt = (await _store.ListArtifactsByTaskAsync(task.TaskId, ct))
            .LastOrDefault(a => a.Role == ProductionArtifactRoles.Lipsync
                && a.VerificationStatus == ProductionArtifactStatuses.Verified
                && a.AttemptId == attempt.AttemptId);
        if (existingArt is not null)
        {
            await TryBindNamedProtectedAsync(existingArt.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), ct);
            await TryBindProtectedAsync(() => _os.BindVisibleProtectedAsync(existingArt.ArtifactId, ct), task, ProductionBindingRoles.Visible, ct);
            return await SliceOfAsync(
                await _store.GetRequestAsync(existing.RequestId, ct) ?? existing,
                await _store.GetTaskAsync(task.TaskId, ct) ?? task,
                await _store.GetAttemptAsync(attempt.AttemptId, ct) ?? attempt,
                existingArt, ct);
        }
        var folder = await StorageFolderAsync(scope, keyframe: false, ct);
        var storage = $"{folder}/{attempt.AttemptId:N}";
        var bytes = _files.Read(storage);
        if (bytes is { Length: >= 32 }
            && ProductionTestMp4.IsDecodable(bytes, out _)
            && ProductionTestMp4.TryReadDuration(bytes, out var duration)
            && duration > 0)
        {
            var artifact = await _os.VerifyArtifactAsync(
                attempt.AttemptId, bytes, ProductionTestMp4.Mime, duration, storage, ct);
            await TryBindNamedProtectedAsync(artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), ct);
            await TryBindProtectedAsync(() => _os.BindVisibleProtectedAsync(artifact.ArtifactId, ct), task, ProductionBindingRoles.Visible, ct);
            return await SliceOfAsync(
                await _store.GetRequestAsync(existing.RequestId, ct) ?? existing,
                await _store.GetTaskAsync(task.TaskId, ct) ?? task,
                await _store.GetAttemptAsync(attempt.AttemptId, ct) ?? attempt,
                artifact, ct);
        }
        if (_officialLipsync is not FalLipSyncExecutionAdapter fal)
            return null;
        var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct);
        if (snap is null) return null;
        ProductionMediaResult executed;
        try
        {
            executed = await fal.RecoverFrozenAsync(snap, attempt.ProviderJobId, ct);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith(ProductionFailureReasons.RecoveryFailed, StringComparison.Ordinal)
            || ex.Message == ProductionFailureReasons.FailedIdentity
            || ex.Message == ProductionFailureReasons.OutputDownloadFailed)
        {
            await _os.FailAttemptAsync(attempt.AttemptId, ProductionFailureReasons.RecoveryFailed, ct);
            throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        }
        return await MaterializeMediaAsync(
            await _store.GetRequestAsync(existing.RequestId, ct) ?? existing,
            await _store.GetTaskAsync(task.TaskId, ct) ?? task,
            await _store.GetAttemptAsync(attempt.AttemptId, ct) ?? attempt,
            executed, keyframe: false, voice: false, bindVisible: true, ct);
    }

    static bool SameProviderJob(string persisted, string incoming)
    {
        var a = persisted.Trim();
        var b = incoming.Trim();
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        const string prefix = "lipsync_v3_";
        if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            a = a[prefix.Length..];
        if (b.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            b = b[prefix.Length..];
        return Guid.TryParse(a, out var ga) && Guid.TryParse(b, out var gb) && ga == gb;
    }

    async Task<CreateVideoSliceResult?> TryRecoverOfficialVoiceAsync(
        ProductionRequest existing, ProductionScope scope, CancellationToken ct)
    {
        var task = (await _store.ListTasksByRequestAsync(existing.RequestId, ct)).LastOrDefault();
        if (task is null || task.Action != ProductionActions.CreateVoice) return null;
        var attempt = (await _store.ListAttemptsAsync(task.TaskId, ct)).LastOrDefault();
        if (attempt is null || string.IsNullOrWhiteSpace(attempt.ProviderJobId)) return null;
        if (attempt.ProviderJobId.StartsWith(FakeElevenLabsVoiceTransport.JobPrefix, StringComparison.Ordinal))
            return null;
        var folder = await VoiceFolderAsync(scope, ct);
        var storage = $"{folder}/{attempt.AttemptId:N}";
        var bytes = _files.Read(storage);
        if (!ProductionVoicePhysical.TryVerify(bytes, ProductionVoicePhysical.MpegMime, 1, out var duration))
            return null;
        var artifact = await _os.VerifyArtifactAsync(
            attempt.AttemptId, bytes!, ProductionVoicePhysical.MpegMime, duration, storage, ct);
        await TryBindNamedProtectedAsync(artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice), ct);
        return await SliceOfAsync(
            await _store.GetRequestAsync(existing.RequestId, ct) ?? existing,
            await _store.GetTaskAsync(task.TaskId, ct) ?? task,
            await _store.GetAttemptAsync(attempt.AttemptId, ct) ?? attempt,
            artifact, ct);
    }

    async Task<string> VoiceFolderAsync(ProductionScope scope, CancellationToken ct)
    {
        if (_scopes is null) return "os-voice";
        var anchor = await _scopes.GetAnchorAsync(scope.BuildId, ct);
        var kind = ProductionScopeGuard.Resolve(anchor, scope.BuildId);
        return ProductionStoragePolicy.VoiceFolder(kind, anchor?.TestRunId);
    }
}
