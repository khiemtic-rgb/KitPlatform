namespace KitPlatform.Application.Success;

public static class ShiftChecklistShiftSelection
{
    public static Guid? Resolve(IReadOnlyList<Guid> openShiftIds, Guid? requested, bool required)
    {
        if (requested is Guid id)
        {
            if (!openShiftIds.Contains(id))
                throw new InvalidOperationException("Ca bán không đang mở tại chi nhánh này.");
            return id;
        }

        if (openShiftIds.Count == 1)
            return openShiftIds[0];

        if (required && openShiftIds.Count == 0)
            throw new InvalidOperationException("Mở ca bán trước khi làm checklist.");

        if (required && openShiftIds.Count > 1)
            throw new InvalidOperationException("Chi nhánh có nhiều ca đang mở. Chọn ca cụ thể.");

        return null;
    }
}
