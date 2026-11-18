using System.Data;
using System.Globalization;
using Dapper;

namespace ModMove.Infrastructure.Persistence;

/// <summary>
/// MySQL returns DATETIME columns with <see cref="DateTimeKind.Unspecified"/>; without this handler,
/// Dapper casts them to <see cref="DateTimeOffset"/> using the host's local offset (+07:00), shifting UTC times.
/// </summary>
public sealed class UtcDateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) => value switch
    {
        DateTimeOffset dto => dto.ToUniversalTime(),
        DateTime { Kind: DateTimeKind.Local } dt => new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero),
        DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
        string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        _ => throw new DataException($"Cannot convert {value.GetType().Name} to DateTimeOffset."),
    };

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.Value = value.UtcDateTime;
        parameter.DbType = DbType.DateTime;
    }
}

/// <summary>Nullable counterpart of <see cref="UtcDateTimeOffsetTypeHandler"/> for nullable UTC DATETIME columns.</summary>
public sealed class NullableUtcDateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset?>
{
    private static readonly UtcDateTimeOffsetTypeHandler Inner = new();

    public override DateTimeOffset? Parse(object value) =>
        value is null or DBNull ? null : Inner.Parse(value);

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value)
    {
        if (value is null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            Inner.SetValue(parameter, value.Value);
        }
    }
}

