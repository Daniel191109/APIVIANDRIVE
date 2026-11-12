namespace VianDriveApp.Models
{
    public class SolicitudViajeDTO
    {
        public int Id { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string Duracion { get; set; }
        public decimal Oferta { get; set; }
        public Command AceptarCommand { get; set; }
    }
}
