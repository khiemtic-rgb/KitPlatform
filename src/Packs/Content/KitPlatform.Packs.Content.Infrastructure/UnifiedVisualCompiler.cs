namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Infrastructure port for UnifiedVisualCompilerV1Rules. No Gemini. No persistence.</summary>
internal sealed class UnifiedVisualCompiler : IUnifiedVisualCompiler
{
    public IReadOnlyList<string> RunRegression() => UnifiedVisualCompilerV1Regression.Run();

    public UnifiedVisualContract Compile(
        VisualUniverseSnapshot snapshot,
        UnifiedVisualIdentityLayer? identity = null,
        UnifiedVisualSceneLayer? scene = null,
        string? view = null) =>
        UnifiedVisualCompilerV1Rules.Compile(snapshot, identity, scene, view);
}
