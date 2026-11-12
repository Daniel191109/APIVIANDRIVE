using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ViandriveApp.Helpers;

namespace ViaDriveApp.Views
{
    public partial class OfertaEnRevisionPage : ContentPage
    {
        private readonly string origen;
        private readonly string destino;
        private readonly int solicitudId;
        private readonly HttpClient cliente;
        private CancellationTokenSource? ctsMonitoreo;
        private bool monitoreando = true;

        public OfertaEnRevisionPage(string origen, string destino, int solicitudId)
        {
            InitializeComponent();
            this.origen = origen;
            this.destino = destino;
            this.solicitudId = solicitudId;
            cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            OrigenLabel.Text = $"Origen: {origen}";
            DestinoLabel.Text = $"Destino: {destino}";

            ActualizarEstadoOferta("revision");
            CargarMapaConVehiculos(origen, destino);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            monitoreando = true;
            _ = MonitorearPostulacionesPasajeroAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            monitoreando = false;
            ctsMonitoreo?.Cancel();
        }

        private async Task MonitorearPostulacionesPasajeroAsync()
        {
            if (solicitudId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("[VIANDRIVE ERROR] El solicitudId es 0 o inválido al entrar a la página de revisión.");
                return;
            }

            ctsMonitoreo = new CancellationTokenSource();

            while (monitoreando && ctsMonitoreo != null && !ctsMonitoreo.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, ctsMonitoreo.Token);

                    string url = $"{ApiConfig.BaseUrl}/api/postulaciones/solicitud/{solicitudId}";
                    System.Diagnostics.Debug.WriteLine($"[VIANDRIVE] Consultando URL: {url}");

                    var response = await cliente.GetAsync(url, ctsMonitoreo.Token);
                    System.Diagnostics.Debug.WriteLine($"[VIANDRIVE] Código de respuesta API: {response.StatusCode}");

                    if (!response.IsSuccessStatusCode) continue;

                    var json = await response.Content.ReadAsStringAsync(ctsMonitoreo.Token);
                    System.Diagnostics.Debug.WriteLine($"[VIANDRIVE JSON RECIBIDO]: {json}");

                    using var doc = JsonDocument.Parse(json);
                    bool hayPostulaciones = false;

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        if (doc.RootElement.GetArrayLength() > 0) hayPostulaciones = true;
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("postulaciones", out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                        {
                            hayPostulaciones = true;
                        }
                    }

                    if (hayPostulaciones)
                    {
                        System.Diagnostics.Debug.WriteLine("[VIANDRIVE] ¡Postulación detectada con éxito! Cerrando vista...");
                        monitoreando = false;

                        // Solución definitiva utilizando MAUI Shell para evitar rutas ambiguas
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            try
                            {
                                // Intentamos regresar al mapa raíz de forma directa con Shell
                                if (Shell.Current != null)
                                {
                                    await Shell.Current.GoToAsync("///MapaPage");
                                }
                                else if (Navigation.NavigationStack.Count > 1)
                                {
                                    await Navigation.PopToRootAsync(false);
                                }
                            }
                            catch (Exception exNav)
                            {
                                System.Diagnostics.Debug.WriteLine("[VIANDRIVE ERROR NAVEGACIÓN SHELL]: " + exNav.Message);
                                // Plan de emergencia si falla Shell
                                try { await Navigation.PopAsync(false); } catch { }
                            }
                        });

                        break;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[VIANDRIVE EXCEPCIÓN MONITOREO] " + ex.Message);
                }
            }
        }

        private async void CargarMapaConVehiculos(string origen, string destino)
        {
            var origenCoords = await UbicacionHelper.ObtenerCoordenadasDesdeDireccionAsync(origen);
            var destinoCoords = await UbicacionHelper.ObtenerCoordenadasDesdeDireccionAsync(destino);

            if (origenCoords == null || destinoCoords == null) return;

            string latOrigen = origenCoords.Latitude.ToString("0.000000", CultureInfo.InvariantCulture);
            string lngOrigen = origenCoords.Longitude.ToString("0.000000", CultureInfo.InvariantCulture);
            string latDestino = destinoCoords.Latitude.ToString("0.000000", CultureInfo.InvariantCulture);
            string lngDestino = destinoCoords.Longitude.ToString("0.000000", CultureInfo.InvariantCulture);

            string apiKey = "AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw";

            string html = $@"
<!DOCTYPE html>
<html>
  <head>
    <meta charset='utf-8'>
    <title>Ruta Oferta</title>
    <style>
      #map {{ height: 100%; width: 100%; }}
      html, body {{ margin: 0; padding: 0; height: 100%; }}
    </style>
    <script src='https://maps.googleapis.com/maps/api/js?key={apiKey}&callback=initMap' async defer></script>
    <script>
      function initMap() {{
        const origen = {{ lat: {latOrigen}, lng: {lngOrigen} }};
        const destino = {{ lat: {latDestino}, lng: {lngDestino} }};
        const map = new google.maps.Map(document.getElementById('map'), {{ zoom: 14, center: origen, mapTypeId: 'roadmap' }});
        const directionsService = new google.maps.DirectionsService();
        const directionsRenderer = new google.maps.DirectionsRenderer({{ map: map }});
        directionsService.route({{ origin: origen, destination: destino, travelMode: google.maps.TravelMode.DRIVING }}, (result, status) => {{
          if (status === 'OK') {{ directionsRenderer.setDirections(result); }}
        }});
      }}
    </script>
  </head>
  <body><div id='map'></div></body>
</html>";

            string htmlPath = Path.Combine(FileSystem.AppDataDirectory, "mapa_oferta.html");
            File.WriteAllText(htmlPath, html);
            MapaWeb.Source = new UrlWebViewSource { Url = htmlPath };
        }

        public void ActualizarEstadoOferta(string estado)
        {
            if (estado == "revision")
            {
                EstadoBarra.Progress = 0.3;
                EstadoTexto.Text = "Revisión en curso...";
            }
        }

        private async void VolverMapa_Clicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}