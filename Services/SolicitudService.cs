using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ViandriveApp.Helpers;
using VianDriveApp.Models;

namespace VianDriveApp.Services
{
    public static class SolicitudService
    {
        private static readonly HttpClient httpClient = new()
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static async Task<List<SolicitudViajeDTO>> ObtenerSolicitudesPendientes(int conductorId)
        {
            const string endpoint = "api/solicitudes/pendientes";
            try
            {
                var solicitudes = await httpClient.GetFromJsonAsync<List<SolicitudViajeDTO>>(endpoint);
                return solicitudes ?? new List<SolicitudViajeDTO>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener solicitudes: {ex.Message}");
                return new List<SolicitudViajeDTO>();
            }
        }

        public static async Task<bool> AceptarSolicitud(int solicitudId, int conductorId)
        {
            if (solicitudId <= 0 || conductorId <= 0) return false;

            var endpoint = $"api/solicitudes/{solicitudId}/aceptar?conductorId={conductorId}";
            try
            {
                using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                using var response = await httpClient.PutAsync(endpoint, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al aceptar solicitud: {ex.Message}");
                return false;
            }
        }
    }
}
