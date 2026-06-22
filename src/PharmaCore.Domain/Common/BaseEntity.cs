namespace PharmaCore.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}

public static class EntityStatus
{
    public const short Active = 1;
    public const short Inactive = 2;
}

public static class DrugType
{
    public const short Otc = 1;
    public const short Prescription = 2;
    public const short Controlled = 3;
}

public static class MovementType
{
    public const short Import = 1;
    public const short Sale = 2;
    public const short CustomerReturn = 3;
    public const short SupplierReturn = 4;
    public const short TransferOut = 5;
    public const short TransferIn = 6;
    public const short AdjustmentPlus = 7;
    public const short AdjustmentMinus = 8;
    public const short Expired = 9;
    public const short Damaged = 10;
}
