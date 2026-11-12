using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ViandriveApp.Helpers;

namespace ViandriveApp.Views
{
    // Modelo auxiliar declarado aquí mismo para garantizar visibilidad absoluta y cero errores de espacio de nombres
    public class SolicitudViajeDto
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string NombrePasajero { get; set; } = string.Empty;
        public string Origen { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public decimal Tarifa { get; set; }
        public decimal Oferta { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public string Duracion { get; set; } = string.Empty;
        public double LatOrigen { get; set; }
        public double LngOrigen { get; set; }
        public double LatDestino { get; set; }
        public double LngDestino { get; set; }
        public ICommand? AceptarCommand { get; set; }
    }

    public partial class ModoConductorInicioPage : ContentPage
    {
        private static readonly HttpClient ApiHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
        private bool actualizandoEnTiempoReal = false;
        private CancellationTokenSource? ctsGpsFondo;
        private double latActual = 0.829;
        private double lngActual = -77.640;

        private ObservableCollection<SolicitudViajeDto> _solicitudes;
        public ObservableCollection<SolicitudViajeDto> Solicitudes
        {
            get => _solicitudes;
            set
            {
                _solicitudes = value;
                OnPropertyChanged();
            }
        }

        public ModoConductorInicioPage()
        {
            InitializeComponent();
            Solicitudes = new ObservableCollection<SolicitudViajeDto>();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            bool permiso = await PedirPermisoUbicacionAsync();
            if (permiso)
            {
                await ObtenerUbicacionGpsAsync();
            }

            CargarMapaFondoHtml();

            actualizandoEnTiempoReal = true;
            IniciarActualizacionAutomatica();
            IniciarBucleGpsFondo();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            actualizandoEnTiempoReal = false;
            ctsGpsFondo?.Cancel();
        }

        private async Task<bool> PedirPermisoUbicacionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }
            return status == PermissionStatus.Granted;
        }

        private async Task ObtenerUbicacionGpsAsync()
        {
            try
            {
                var loc = await Geolocation.GetLastKnownLocationAsync() ?? await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
                if (loc != null)
                {
                    latActual = loc.Latitude;
                    lngActual = loc.Longitude;
                }
            }
            catch { }
        }

        private void CargarMapaFondoHtml()
        {
            string latStr = latActual.ToString(CultureInfo.InvariantCulture);
            string lngStr = lngActual.ToString(CultureInfo.InvariantCulture);
            string apiKey = "AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw";

            string htmlContent = $@"
<!DOCTYPE html>
<html>
  <head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
      html, body, #map {{
        height: 100%;
        width: 100%;
        margin: 0;
        padding: 0;
      }}
    </style>
    <script src='https://maps.googleapis.com/maps/api/js?key={apiKey}&callback=initMap' async defer></script>
    <script>
      let map;
      let marker;
      let ultimaLat = {latStr};
      let ultimaLng = {lngStr};

      function initMap() {{
        const pos = {{ lat: ultimaLat, lng: ultimaLng }};
        map = new google.maps.Map(document.getElementById('map'), {{
          zoom: 16,
          center: pos,
          disableDefaultUI: true,
          zoomControl: false
        }});

        marker = new google.maps.Marker({{
          position: pos,
          map: map,
          title: 'Mi Vehículo',
          icon: {{
            url: 'https://cdn-icons-png.flaticon.com/512/741/741407.png',
            scaledSize: new google.maps.Size(44, 44),
            anchor: new google.maps.Point(22, 22)
          }}
        }});
      }}

      function actualizarUbicacion(lat, lng) {{
        if (marker && map) {{
          const nuevaPos = {{ lat: lat, lng: lng }};
          marker.setPosition(nuevaPos);
          map.setCenter(nuevaPos);
          ultimaLat = lat;
          ultimaLng = lng;
        }}
      }}
    </script>
  </head>
  <body>
    <div id='map'></div>
  </body>
</html>";

            MapaFondoWeb.Source = new HtmlWebViewSource
            {
                Html = htmlContent
            };
        }

        private void IniciarBucleGpsFondo()
        {
            ctsGpsFondo = new CancellationTokenSource();
            Task.Run(async () =>
            {
                while (ctsGpsFondo != null && !ctsGpsFondo.Token.IsCancellationRequested)
                {
                    try
                    {
                        var loc = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(3)), ctsGpsFondo.Token);
                        if (loc != null)
                        {
                            latActual = loc.Latitude;
                            lngActual = loc.Longitude;
                            string latStr = latActual.ToString(CultureInfo.InvariantCulture);
                            string lngStr = lngActual.ToString(CultureInfo.InvariantCulture);

                            string script = $"actualizarUbicacion({latStr}, {lngStr});";
                            await MainThread.InvokeOnMainThreadAsync(async () => {
                                await MapaFondoWeb.EvaluateJavaScriptAsync(script);
                            });

                            // Publica la ubicación para que los pasajeros puedan ver conductores disponibles.
                            int conductorId = Preferences.Get("ConductorId", 0);
                            if (conductorId > 0)
                            {
                                var payload = new
                                {
                                    ConductorId = conductorId,
                                    Latitud = latActual,
                                    Longitud = lngActual
                                };

                                try
                                {
                                    using var response = await ApiHttp.PostAsJsonAsync(
                                        ApiConfig.BuildUrl("api/conductores/actualizar-ubicacion"),
                                        payload,
                                        ctsGpsFondo.Token);
                                    if (!response.IsSuccessStatusCode)
                                    {
                                        System.Diagnostics.Debug.WriteLine(
                                            $"No se pudo publicar la ubicación del conductor: {(int)response.StatusCode}");
                                    }
                                }
                                catch (OperationCanceledException) { throw; }
                                catch (Exception apiEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error publicando ubicación: {apiEx.Message}");
                                }
                            }
                        }
                    }
                    catch { }
                    await Task.Delay(4000, ctsGpsFondo.Token);
                }
            }, ctsGpsFondo.Token);
        }

        private void IniciarActualizacionAutomatica()
        {
            Task.Run(async () =>
            {
                while (actualizandoEnTiempoReal)
                {
                    try
                    {
                        bool radarActivo = false;
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            radarActivo = SwitchRadar != null && SwitchRadar.IsToggled;
                        });

                        if (radarActivo)
                        {
                            await CargarViajesDesdeApiAsync();
                        }
                        else
                        {
                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                Solicitudes.Clear();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error en bucle de solicitudes: {ex.Message}");
                    }

                    await Task.Delay(3000);
                }
            });
        }

        public async Task CargarViajesDesdeApiAsync()
        {
            var viajes = await ObtenerViajesPendientesAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Solicitudes.Clear();
                foreach (var viaje in viajes)
                {
                    Solicitudes.Add(viaje);
                }
            });
        }

        private async Task<List<SolicitudViajeDto>> ObtenerViajesPendientesAsync()
        {
            try
            {
                using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                string url = $"{ApiConfig.BaseUrl}/api/solicitudes/pendientes";

                var respuesta = await cliente.GetAsync(url);
                if (!respuesta.IsSuccessStatusCode) return new List<SolicitudViajeDto>();

                var contenidoJson = await respuesta.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(contenidoJson);
                var listaResultado = new List<SolicitudViajeDto>();

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        var dto = new SolicitudViajeDto();

                        dto.Id = ObtenerIntFlexible(elem, "id", "solicitudId");

                        string nombre = ObtenerTextoFlexible(elem, "nombrePasajero", "usuario", "nombreCliente", "clienteNombre", "nombre") ?? "Pasajero";
                        if (TryGetPropertyInsensitive(elem, "cliente", out var cliObj) ||
                            TryGetPropertyInsensitive(elem, "pasajero", out cliObj) ||
                            TryGetPropertyInsensitive(elem, "usuario", out cliObj))
                        {
                            nombre = ObtenerTextoFlexible(cliObj, "nombre", "nombreCompleto", "nombres") ?? nombre;
                        }

                        dto.NombrePasajero = nombre;

                        dto.Origen = ObtenerTextoFlexible(elem, "origen", "origenTexto", "direccionOrigen") ?? "Ubicación actual";
                        dto.Destino = ObtenerTextoFlexible(elem, "destino", "destinoTexto", "direccionDestino") ?? "Destino solicitado";

                        decimal montoOferta = ObtenerDecimalFlexible(elem, "ofertaPasajero", "oferta", "tarifaOfrecida", "precio", "monto");
                        dto.Oferta = montoOferta > 0 ? montoOferta : ObtenerDecimalFlexible(elem, "tarifa");
                        dto.Tarifa = dto.Oferta;

                        int idCapturado = dto.Id;
                        dto.AceptarCommand = new Command(() => OnAceptarViaje(idCapturado));

                        listaResultado.Add(dto);
                    }
                }

                return listaResultado;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo pendientes: {ex.Message}");
                return new List<SolicitudViajeDto>();
            }
        }

        private async void OnAceptarViaje(int viajeId)
        {
            actualizandoEnTiempoReal = false;
            await Navigation.PushAsync(new MapaOfertaPage(viajeId));
        }

        private async void OnCerrarSesionClicked(object sender, EventArgs e)
        {
            Preferences.Set("ConductorSesion", false);
            Preferences.Clear();
            await Shell.Current.GoToAsync("//LoginConductorPage");
        }

        // --- MÉTODOS AUXILIARES DE EXTRACCIÓN FLEXIBLE DE JSON ---
        private bool TryGetPropertyInsensitive(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                string target = propertyName.ToLowerInvariant().Replace("_", "");
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name.ToLowerInvariant().Replace("_", "") == target)
                    {
                        value = prop.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }

        private string? ObtenerTextoFlexible(JsonElement root, params string[] nombresPosibles)
        {
            foreach (var n in nombresPosibles)
            {
                if (TryGetPropertyInsensitive(root, n, out var val) && val.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(val.GetString()))
                    return val.GetString();
            }
            return null;
        }

        private int ObtenerIntFlexible(JsonElement root, params string[] nombresPosibles)
        {
            foreach (var n in nombresPosibles)
            {
                if (TryGetPropertyInsensitive(root, n, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out int i)) return i;
                    if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out int ps)) return ps;
                }
            }
            return 0;
        }

        private decimal ObtenerDecimalFlexible(JsonElement root, params string[] nombresPosibles)
        {
            foreach (var n in nombresPosibles)
            {
                if (TryGetPropertyInsensitive(root, n, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Number && val.TryGetDecimal(out decimal d)) return d;
                    if (val.ValueKind == JsonValueKind.String && decimal.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pd)) return pd;
                }
            }
            return 0;
        }
    }
}