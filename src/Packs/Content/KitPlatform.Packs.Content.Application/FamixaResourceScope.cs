namespace KitPlatform.Packs.Content;

/// <summary>
/// Phase 1.5 split between inherited studio canon and customer production rows.
/// Shared canon is not given a customer column. Inheriting it does not expose another customer's production.
/// </summary>
public static class FamixaResourceScope
{
    public static readonly string[] SharedCanonTables =
    [
        "video_project",
        "video_universe",
        "video_visual_system",
        "video_character_dna",
        "video_master_reference",
    ];

    public static readonly string[] CustomerProductionTables =
    [
        "video_production",
        "video_production_shot",
        "video_image_generation_execution",
        "video_generation_execution",
    ];

    public static bool IsSharedCanon(string table) =>
        SharedCanonTables.Contains(table, StringComparer.Ordinal);

    public static bool IsCustomerProduction(string table) =>
        CustomerProductionTables.Contains(table, StringComparer.Ordinal);
}
