namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Canonical shot production graph. Downstream work names upstream artifacts; never "latest take".
/// </summary>
public static class ShotDependencyGraph
{
    public static readonly IReadOnlyList<string> Nodes =
    [
        ShotNodeIds.Story, ShotNodeIds.Acting, ShotNodeIds.Keyframe, ShotNodeIds.Motion,
        ShotNodeIds.Voice, ShotNodeIds.Lipsync, ShotNodeIds.Mix, ShotNodeIds.Accepted, ShotNodeIds.Assembly,
    ];

    public static IReadOnlyList<string> DownstreamOf(string changedNode) => changedNode switch
    {
        ShotNodeIds.Story => [ShotNodeIds.Acting, ShotNodeIds.Motion, ShotNodeIds.Voice, ShotNodeIds.Lipsync, ShotNodeIds.Mix, ShotNodeIds.Accepted, ShotNodeIds.Assembly],
        ShotNodeIds.Acting => [ShotNodeIds.Motion, ShotNodeIds.Voice, ShotNodeIds.Lipsync, ShotNodeIds.Mix, ShotNodeIds.Accepted, ShotNodeIds.Assembly],
        ShotNodeIds.Keyframe => [ShotNodeIds.Motion, ShotNodeIds.Lipsync, ShotNodeIds.Mix, ShotNodeIds.Accepted, ShotNodeIds.Assembly],
        ShotNodeIds.Motion => [ShotNodeIds.Lipsync, ShotNodeIds.Mix, ShotNodeIds.Accepted, ShotNodeIds.Assembly],
        ShotNodeIds.Voice => [ShotNodeIds.Lipsync, ShotNodeIds.Mix, ShotNodeIds.Accepted, ShotNodeIds.Assembly],
        ShotNodeIds.Lipsync => [ShotNodeIds.Mix, ShotNodeIds.Accepted, ShotNodeIds.Assembly],
        ShotNodeIds.Mix => [ShotNodeIds.Accepted, ShotNodeIds.Assembly],
        ShotNodeIds.Accepted => [ShotNodeIds.Assembly],
        _ => [],
    };

    public static string ReasonFor(string changedNode) => changedNode switch
    {
        ShotNodeIds.Story => ProductionFailureReasons.StoryChanged,
        ShotNodeIds.Acting => ProductionFailureReasons.ActingChanged,
        ShotNodeIds.Keyframe => ProductionFailureReasons.KeyframeChanged,
        ShotNodeIds.Motion => ProductionFailureReasons.MotionChanged,
        ShotNodeIds.Voice => ProductionFailureReasons.VoiceChanged,
        _ => ProductionFailureReasons.BindingChanged,
    };

    public static string ArtifactRoleFor(string node) => node switch
    {
        ShotNodeIds.Keyframe => ProductionArtifactRoles.Keyframe,
        ShotNodeIds.Motion => ProductionArtifactRoles.VideoTake,
        ShotNodeIds.Voice => ProductionArtifactRoles.Voice,
        ShotNodeIds.Lipsync => ProductionArtifactRoles.Lipsync,
        ShotNodeIds.Mix => ProductionArtifactRoles.Mix,
        _ => "",
    };

    public static IReadOnlyList<string> BindingRolesInvalidatedBy(string changedNode)
    {
        var roles = new List<string>();
        foreach (var node in DownstreamOf(changedNode))
        {
            var role = ArtifactRoleFor(node);
            if (role.Length == 0) continue;
            roles.Add(ProductionBindingRoles.CurrentFor(role));
            roles.Add(ProductionBindingRoles.VisibleFor(role));
        }
        roles.Add(ProductionBindingRoles.Current);
        roles.Add(ProductionBindingRoles.Visible);
        if (changedNode is ShotNodeIds.Story or ShotNodeIds.Acting or ShotNodeIds.Keyframe or ShotNodeIds.Motion)
            roles.Add(ProductionBindingRoles.Accepted);
        return roles.Distinct().ToArray();
    }
}
