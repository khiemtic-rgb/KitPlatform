using System.Text.Json;

namespace KitPlatform.Packs.PersonalIntelligence.Intent;

public static class BusinessIntentPrompt
{
    public static string Build(IReadOnlyList<CapabilityDescriptor> catalog, InterpretationContext context)
    {
        var catalogJson = JsonSerializer.Serialize(
            catalog.Select(item => new
            {
                item.Id,
                item.Domain,
                item.Summary,
                sideEffect = item.SideEffect.ToString(),
                item.TenantScoped,
                item.RefusesLive,
                permissions = item.Permissions,
                slots = item.Slots.Select(slot => new { slot.Name, slot.Required, slot.Description }),
            }),
            BusinessIntentJson.Serializer);

        return $"""
You map one business request onto the capability catalog. You do not execute anything.

Rules:
- Select capabilityId only by copying an id from the catalog. If none apply, bindings is empty.
- Do not invent capabilities, stored records, prices, quantities, approvals, or tenant ids.
- The caller tenant id is {context.TenantId:D}. Set requestedTenant only when the user names a different tenant code or id. Leave requestedTenant null when they mean the current tenant or name no tenant.
- Distinguish the goal, the completion conditions, the context you actually used, missing slots, and decisions a human must make.
- A human decision is required when the user wants a write, an approval, a payment, or a live external action. Do not supply an approver.
- Slot names and capability ids must match the catalog exactly. Decimal slot values use a dot. mode is paper or live, and only when the user stated which. Leave a slot empty instead of guessing.
- If two readings of the goal are plausible, set ambiguous to true and leave bindings empty.
- If the user does not name the object of the request, set ambiguous to true and leave bindings empty. Do not choose a catalog domain on their behalf. The goal must say that the object was not named.
- If no catalog capability performs the requested action, leave bindings empty and explain that in limit. Do not bind a different capability to approximate it.
- Permissions are enforced elsewhere. You may note a gap. Naming a gap does not create a capability.
- Return one JSON object with these keys only: goal, completionConditions, contextUsed, missingInformation, humanDecisions, bindings, ambiguous, ambiguityNote, limit, nextStep, requestedTenant.
- missingInformation items are objects with slot and why. humanDecisions items are objects with capabilityId and why. bindings items are objects with capabilityId, slots, and purpose. slots is an object whose values are strings.
- No markdown.

Catalog:
{catalogJson}
""";
    }
}
