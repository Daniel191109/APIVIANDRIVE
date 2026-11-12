namespace ViandriveApp.Models
{
    public class ConductorRegistroDto
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Document { get; set; }

        public string LicenseType { get; set; }
        public string LicenseNumber { get; set; }
        public string Plate { get; set; }
        public string Color { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }

        public string Password { get; set; }

        public string SelfieBase64 { get; set; }
        public string PropertyCardBase64 { get; set; }
        public string SoatBase64 { get; set; }
        public string VehiclePhotosBase64 { get; set; }


    }
}
