using System.Data;
using Dapper;

namespace Login_Administracion_de_Modulos.Seguridad;

/// <summary>
/// Con Npgsql 10 las columnas DATE llegan como DateOnly; este handler enseña a
/// Dapper a materializarlas en propiedades DateTime (y DateTime?) de los modelos.
/// </summary>
public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value)
    {
        return value switch
        {
            DateOnly d => d.ToDateTime(TimeOnly.MinValue),
            DateTime dt => dt,
            _ => (DateTime)Convert.ChangeType(value, typeof(DateTime))
        };
    }

    public override void SetValue(IDbDataParameter parameter, DateTime value)
        => parameter.Value = value;
}

/// <summary>Variante para propiedades DateTime?.</summary>
public class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateTime?>
{
    public override DateTime? Parse(object value)
    {
        if (value is null or DBNull) return null;
        return value switch
        {
            DateOnly d => d.ToDateTime(TimeOnly.MinValue),
            DateTime dt => dt,
            _ => (DateTime)Convert.ChangeType(value, typeof(DateTime))
        };
    }

    public override void SetValue(IDbDataParameter parameter, DateTime? value)
        => parameter.Value = (object?)value ?? DBNull.Value;
}
