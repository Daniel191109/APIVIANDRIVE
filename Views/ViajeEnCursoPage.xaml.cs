using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ViandriveApp.Helpers;

namespace ViaDriveApp.Views
{
    public partial class ViajeEnCursoPage : ContentPage
    {
        private readonly int solicitudId;
        private decimal tarifa;
        private readonly HttpClient httpClient;
        private CancellationTokenSource? ctsMonitoreoRuta;
        private bool viajeActivo = true;

        // ID real del pasajero validado
        private int clienteIdActual = 0;
        private readonly int clienteIdRecibido;
        private int calificacionConductorSeleccionada = 5;

        public ViajeEnCursoPage(int solicitudId, string nombreConductor, string vehiculoInfo, decimal tarifaOriginal, int clienteId = 0)
        {
            InitializeComponent();
            this.solicitudId = solicitudId;
            this.tarifa = tarifaOriginal;
            clienteIdRecibido = clienteId;
            httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            NombreConductorLabel.Text = nombreConductor;
            VehiculoInfoLabel.Text = vehiculoInfo;
            TarifaLabel.Text = $"${tarifa:N0}";

            CargarMapaRutaEnTiempoReal();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 1. VALIDACIÓN ESTRICTA DE SESIÓN
            clienteIdActual = clienteIdRecibido > 0 ? clienteIdRecibido : Preferences.Get("ClienteId", 0);
            if (clienteIdActual == 0)
            {
                await DisplayAlert("Sesión Expirada", "Tu sesión no es válida. Inicia sesión nuevamente.", "OK");
                await Shell.Current.GoToAsync("//LoginPage");
                return;
            }

            viajeActivo = true;

            _ = ObtenerDatosRealesConductorAsync();
            _ = MonitorearEstadoViajeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            DetenerMonitoreo();
        }

        private void DetenerMonitoreo()
        {
            viajeActivo = false;
            if (ctsMonitoreoRuta != null && !ctsMonitoreoRuta.IsCancellationRequested)
            {
                ctsMonitoreoRuta.Cancel();
                ctsMonitoreoRuta.Dispose();
                ctsMonitoreoRuta = null;
            }
        }

        private async Task ObtenerDatosRealesConductorAsync()
        {
            try
            {
                string url = $"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}";
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (root.TryGetProperty("nombreConductor", out var nomProp) && !string.IsNullOrWhiteSpace(nomProp.GetString()))
                        {
                            NombreConductorLabel.Text = nomProp.GetString();
                        }
                        if (root.TryGetProperty("vehiculoInfo", out var vehProp) && !string.IsNullOrWhiteSpace(vehProp.GetString()))
                        {
                            VehiculoInfoLabel.Text = vehProp.GetString();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[VIANDRIVE ERROR DATOS REALES CONDUCTOR]: " + ex.Message);
            }
        }

        private async Task MonitorearEstadoViajeAsync()
        {
            DetenerMonitoreo();
            viajeActivo = true;
            ctsMonitoreoRuta = new CancellationTokenSource();
            var token = ctsMonitoreoRuta.Token;

            while (viajeActivo && !token.IsCancellationRequested)
            {
                try
                {
                    var response = await httpClient.GetAsync(
                        $"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}", token);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(token);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            if (TryGetPropertyInsensitive(root, "solicitud", out var innerSol)) root = innerSol;
                            else if (TryGetPropertyInsensitive(root, "data", out var innerData)) root = innerData;
                        }

                        string estado = TryGetPropertyInsensitive(root, "estado", out var estadoProp)
                            ? (estadoProp.GetString() ?? string.Empty) : string.Empty;

                        double latConductor = ExtraerCoordenada(root, "latConductor", "conductorLat", "latitudConductor");
                        double lngConductor = ExtraerCoordenada(root, "lngConductor", "conductorLng", "longitudConductor");
                        double latOrigen = ExtraerCoordenada(root, "latOrigen", "latitudOrigen", "origenLat");
                        double lngOrigen = ExtraerCoordenada(root, "lngOrigen", "longitudOrigen", "origenLng");
                        double latDestino = ExtraerCoordenada(root, "latDestino", "latitudDestino", "destinoLat");
                        double lngDestino = ExtraerCoordenada(root, "lngDestino", "longitudDestino", "destinoLng");

                        if (latConductor != 0 && lngConductor != 0 && latOrigen != 0 && lngOrigen != 0 &&
                            latDestino != 0 && lngDestino != 0)
                        {
                            await ActualizarRutaMapaJS(latConductor, lngConductor,
                                latOrigen, lngOrigen, latDestino, lngDestino, estado);
                        }

                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            if (LblEstadoViaje == null) return;
                            if (estado.Equals("EnCamino", StringComparison.OrdinalIgnoreCase) ||
                                estado.Equals("Aceptada", StringComparison.OrdinalIgnoreCase) ||
                                estado.Equals("Recogiendo", StringComparison.OrdinalIgnoreCase))
                                LblEstadoViaje.Text = "Tu conductor va en camino a recogerte";
                            else if (estado.Equals("EnViaje", StringComparison.OrdinalIgnoreCase) ||
                                     estado.Equals("EnRuta", StringComparison.OrdinalIgnoreCase) ||
                                     estado.Equals("EnCurso", StringComparison.OrdinalIgnoreCase))
                                LblEstadoViaje.Text = "¡Ya estás a bordo! Ruta hacia tu destino";
                        });

                        if (estado.Equals("Finalizada", StringComparison.OrdinalIgnoreCase) ||
                            estado.Equals("Finalizado", StringComparison.OrdinalIgnoreCase) ||
                            estado.Equals("Completado", StringComparison.OrdinalIgnoreCase))
                        {
                            DetenerMonitoreo();
                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                MapaRutaWeb.IsVisible = false;
                                OverlayCalificacionPasajero.IsVisible = true;
                            });
                            break;
                        }

                        if (estado.Equals("Cancelada", StringComparison.OrdinalIgnoreCase) ||
                            estado.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
                        {
                            string canceladaPor = TryGetPropertyInsensitive(root, "canceladaPor", out var quien)
                                ? (quien.GetString() ?? string.Empty) : string.Empty;
                            DetenerMonitoreo();
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                string quienCancelo = canceladaPor.StartsWith("conductor", StringComparison.OrdinalIgnoreCase)
                                    ? "El conductor canceló la carrera."
                                    : "La carrera fue cancelada.";
                                await DisplayAlert("Carrera cancelada", quienCancelo + " La solicitud quedó anulada para ambos participantes.", "OK");
                                await SalirAlMapaPrincipalAsync();
                            });
                            break;
                        }
                    }

                    await Task.Delay(2500, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[VIANDRIVE ERROR MONITOREO PASAJERO]: " + ex.Message);
                    try { await Task.Delay(2500, token); } catch (OperationCanceledException) { break; }
                }
            }
        }

        // --- MAPA Y JAVASCRIPT ---
        private void CargarMapaRutaEnTiempoReal()
        {
            const string apiKey = "AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw";
            string html = $@"
<!DOCTYPE html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<style>html,body,#map{{height:100%;width:100%;margin:0;padding:0;overflow:hidden}}</style>
<script>
let map, directionsService, directionsRenderer;
let markerDriver, markerPickup, markerDestination;
let routePhase = '', lastRouteAt = 0, firstFit = true;
function initMap() {{
  const fallback = {{lat:0.829,lng:-77.640}};
  map = new google.maps.Map(document.getElementById('map'), {{zoom:15,center:fallback,mapTypeId:'roadmap',disableDefaultUI:true,zoomControl:true}});
  directionsService = new google.maps.DirectionsService();
  directionsRenderer = new google.maps.DirectionsRenderer({{map:map,suppressMarkers:true,polylineOptions:{{strokeColor:'#00C853',strokeWeight:6,strokeOpacity:0.9}}}});
}}
function validPoint(lat,lng) {{ return Number.isFinite(lat) && Number.isFinite(lng) && Math.abs(lat)<=90 && Math.abs(lng)<=180 && !(lat===0 && lng===0); }}
function putMarker(oldMarker, position, title, label, color) {{
  if (!oldMarker) return new google.maps.Marker({{position,map,title,label,icon:{{path:google.maps.SymbolPath.CIRCLE,scale:label==='C'?9:7,fillColor:color,fillOpacity:1,strokeColor:'#fff',strokeWeight:2}}}});
  oldMarker.setPosition(position); return oldMarker;
}}
window.actualizarViaje = function(latD, lngD, latP, lngP, latF, lngF, estado) {{
  if (!map || !directionsService) {{ setTimeout(()=>window.actualizarViaje(latD,lngD,latP,lngP,latF,lngF,estado),500); return; }}
  const driver={{lat:Number(latD),lng:Number(lngD)}}, pickup={{lat:Number(latP),lng:Number(lngP)}}, finish={{lat:Number(latF),lng:Number(lngF)}};
  const driverOk=validPoint(driver.lat,driver.lng), pickupOk=validPoint(pickup.lat,pickup.lng), finishOk=validPoint(finish.lat,finish.lng);
  if (driverOk) markerDriver=putMarker(markerDriver,driver,'Ubicación del conductor','C','#007AFF');
  if (pickupOk) markerPickup=putMarker(markerPickup,pickup,'Punto de recogida','P','#FF9800');
  if (finishOk) markerDestination=putMarker(markerDestination,finish,'Destino','D','#00C853');
  const fase=String(estado||'').toLowerCase();
  const haciaRecogida=['encamino','aceptada','recogiendo'].includes(fase);
  const target = haciaRecogida ? pickup : finish;
  const targetOk = haciaRecogida ? pickupOk : finishOk;
  if (!driverOk || !targetOk) {{
    if (firstFit && pickupOk) {{ map.setCenter(pickup); map.setZoom(15); firstFit=false; }}
    return;
  }}
  const now=Date.now();
  if (firstFit || routePhase !== (haciaRecogida?'recogida':'destino') || now-lastRouteAt>12000) {{
    routePhase=haciaRecogida?'recogida':'destino'; lastRouteAt=now;
    directionsService.route({{origin:driver,destination:target,travelMode:google.maps.TravelMode.DRIVING}},(result,status)=>{{
      if(status==='OK') {{ directionsRenderer.setDirections(result); if(firstFit) {{map.fitBounds(result.routes[0].bounds); firstFit=false;}} }}
      else console.warn('No se pudo recalcular ruta: '+status);
    }});
  }}
}};
</script>
<script src='https://maps.googleapis.com/maps/api/js?key={apiKey}&callback=initMap' async defer></script>
</head><body><div id='map'></div></body></html>";

            MainThread.BeginInvokeOnMainThread(() =>
            {
                MapaRutaWeb.Source = new HtmlWebViewSource
                {
                    Html = html,
                    BaseUrl = DeviceInfo.Platform == DevicePlatform.Android ? "file:///android_asset/" : ""
                };
            });
        }

        private async Task ActualizarRutaMapaJS(double latConductor, double lngConductor,
            double latOrigen, double lngOrigen, double latDestino, double lngDestino, string estado)
        {
            string script = $"actualizarViaje({latConductor.ToString(CultureInfo.InvariantCulture)}," +
                $"{lngConductor.ToString(CultureInfo.InvariantCulture)},{latOrigen.ToString(CultureInfo.InvariantCulture)}," +
                $"{lngOrigen.ToString(CultureInfo.InvariantCulture)},{latDestino.ToString(CultureInfo.InvariantCulture)}," +
                $"{lngDestino.ToString(CultureInfo.InvariantCulture)},{JsonSerializer.Serialize(estado)});";
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try { if (MapaRutaWeb != null) await MapaRutaWeb.EvaluateJavaScriptAsync(script); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Error actualizando ruta del pasajero: " + ex.Message); }
            });
        }

        // --- UTILERÍAS JSON ---
        private double ExtraerCoordenada(JsonElement root, params string[] nombresPosibles)
        {
            foreach (var nombre in nombresPosibles)
            {
                if (TryGetPropertyInsensitive(root, nombre, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Number && val.TryGetDouble(out double d)) return d;
                    if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double ds)) return ds;
                }
            }
            return 0;
        }

        private bool TryGetPropertyInsensitive(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                string targetLimpio = propertyName.ToLowerInvariant().Replace("_", "");
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name.ToLowerInvariant().Replace("_", "") == targetLimpio)
                    {
                        value = prop.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }

        // --- MÉTODOS DE CALIFICACIÓN DEL PASAJERO AL CONDUCTOR ---
        private void EstrellaPasajero_Tapped(object? sender, TappedEventArgs e)
        {
            if (sender is Label lbl && int.TryParse(lbl.ClassId, out int rating))
            {
                calificacionConductorSeleccionada = rating;
                ActualizarUIEstrellasPasajero();
            }
        }

        private void ActualizarUIEstrellasPasajero()
        {
            Color colorSeleccionado = Color.FromArgb("#FFD700");
            Color colorInactivo = Color.FromArgb("#4A4C5A");

            if (StarPasajero1 != null) StarPasajero1.TextColor = calificacionConductorSeleccionada >= 1 ? colorSeleccionado : colorInactivo;
            if (StarPasajero2 != null) StarPasajero2.TextColor = calificacionConductorSeleccionada >= 2 ? colorSeleccionado : colorInactivo;
            if (StarPasajero3 != null) StarPasajero3.TextColor = calificacionConductorSeleccionada >= 3 ? colorSeleccionado : colorInactivo;
            if (StarPasajero4 != null) StarPasajero4.TextColor = calificacionConductorSeleccionada >= 4 ? colorSeleccionado : colorInactivo;
            if (StarPasajero5 != null) StarPasajero5.TextColor = calificacionConductorSeleccionada >= 5 ? colorSeleccionado : colorInactivo;
        }

        private async void EnviarCalificacionAlConductor_Clicked(object sender, EventArgs e)
        {
            try
            {
                string url = $"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}/calificar-conductor";

                // 2. AÑADIMOS EL ID DEL CLIENTE PARA VALIDAR QUIÉN CALIFICA
                var payload = new
                {
                    ClienteId = clienteIdActual,
                    Calificacion = calificacionConductorSeleccionada,
                    Comentario = ComentarioPasajeroEntry?.Text?.Trim() ?? "Excelente servicio"
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PutAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("¡Gracias!", "Tu calificación ha sido enviada al conductor.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al calificar conductor: " + ex.Message);
            }
            finally
            {
                await SalirAlMapaPrincipalAsync();
            }
        }

        private async Task SalirAlMapaPrincipalAsync()
        {
            DetenerMonitoreo();
            await Shell.Current.GoToAsync("/MapaPage");
        }

        // --- BOTONES DE ACCIÓN ---
        private async void BotonSos_Clicked(object sender, EventArgs e)
        {
            bool confirmarSos = await DisplayAlert("🚨 EMERGENCIA SOS", "¿Deseas contactar a las líneas de emergencia y reportar tu ubicación actual?", "LLAMAR 123", "Cancelar");
            if (confirmarSos)
            {
                await DisplayAlert("Alerta Enviada", "Se ha notificado a soporte y a las autoridades con los datos de tu viaje.", "OK");
            }
        }

        private async void BotonCancelarViaje_Clicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Cancelar Carrera", "¿Estás seguro de cancelar? El conductor recibirá la notificación.", "Sí, cancelar", "Volver");
            if (!confirmar) return;

            try
            {
                string url = $"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}/cancelar";
                var payload = new
                {
                    Rol = "cliente",
                    ClienteId = clienteIdActual,
                    Motivo = "Cancelado por el pasajero durante el viaje"
                };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var respuesta = await httpClient.PutAsync(url, content);
                string body = await respuesta.Content.ReadAsStringAsync();
                if (!respuesta.IsSuccessStatusCode)
                {
                    await DisplayAlert("No se pudo cancelar", ObtenerMensajeErrorApi(body) ??
                        $"El servidor respondió {(int)respuesta.StatusCode}. La carrera sigue activa.", "OK");
                    return;
                }

                DetenerMonitoreo();
                await DisplayAlert("Carrera cancelada", "La solicitud quedó cancelada en el servidor. El conductor verá la notificación.", "OK");
                await SalirAlMapaPrincipalAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error de conexión", "No se confirmó la cancelación en el servidor: " + ex.Message, "OK");
            }
        }

        private string? ObtenerMensajeErrorApi(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (TryGetPropertyInsensitive(doc.RootElement, "mensaje", out var mensaje)) return mensaje.GetString();
                if (TryGetPropertyInsensitive(doc.RootElement, "error", out var error)) return error.GetString();
            }
            catch { }
            return null;
        }

    }
}