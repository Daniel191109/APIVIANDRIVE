namespace ViandriveApp.Models
{
    public class ClienteRegistroDto
    {
        public string NombreCompleto { get; set; }
        public string Correo { get; set; }
        public string Clave { get; set; }
        public string Celular { get; set; }
        public string MetodoPagoPreferido { get; set; }
        public string EnviarConfirmacionPor { get; set; } // "Correo" o "SMS"
    }
}
