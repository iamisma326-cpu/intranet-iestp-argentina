using System.Data;
using Login_Administracion_de_Modulos.Seguridad;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Login_Administracion_de_Modulos.Areas.Modulo00;

/// <summary>
/// Factory de conexiones por módulo (equivalente del IModuleDbConnectionFactory del
/// proyecto original). Devuelve una NpgsqlConnection cuya search_path es
/// mod{XX},core,public: los servicios portados pueden seguir escribiendo tablas
/// SIN prefijo (ej. "FROM matriculas") y resuelven a su esquema + core.
/// Usa Npgsql 8 (misma versión que el proyecto original): las columnas "date"
/// llegan como DateTime, como esperan los DTOs portados.
/// </summary>
public interface IModuleDbConnectionFactory
{
    IDbConnection CreateConnection(string codigoModulo);
}

public class ModuleDbConnectionFactory : IModuleDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public ModuleDbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IDbConnection CreateConnection(string codigoModulo)
    {
        var cadena = ConfiguracionSegura.ObtenerCadenaModulo(_configuration, codigoModulo);
        return new NpgsqlConnection(cadena);
    }
}
