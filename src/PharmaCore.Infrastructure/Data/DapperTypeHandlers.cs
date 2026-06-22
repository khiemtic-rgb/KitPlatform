using System.Data;
using Dapper;

namespace PharmaCore.Infrastructure.Data;

internal static class DapperTypeHandlers
{
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered) return;
        _registered = true;
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value) => parameter.Value = value;

        public override DateOnly Parse(object value) => value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            _ => DateOnly.FromDateTime(Convert.ToDateTime(value)),
        };
    }

    private sealed class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly? value) =>
            parameter.Value = value.HasValue ? value.Value : DBNull.Value;

        public override DateOnly? Parse(object value) => value switch
        {
            null or DBNull => null,
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            _ => DateOnly.FromDateTime(Convert.ToDateTime(value)),
        };
    }
}
