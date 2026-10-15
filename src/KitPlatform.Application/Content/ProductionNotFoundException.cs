namespace KitPlatform.Application.Content;

/// <summary>
/// One result for a missing production resource, another customer's resource, and a resource with no owner.
/// </summary>
public sealed class ProductionNotFoundException : Exception
{
    public const string FixedMessage = "Production không tồn tại.";

    public ProductionNotFoundException() : base(FixedMessage)
    {
    }
}
