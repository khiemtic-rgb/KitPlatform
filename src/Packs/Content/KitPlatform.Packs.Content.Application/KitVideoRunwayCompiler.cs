using System.Text.Json;

namespace KitPlatform.Packs.Content;

public sealed class KitVideoRunwayCompiler : IRunwayRequestCompiler
{
    public KitVideoRunwayRequestDto Compile(KitVideoI2vReadyPackageDto pack, JsonElement motionContract)
    {
        if (pack is null || !pack.Ready)
            throw new InvalidOperationException("PREFLIGHT: I2V_READY = FALSE — RUNWAY_NOT_CALLED.");
        if (!string.Equals(pack.Source, "APPROVED_KEYFRAME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PREFLIGHT: inputImage phải là APPROVED_KEYFRAME.");
        var compiled = KitVideoMotionRules.Compile(motionContract);
        KitVideoMotionRules.EnsureGoldenShot(compiled.ShotCode);
        return new KitVideoRunwayRequestDto(
            compiled.Model,
            "APPROVED_KEYFRAME",
            compiled.DurationSec,
            compiled.Ratio,
            compiled.Prompt,
            pack.SourceArtifactHash ?? "");
    }
}

public sealed class KitVideoOutputQaEngine : IVideoOutputQA
{
    public KitVideoVideoQaDto Evaluate(JsonElement motionContract, JsonElement observation) =>
        KitVideoMotionRules.EvaluateDeclared(motionContract, observation);
}
