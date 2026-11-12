using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;

namespace ViandriveApp.Helpers
{
    public static class UbicacionHelper
    {
        public static readonly (double lat, double lng) CoordenadasIpiales = (0.8303, -77.6413);

        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<string> ObtenerDireccionDesdeCoordenadasAsync(double lat, double lng)
        {
            string direccion = $"Lat: {lat:0.000000}, Lng: {lng:0.000000}";

            try
            {
                if (lat == 0 || lng == 0)
                {
                    Console.WriteLine("[ERROR] Coordenadas inválidas. No se puede construir la URL.");
                    return direccion;
                }

                string apiKey = "AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw";
                string geocodeUrl = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}&language=es&key={apiKey}";

                Console.WriteLine($"[DEBUG] URL: {geocodeUrl}");

                var response = await httpClient.GetAsync(geocodeUrl);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ERROR] Código HTTP: {(int)response.StatusCode} - {response.ReasonPhrase}");
                    Console.WriteLine($"[ERROR] Cuerpo de error: {errorBody}");
                    return direccion;
                }

                var geocodeJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(geocodeJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                {
                    var direccionLegible = results[0].GetProperty("formatted_address").GetString();
                    if (!string.IsNullOrWhiteSpace(direccionLegible))
                        direccion = direccionLegible;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falló la geocodificación: {ex.Message}");
            }

            return direccion;
        }

        public static async Task<string> ObtenerDireccionOrigenAsync()
        {
            try
            {
                Console.WriteLine($"[UbicacionHelper] [INFO] Plataforma detectada: {DeviceInfo.Platform}");

                // 🖥️ En Windows, usar Ipiales fijo
                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    Console.WriteLine("[UbicacionHelper] [INFO] Plataforma Windows detectada. Usando coordenadas fijas de Ipiales.");
                    return await ObtenerDireccionDesdeCoordenadasAsync(CoordenadasIpiales.lat, CoordenadasIpiales.lng);
                }

                // 📱 En Android u otras plataformas móviles, usar ubicación real
                var geolocationRequest = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                var ubicacion = await Geolocation.GetLocationAsync(geolocationRequest);

                if (ubicacion != null)
                {
                    Console.WriteLine($"[UbicacionHelper] [DEBUG] Ubicación obtenida: lat={ubicacion.Latitude}, lng={ubicacion.Longitude}, precisión={ubicacion.Accuracy}");

                    const double precisionMinima = 100; // configurable según entorno

                    if (ubicacion.Accuracy <= precisionMinima && ubicacion.Latitude != 0 && ubicacion.Longitude != 0)
                    {
                        return await ObtenerDireccionDesdeCoordenadasAsync(ubicacion.Latitude, ubicacion.Longitude);
                    }

                    Console.WriteLine("[UbicacionHelper] [WARN] Precisión insuficiente o coordenadas inválidas. Usando respaldo.");
                }
                else
                {
                    Console.WriteLine("[UbicacionHelper] [WARN] No se pudo obtener ubicación actual.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UbicacionHelper] [ERROR] Falló la detección de ubicación: {ex.Message}");
            }

            // 🛑 Fallback defensivo
            Console.WriteLine("[UbicacionHelper] [INFO] Usando coordenadas fijas de Ipiales como respaldo.");
            return await ObtenerDireccionDesdeCoordenadasAsync(CoordenadasIpiales.lat, CoordenadasIpiales.lng);
        }

        public static async Task<Location?> ObtenerCoordenadasDesdeDireccionAsync(string direccion)
        {
            try
            {
                string apiKey = "AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw";
                string url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(direccion)}&key={apiKey}&language=es";

                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[ERROR] Código HTTP: {(int)response.StatusCode} - {response.ReasonPhrase}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                {
                    var location = results[0].GetProperty("geometry").GetProperty("location");
                    double lat = location.GetProperty("lat").GetDouble();
                    double lng = location.GetProperty("lng").GetDouble();

                    return new Location(lat, lng);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falló la geocodificación directa: {ex.Message}");
            }

            return null;
        }

        public static async Task<string> ObtenerDuracionDesdeGoogleAsync(double latOrigen, double lngOrigen, string destinoTexto)
        {
            try
            {
                string apiKey = "AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw";
                string destinoEncoded = Uri.EscapeDataString(destinoTexto);
                string url = $"https://maps.googleapis.com/maps/api/directions/json?origin={latOrigen.ToString(CultureInfo.InvariantCulture)},{lngOrigen.ToString(CultureInfo.InvariantCulture)}&destination={destinoEncoded}&key={apiKey}&language=es";

                var response = await httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("routes", out var routes) && routes.GetArrayLength() > 0)
                {
                    var leg = routes[0].GetProperty("legs")[0];
                    var duracion = leg.GetProperty("duration").GetProperty("text").GetString();
                    return duracion ?? "Duración no disponible";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falló la consulta de duración: {ex.Message}");
            }

            return "Duración no disponible";
        }
    }
}
