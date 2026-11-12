using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ViandriveApp.Models
{
    public class SolicitudServicioDto
    {
        public int ClienteId { get; set; }
        public int ConductorId { get; set; } = 0;
        public int VehiculoId { get; set; } = 0;
        public string Origen { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;

        public double LatOrigen { get; set; }
        public double LngOrigen { get; set; }
        public double LatDestino { get; set; }
        public double LngDestino { get; set; }

        public double DistanciaKm { get; set; }
        public int TiempoEstimadoMin { get; set; }
        public bool HoraPico { get; set; }
        public decimal OfertaPasajero { get; set; }

        // 💡 Solución: Incluimos PrecioFinal para que se envíe en el JSON con el valor de la oferta
        public decimal PrecioFinal => OfertaPasajero > 0 ? OfertaPasajero : 5000m;

        public string MedioPago { get; set; } = string.Empty;
    }
}