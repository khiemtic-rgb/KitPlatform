using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class FamixaSh02DownstreamReadinessV1Regression
{
    public const string SuiteId = "FAMIXA_VIDEO_RECOVERY_002_DOWNSTREAM";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        try
        {
            var real = SizedVideo();
            var playable = FamixaSh02DownstreamReadiness.Evaluate(real);
            Ok(playable.PlayableRealVideo
               && !playable.VoiceVerified
               && !playable.MixAcceptable
               && !playable.EpisodeAssembled
               && playable.Defects.Contains(FamixaSh02DownstreamReadiness.SpeechUnverified),
                "a structurally playable Veo file is not verified speech, mix, or an episode");

            var shortAudio = FamixaSh02DownstreamReadiness.Evaluate(real with { AudioDurationSec = 3m });
            Ok(!shortAudio.MixAcceptable
               && shortAudio.Defects.Contains(FamixaSh02DownstreamReadiness.AudioShorterThanVideo),
                "audio shorter than the video is not an acceptable mix");

            var silence = FamixaSh02DownstreamReadiness.Evaluate(real with
            {
                AudioDurationSec = real.DurationSec,
                LeadingSilenceSec = 2.5m,
                ApprovedLeadSec = 0m,
            });
            Ok(!silence.MixAcceptable
               && silence.Defects.Contains(FamixaSh02DownstreamReadiness.ExcessSilence),
                "leading silence beyond the approved speech start is rejected");

            var timingOnly = FamixaSh02DownstreamReadiness.Evaluate(real with
            {
                AudioDurationSec = real.DurationSec,
                SpeechTimingVerified = true,
            });
            Ok(!timingOnly.VoiceVerified && !timingOnly.MixAcceptable,
                "speech timing alone does not verify Minh's voice or usage rights");

            var evidenced = FamixaSh02DownstreamReadiness.Evaluate(real with
            {
                AudioDurationSec = real.DurationSec,
                SpeechContentVerified = true,
                MinhVoiceIdentityVerified = true,
                UsageRightsVerified = true,
                SpeechTimingVerified = true,
            });
            Ok(evidenced.VoiceVerified && evidenced.MixAcceptable && !evidenced.EpisodeAssembled,
                "voice is verified only when content, identity, rights, and timing are all present");

            var fixture = ProductionTestMp4.Encode("sh02-fixture", 8);
            var mistaken = FamixaSh02DownstreamReadiness.Evaluate(real with
            {
                Bytes = fixture,
                ByteSha256 = KitVideoIntegrityRules.Sha256Hex(fixture),
                ProviderJobId = "projects/recovery/operations/fixture-looking",
            });
            Ok(!mistaken.PlayableRealVideo
               && mistaken.Defects.Contains(FamixaSh02DownstreamReadiness.FixtureNotReal),
                "a fixture MP4 is not a real Veo result even when the provider fields say google");

            var knownMix = FamixaSh02DownstreamReadiness.Evaluate(real with
            {
                Role = ProductionArtifactRoles.Mix,
                ArtifactId = ProductionEp01Sh02AssembleCanon.MixArtifactId,
                ByteSha256 = ProductionEp01Sh02AssembleCanon.MixSha256,
            });
            Ok(!knownMix.PlayableRealVideo
               && !knownMix.EpisodeAssembled
               && knownMix.Defects.Contains(FamixaSh02DownstreamReadiness.MixIsNotEpisode),
                "the current SH02 mix is not an assembled episode");

            var incomplete = FamixaSh02DownstreamReadiness.Evaluate(real with
            {
                Role = ProductionArtifactRoles.Assembly,
                BoundShotCount = 1,
                ExpectedShotCount = 6,
            });
            Ok(!incomplete.EpisodeAssembled
               && incomplete.Defects.Contains(FamixaSh02DownstreamReadiness.EpisodeIncomplete),
                "an assembly that does not cover the episode is incomplete");
        }
        catch (Exception ex)
        {
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message);
        }

        return fail;
    }

    static FamixaDownstreamCandidate SizedVideo()
    {
        var bytes = ProductionTestMp4.EncodeSized("sh02-structure", 8, 1280, 720);
        return new FamixaDownstreamCandidate(
            FamixaProviderIds.Google,
            ProductionMotionCatalog.Veo31ModelId,
            ProductionArtifactRoles.VideoTake,
            ProductionTestMp4.Mime,
            bytes,
            KitVideoIntegrityRules.Sha256Hex(bytes),
            8m,
            0m,
            0m,
            0m,
            0m,
            0m,
            false,
            "projects/recovery/locations/us-central1/operations/op-structure",
            Guid.NewGuid(),
            0,
            6,
            false,
            false,
            false,
            false);
    }
}
