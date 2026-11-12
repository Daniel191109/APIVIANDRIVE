using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ViandriveApp.Models
{
    public class ConductorDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public string Placa { get; set; }
        public string Modelo { get; set; }
        public string Color { get; set; }
        public int Anio { get; set; }
    }
}
