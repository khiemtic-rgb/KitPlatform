namespace KitPlatform.Packs.PersonalIntelligence.Intent;

public static class BusinessIntentPlanner
{
    static readonly IReadOnlyDictionary<string, string> EmptySlots = new Dictionary<string, string>();

    public static bool TargetsAnotherTenant(IntentInterpretation? interpretation, BusinessCaller caller)
    {
        var requested = interpretation?.RequestedTenant?.Trim();
        if (string.IsNullOrEmpty(requested))
            return false;
        return !Guid.TryParse(requested, out var tenantId) || tenantId != caller.TenantId;
    }

    public static bool HasPermission(CapabilityDescriptor capability, BusinessCaller caller)
    {
        if (caller.IsAdmin)
            return true;
        return capability.Permissions.Any(permission => caller.Permissions.Contains(permission));
    }

    public static bool MayExecute(
        PlanStep step,
        IntentInterpretation interpretation,
        IReadOnlyList<CapabilityDescriptor> catalog,
        BusinessCaller caller)
    {
        if (!step.RunHandler || step.CapabilityId is null)
            return false;
        if (interpretation.Ambiguous || TargetsAnotherTenant(interpretation, caller))
            return false;
        var capability = catalog.FirstOrDefault(item => item.Id == step.CapabilityId);
        if (capability is null || capability.SideEffect == SideEffectKind.Unknown)
            return false;
        if (!HasPermission(capability, caller))
            return false;
        foreach (var slot in capability.Slots.Where(item => item.Required))
        {
            if (!step.Slots.TryGetValue(slot.Name, out var value) || string.IsNullOrWhiteSpace(value))
                return false;
        }

        return true;
    }

    public static BusinessPlan Blocked(string understanding, string disposition, string reason) =>
        new(understanding, null, [new PlanStep(null, disposition, EmptySlots, [], reason, false)], [], []);

    public static BusinessPlan Plan(
        string understanding,
        IntentInterpretation interpretation,
        IReadOnlyList<CapabilityDescriptor> catalog,
        BusinessCaller caller)
    {
        var byId = catalog.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var tenantConflict = TargetsAnotherTenant(interpretation, caller);
        var steps = new List<PlanStep>();
        var rejected = new List<RejectedBinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var decisions = interpretation.HumanDecisions.ToList();

        foreach (var binding in interpretation.Bindings)
        {
            var capabilityId = binding.CapabilityId?.Trim() ?? "";
            if (capabilityId.Length == 0)
            {
                rejected.Add(new RejectedBinding("", "blank_capability"));
                continue;
            }

            if (!seen.Add(capabilityId))
            {
                rejected.Add(new RejectedBinding(capabilityId, "duplicate"));
                continue;
            }

            if (!byId.TryGetValue(capabilityId, out var capability))
            {
                rejected.Add(new RejectedBinding(capabilityId, IntentDisposition.NotInCatalog));
                steps.Add(Step(capabilityId, IntentDisposition.NotInCatalog, EmptySlots, [], "Capability is not in the catalog.", false));
                continue;
            }

            if (capability.SideEffect == SideEffectKind.Unknown)
            {
                steps.Add(Step(capabilityId, IntentDisposition.UnknownSideEffect, EmptySlots, [], "Side effect is unknown.", false));
                continue;
            }

            if (tenantConflict)
            {
                steps.Add(Step(capabilityId, IntentDisposition.TenantConflict, EmptySlots, [], "The request names a different tenant.", false));
                continue;
            }

            if (interpretation.Ambiguous)
            {
                steps.Add(Step(capabilityId, IntentDisposition.Ambiguous, EmptySlots, [], interpretation.AmbiguityNote ?? "The goal is ambiguous.", false));
                continue;
            }

            if (!HasPermission(capability, caller))
            {
                var required = string.Join(" or ", capability.Permissions);
                steps.Add(Step(capabilityId, IntentDisposition.PermissionDenied, EmptySlots, [], "Missing permission: " + required, false));
                continue;
            }

            var slots = FilterSlots(binding.Slots, capability);
            var missing = capability.Slots
                .Where(slot => slot.Required && (!slots.TryGetValue(slot.Name, out var value) || string.IsNullOrWhiteSpace(value)))
                .Select(slot => slot.Name)
                .ToArray();
            if (missing.Length > 0)
            {
                steps.Add(Step(capabilityId, IntentDisposition.MissingInformation, slots, missing, "Missing: " + string.Join(", ", missing), false));
                continue;
            }

            var disposition = capability.CanSatisfyCallerGoal ? IntentDisposition.Run : IntentDisposition.Evaluate;
            steps.Add(Step(capabilityId, disposition, slots, [], capability.CanSatisfyCallerGoal ? "Ready." : "Evaluation only. A caller goal that needs a write stays open.", true));
            if (!capability.CanSatisfyCallerGoal && decisions.All(item => item.CapabilityId != capabilityId))
            {
                decisions.Add(new HumanDecision(
                    capabilityId,
                    "Ghi nhận khớp lệnh giấy cần phê duyệt riêng trên một decision đã tồn tại. Câu nói không phải phê duyệt."));
            }
        }

        if (steps.Count == 0)
        {
            if (tenantConflict)
                steps.Add(Step(null, IntentDisposition.TenantConflict, EmptySlots, [], "The request names a different tenant.", false));
            else if (interpretation.Ambiguous)
                steps.Add(Step(null, IntentDisposition.Ambiguous, EmptySlots, [], interpretation.AmbiguityNote ?? "The goal is ambiguous.", false));
            else
                steps.Add(Step(null, IntentDisposition.NoCapability, EmptySlots, [], "No catalog capability was selected.", false));
        }

        return new BusinessPlan(understanding, interpretation, steps, rejected, decisions);
    }

    static PlanStep Step(
        string? capabilityId,
        string disposition,
        IReadOnlyDictionary<string, string> slots,
        IReadOnlyList<string> missing,
        string reason,
        bool run) =>
        new(capabilityId, disposition, slots, missing, reason, run);

    static IReadOnlyDictionary<string, string> FilterSlots(
        IReadOnlyDictionary<string, string>? slots,
        CapabilityDescriptor capability)
    {
        var allowed = capability.Slots.Select(slot => slot.Name).ToHashSet(StringComparer.Ordinal);
        var filtered = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (slots is null)
            return filtered;
        foreach (var pair in slots)
        {
            if (!allowed.Contains(pair.Key))
                continue;
            var value = pair.Value?.Trim() ?? "";
            if (value.Length == 0)
                continue;
            filtered[pair.Key] = value;
        }

        return filtered;
    }
}
