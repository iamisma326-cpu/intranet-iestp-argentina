namespace Login_Administracion_de_Modulos.Model
{
    public class Usuario
    {
        public int IdUsuario { get; set; }
        public int IdPersona { get; set; }
        public string? DniLogin { get; set; }
        public string? Correo { get; set; }
        public string? ClaveCifrada { get; set; }
        public string? Estado { get; set; }
    }
}
