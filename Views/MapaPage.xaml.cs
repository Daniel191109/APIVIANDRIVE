using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ViaDriveApp.Views;
using ViandriveApp.Helpers;
using ViandriveApp.Models;

namespace ViandriveApp.Views
{
    public class PostulacionConductorItem
    {
        public int Id { get; set; }
        public int ConductorId { get; set; }
        public string NombreConductor { get; set; } = string.Empty;
        public string VehiculoInfo { get; set; } = string.Empty;
        public string CalificacionTexto { get; set; } = string.Empty;
        public decimal Tarifa { get; set; }
        public string TarifaTexto => $"${Tarifa:N0}";
    }

    // Decoradores para recibir los parámetros desde LoginPage
    [QueryProperty(nameof(ClienteIdParam), "ClienteId")]
    [QueryProperty(nameof(NombreParam), "Nombre")]
    public partial class MapaPage : ContentPage
    {
        private double destinoLat;
        private double destinoLng;
        private string? destinoTitulo;

        private int solicitudActivaId = 0;
        private bool monitoreandoSolicitud = false;
        private bool monitoreandoVehiculos = false;
        private int calificacionConductorSeleccionada = 5;
        private int clienteIdActual = 0;
        private double latPasajeroActual = 0;
        private double lngPasajeroActual = 0;

        private CancellationTokenSource? ctsVehiculos;
        private CancellationTokenSource? ctsSolicitud;

        private readonly HttpClient httpClient;
        private TaskCompletionSource<PostulacionConductorItem?>? tcsSeleccionConductor;

        // =========================================================
        // PROPIEDADES DE NAVEGACIÓN
        // =========================================================
        private int _clienteIdParam;
        public int ClienteIdParam
        {
            get => _clienteIdParam;
            set
            {
                _clienteIdParam = value;
                clienteIdActual = value; // Asignamos el ID directamente a la variable operativa
            }
        }

        private string _nombreParam = string.Empty;
        public string NombreParam
        {
            get => _nombreParam;
            set
            {
                _nombreParam = value;
            }
        }
        // =========================================================

        public MapaPage()
        {
            InitializeComponent();
            httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            DestinoEntry.TextChanged += async (sender, e) =>
            {
                var texto = e.NewTextValue;
                if (string.IsNullOrWhiteSpace(texto) || texto.Length < 3) return;
                await BuscarAutocompletadoDestino(texto);
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Si el ClienteIdParam no se asignó por navegación, intentamos recuperarlo de Preferences
            if (clienteIdActual == 0)
            {
                clienteIdActual = Preferences.Get("ClienteId", 0);
            }

            if (clienteIdActual == 0)
            {
                await DisplayAlert("Sesión Expirada", "Por favor, inicia sesión para continuar.", "OK");
                await Shell.Current.GoToAsync("//LoginPage");
                return;
            }

            _ = InicializarUbicacionConAnuncioAsync();
            IniciarMonitoreoVehiculosActivos();

            if (solicitudActivaId > 0 && !monitoreandoSolicitud)
            {
                _ = MonitorearPostulacionesAsync();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            DetenerTodosLosMonitoreos();
        }

        // =========================================================
        // NAVEGACIÓN AL PERFIL DEL USUARIO
        // =========================================================
        private async void BtnIrUsuario_Clicked(object sender, EventArgs e)
        {
            try
            {
                // Navega a la ruta de la página del usuario usando una ruta relativa limpia
                await Shell.Current.GoToAsync("Usuario");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al navegar a Usuario: {ex.Message}");
                await DisplayAlert("Error", "No se pudo abrir el perfil del usuario.", "OK");
            }
        }

        private void DetenerTodosLosMonitoreos()
        {
            monitoreandoVehiculos = false;
            monitoreandoSolicitud = false;

            if (ctsVehiculos != null && !ctsVehiculos.IsCancellationRequested)
            {
                ctsVehiculos.Cancel();
                ctsVehiculos.Dispose();
                ctsVehiculos = null;
            }

            if (ctsSolicitud != null && !ctsSolicitud.IsCancellationRequested)
            {
                ctsSolicitud.Cancel();
                ctsSolicitud.Dispose();
                ctsSolicitud = null;
            }
        }

        private async Task BuscarAutocompletadoDestino(string texto)
        {
            var autocompleteUrl = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={Uri.EscapeDataString(texto)}&key=AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw&components=country:co";
            try
            {
                var autocompleteJson = await httpClient.GetStringAsync(autocompleteUrl);
                using var resultado = JsonDocument.Parse(autocompleteJson);

                if (resultado.RootElement.TryGetProperty("predictions", out var predictions))
                {
                    var sugerencias = predictions.EnumerateArray()
                        .Select(p => p.TryGetProperty("description", out var desc) ? desc.GetString() : null)
                        .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

                    SugerenciasListView.ItemsSource = sugerencias;
                    SugerenciasListView.IsVisible = sugerencias.Any();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error autocomplete: " + ex.Message);
            }
        }

        private async void SugerenciasListView_ItemSelected(object? sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is string direccionSeleccionada && !string.IsNullOrWhiteSpace(direccionSeleccionada))
            {
                DestinoEntry.Text = direccionSeleccionada;
                SugerenciasListView.IsVisible = false;
                SugerenciasListView.SelectedItem = null;
                await CalcularTarifaSugerida(direccionSeleccionada);
            }
        }

        private async Task<Location?> ObtenerUbicacionRealDispositivoConAnuncioAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    if (status != PermissionStatus.Granted)
                    {
                        await DisplayAlert("⚠️ Permiso Denegado", "Se requieren permisos de ubicación para buscar transporte.", "OK");
                        return null;
                    }
                }
                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(6));
                return await Geolocation.GetLocationAsync(request) ?? await Geolocation.GetLastKnownLocationAsync();
            }
            catch (Exception)
            {
                return new Location(0.829, -77.640);
            }
        }

        private void OfertaEntry_TextChanged(object? sender, TextChangedEventArgs e)
        {
            var limpio = e.NewTextValue?.Replace(".", "").Replace(",", "").Trim();
            if (decimal.TryParse(limpio, out decimal valor) && valor > 0)
            {
                OfertaEntry.TextChanged -= OfertaEntry_TextChanged;
                OfertaEntry.Text = valor.ToString("N0", CultureInfo.InvariantCulture);
                OfertaEntry.CursorPosition = OfertaEntry.Text.Length;
                OfertaEntry.TextChanged += OfertaEntry_TextChanged;
            }
        }

        private async Task InicializarUbicacionConAnuncioAsync()
        {
            var ubicacion = await ObtenerUbicacionRealDispositivoConAnuncioAsync();
            if (ubicacion == null) return;

            latPasajeroActual = ubicacion.Latitude;
            lngPasajeroActual = ubicacion.Longitude;

            string htmlFinal = await CargarHtmlMapa("ubicacion.html");
            htmlFinal = htmlFinal
                .Replace("{{origenLat}}", ubicacion.Latitude.ToString(CultureInfo.InvariantCulture))
                .Replace("{{origenLng}}", ubicacion.Longitude.ToString(CultureInfo.InvariantCulture))
                .Replace("{{origenTitulo}}", "Mi Ubicación");

            EjecutarHtmlEnMapa(htmlFinal);
        }

        private async Task CalcularTarifaSugerida(string destinoTexto)
        {
            try
            {
                var urlAutocomplete = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={Uri.EscapeDataString(destinoTexto)}&key=AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw&components=country:co";
                var jsonAutocomplete = await httpClient.GetStringAsync(urlAutocomplete);
                using var resultado = JsonDocument.Parse(jsonAutocomplete);

                if (!resultado.RootElement.TryGetProperty("predictions", out var predictions) || predictions.GetArrayLength() == 0) return;

                if (!predictions[0].TryGetProperty("place_id", out var placeIdProp)) return;
                string? placeId = placeIdProp.GetString();
                if (string.IsNullOrEmpty(placeId)) return;

                var urlDetalles = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeId}&key=AIzaSyAf_F2FMOZdm53A04qlb5U9MlyH4M-YOvw";
                var jsonDetalles = await httpClient.GetStringAsync(urlDetalles);
                using var detalles = JsonDocument.Parse(jsonDetalles);

                var location = detalles.RootElement.GetProperty("result").GetProperty("geometry").GetProperty("location");
                destinoLat = location.GetProperty("lat").GetDouble();
                destinoLng = location.GetProperty("lng").GetDouble();
                destinoTitulo = destinoTexto;

                var ubicacion = await ObtenerUbicacionRealDispositivoConAnuncioAsync();
                if (ubicacion == null) return;
                latPasajeroActual = ubicacion.Latitude;
                lngPasajeroActual = ubicacion.Longitude;

                OfertaEntry.Text = 7000m.ToString("N0", CultureInfo.InvariantCulture);
                await MostrarMapaConMarcadoresAsync(ubicacion.Latitude, ubicacion.Longitude, destinoLat, destinoLng);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al procesar destino: " + ex.Message);
            }
        }

        private async Task MostrarMapaConMarcadoresAsync(double origenLat, double origenLng, double destLat, double destLng)
        {
            latPasajeroActual = origenLat;
            lngPasajeroActual = origenLng;
            string htmlFinal = await CargarHtmlMapa("ruta.html");
            htmlFinal = htmlFinal
                .Replace("{{origenLat}}", origenLat.ToString(CultureInfo.InvariantCulture))
                .Replace("{{origenLng}}", origenLng.ToString(CultureInfo.InvariantCulture))
                .Replace("{{destinoLat}}", destLat.ToString(CultureInfo.InvariantCulture))
                .Replace("{{destinoLng}}", destLng.ToString(CultureInfo.InvariantCulture))
                .Replace("{{origenTitulo}}", "Origen")
                .Replace("{{destinoTitulo}}", destinoTitulo ?? "Destino");

            EjecutarHtmlEnMapa(htmlFinal);
        }

        private async Task<string> CargarHtmlMapa(string archivo)
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(archivo);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        private void EjecutarHtmlEnMapa(string html)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MapaWeb.Source = new HtmlWebViewSource
                {
                    Html = html,
                    BaseUrl = DeviceInfo.Platform == DevicePlatform.Android ? "file:///android_asset/" : ""
                };
            });
        }

        private void IniciarMonitoreoVehiculosActivos()
        {
            if (monitoreandoVehiculos) return;
            monitoreandoVehiculos = true;

            ctsVehiculos?.Cancel();
            ctsVehiculos = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (monitoreandoVehiculos && !ctsVehiculos.Token.IsCancellationRequested)
                {
                    try
                    {
                        var urlActivos = $"{ApiConfig.BaseUrl}/api/conductores/activos?radioKm=25";
                        if (latPasajeroActual != 0 && lngPasajeroActual != 0)
                            urlActivos += $"&lat={latPasajeroActual.ToString(CultureInfo.InvariantCulture)}&lng={lngPasajeroActual.ToString(CultureInfo.InvariantCulture)}";
                        var response = await httpClient.GetAsync(urlActivos, ctsVehiculos.Token);
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync(ctsVehiculos.Token);

                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                try
                                {
                                    if (MapaWeb != null && !string.IsNullOrWhiteSpace(json))
                                    {
                                        // En Windows (WebView2), nos aseguramos de que esté inicializado antes de evaluar JS
#if WINDOWS
                                if (MapaWeb.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView2 && webView2.CoreWebView2 == null)
                                {
                                    await webView2.EnsureCoreWebView2Async();
                                }
#endif
                                        await MapaWeb.EvaluateJavaScriptAsync($"try {{ if (typeof actualizarVehiculosActivos === 'function') {{ actualizarVehiculosActivos({json}); }} }} catch(e) {{}}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Evita que un fallo puntual de WebView crashee el bucle de monitoreo
                                    System.Diagnostics.Debug.WriteLine($"Error actualizando mapa: {ex.Message}");
                                }
                            });
                        }
                        await Task.Delay(5000, ctsVehiculos.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Salida normal por cancelación del token
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error en ciclo de monitoreo: {ex.Message}");
                        await Task.Delay(5000, ctsVehiculos.Token); // Espera antes de reintentar si falló el HTTP
                    }
                }
            }, ctsVehiculos.Token);
        }

        private async void SolicitarViajeDesdeMapa_Clicked(object sender, EventArgs e)
        {
            if (clienteIdActual == 0)
            {
                await DisplayAlert("Error", "Sesión inválida. Inicia sesión nuevamente.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(DestinoEntry.Text))
            {
                await DisplayAlert("Falta destino", "Ingresa hacia donde te diriges.", "OK");
                return;
            }

            var ubicacion = await ObtenerUbicacionRealDispositivoConAnuncioAsync();
            if (ubicacion == null) return;
            latPasajeroActual = ubicacion.Latitude;
            lngPasajeroActual = ubicacion.Longitude;

            var limpio = OfertaEntry.Text?.Replace(".", "").Replace(",", "").Trim();
            if (!decimal.TryParse(limpio, out decimal oferta) || oferta < 5000)
            {
                await DisplayAlert("Monto muy bajo", "La oferta mínima es de $5.000.", "OK");
                return;
            }

            if (MedioPagoPicker?.SelectedItem == null)
            {
                await DisplayAlert("Método de pago", "Selecciona cómo vas a pagar.", "OK");
                return;
            }

            string origenDireccion = await UbicacionHelper.ObtenerDireccionDesdeCoordenadasAsync(ubicacion.Latitude, ubicacion.Longitude) ?? "Ubicación actual";
            string destinoDireccion = string.IsNullOrWhiteSpace(destinoTitulo) ? DestinoEntry.Text.Trim() : destinoTitulo;
            string medioPagoSeleccionado = MedioPagoPicker.SelectedItem.ToString() ?? "Efectivo";

            var solicitudDto = new SolicitudServicioDto
            {
                ClienteId = clienteIdActual,
                Origen = origenDireccion,
                Destino = destinoDireccion,
                LatOrigen = ubicacion.Latitude,
                LngOrigen = ubicacion.Longitude,
                LatDestino = destinoLat,
                LngDestino = destinoLng,
                DistanciaKm = 3.5,
                TiempoEstimadoMin = 12,
                HoraPico = false,
                OfertaPasajero = oferta,
                MedioPago = medioPagoSeleccionado
            };

            int idCreado = await EnviarSolicitudAlBackend(solicitudDto);

            if (idCreado > 0)
            {
                solicitudActivaId = idCreado;

                // CORREGIDO: Ya no se abre ConfirmarViajePage de golpe. 
                // Se inicia el monitoreo para que el mapa escuche las ofertas de los conductores automáticamente.
                _ = MonitorearPostulacionesAsync();
                
            }
        }

        private async Task<int> EnviarSolicitudAlBackend(SolicitudServicioDto solicitud)
        {
            try
            {
                var json = JsonSerializer.Serialize(solicitud);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await httpClient.PostAsync(ApiConfig.BuildUrl("api/solicitudes"), content);
                var respuestaJson = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(respuestaJson);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("solicitud", out var solElement) && solElement.TryGetProperty("id", out var idElement)) return idElement.GetInt32();
                    if (root.TryGetProperty("id", out var directId)) return directId.GetInt32();

                    await DisplayAlert("Respuesta inesperada", "La API respondió correctamente, pero no devolvió el ID de la carrera.", "OK");
                }
                else
                {
                    await DisplayAlert($"No se pudo crear la carrera ({(int)response.StatusCode})",
                        string.IsNullOrWhiteSpace(respuestaJson) ? response.ReasonPhrase ?? "Error del servidor." : respuestaJson,
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error de conexión", $"No se pudo enviar la solicitud a la API: {ex.Message}", "OK");
            }
            return 0;
        }

        private async Task MonitorearPostulacionesAsync()
        {
            if (solicitudActivaId <= 0) return;

            monitoreandoSolicitud = true;
            ctsSolicitud?.Cancel();
            ctsSolicitud = new CancellationTokenSource();

            while (monitoreandoSolicitud && !ctsSolicitud.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2500, ctsSolicitud.Token);
                    var response = await httpClient.GetAsync($"{ApiConfig.BaseUrl}/api/postulaciones/solicitud/{solicitudActivaId}", ctsSolicitud.Token);
                    if (!response.IsSuccessStatusCode) continue;

                    var json = await response.Content.ReadAsStringAsync(ctsSolicitud.Token);
                    using var doc = JsonDocument.Parse(json);
                    var listaItems = new List<PostulacionConductorItem>();

                    if (doc.RootElement.TryGetProperty("postulaciones", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var elem in arr.EnumerateArray())
                        {
                            string nombreConductor = ObtenerTextoFlexible(elem, "nombreConductor", "conductorNombre", "nombre") ?? "Conductor Asignado";
                            string vehiculo = ObtenerTextoFlexible(elem, "vehiculo", "modelo", "carro", "vehiculoInfo") ?? "";
                            string placa = ObtenerTextoFlexible(elem, "placa", "patente", "placas") ?? "";
                            double calificacion = ObtenerPropiedadDoubleFlexible(elem, "calificacion", "rating", "estrellas");
                            if (calificacion == 0) calificacion = 5.0; // Fallback

                            if (TryGetPropertyInsensitive(elem, "conductor", out var conductorObj))
                            {
                                nombreConductor = ObtenerTextoFlexible(conductorObj, "nombre", "nombreCompleto", "nombres") ?? nombreConductor;
                                vehiculo = ObtenerTextoFlexible(conductorObj, "vehiculo", "modelo", "carro") ?? vehiculo;
                                placa = ObtenerTextoFlexible(conductorObj, "placa", "patente") ?? placa;
                                calificacion = ObtenerPropiedadDoubleFlexible(conductorObj, "calificacion", "rating") > 0 ? ObtenerPropiedadDoubleFlexible(conductorObj, "calificacion", "rating") : calificacion;
                            }

                            string vehiculoInfo = string.IsNullOrWhiteSpace(placa) ? vehiculo : $"{vehiculo} • {placa}";
                            if (string.IsNullOrWhiteSpace(vehiculoInfo)) vehiculoInfo = "Vehículo Autorizado";

                            int idPostulacion = ObtenerPropiedadIntFlexible(elem, "id", "postulacionId");
                            int conductorId = ObtenerPropiedadIntFlexible(elem, "conductorId", "idConductor");
                            decimal tarifa = ObtenerPropiedadDecimalFlexible(elem, "tarifaOfrecida", "oferta", "precio", "tarifa");

                            listaItems.Add(new PostulacionConductorItem
                            {
                                Id = idPostulacion,
                                ConductorId = conductorId,
                                NombreConductor = nombreConductor,
                                VehiculoInfo = vehiculoInfo,
                                CalificacionTexto = $"⭐ {calificacion:0.0}",
                                Tarifa = tarifa
                            });
                        }
                    }

                    if (listaItems.Count > 0)
                    {
                        monitoreandoSolicitud = false;
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            CvConductoresDisponibles.ItemsSource = listaItems;
                            OverlayPropuestaConductor.IsVisible = true;
                        });

                        tcsSeleccionConductor = new TaskCompletionSource<PostulacionConductorItem?>();
                        var itemElegido = await tcsSeleccionConductor.Task;

                        await MainThread.InvokeOnMainThreadAsync(() => OverlayPropuestaConductor.IsVisible = false);

                        if (itemElegido != null && await AceptarPostulacionServidorAsync(itemElegido.Id))
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await DisplayAlert("✅ Conductor Confirmado", $"{itemElegido.NombreConductor} va en camino.", "OK");
                                await Navigation.PushAsync(new ViajeEnCursoPage(solicitudActivaId, itemElegido.NombreConductor, itemElegido.VehiculoInfo, itemElegido.Tarifa, clienteIdActual));
                            });
                            break;
                        }
                        monitoreandoSolicitud = true; // Retomar si cancela o falla
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception) { }
            }
        }

        private async Task<bool> AceptarPostulacionServidorAsync(int postulacionId)
        {
            try
            {
                using var content = new StringContent("", Encoding.UTF8, "application/json");
                var response = await httpClient.PutAsync($"{ApiConfig.BaseUrl}/api/postulaciones/aceptar/{postulacionId}", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private void BtnElegirConductorEspecifico_Clicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is PostulacionConductorItem item)
            {
                tcsSeleccionConductor?.TrySetResult(item);
            }
        }

        private async void BtnCancelarBusqueda_Clicked(object sender, EventArgs e)
        {
            if (solicitudActivaId > 0)
            {
                await CancelarSolicitudPasajeroAsync("Cancelado por el pasajero durante la búsqueda");
                return;
            }
            tcsSeleccionConductor?.TrySetResult(null);
        }

        private async void BtnCancelarServicioPasajero_Clicked(object sender, EventArgs e)
        {
            await CancelarSolicitudPasajeroAsync("Cancelado por el pasajero");
        }

        private async Task CancelarSolicitudPasajeroAsync(string motivo)
        {
            if (solicitudActivaId <= 0) return;
            if (!await DisplayAlert("Cancelar carrera", "¿Deseas cancelar realmente esta solicitud? El conductor será informado.", "Sí, cancelar", "Volver")) return;

            try
            {
                var payload = new
                {
                    Rol = "cliente",
                    ClienteId = clienteIdActual,
                    Motivo = motivo
                };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await httpClient.PutAsync($"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudActivaId}/cancelar", content);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    await DisplayAlert("No se pudo cancelar", ExtraerMensajeApi(body) ?? $"El servidor respondió {(int)response.StatusCode}.", "OK");
                    return;
                }

                solicitudActivaId = 0;
                monitoreandoSolicitud = false;
                ctsSolicitud?.Cancel();
                tcsSeleccionConductor?.TrySetResult(null);
                OverlayPropuestaConductor.IsVisible = false;
                await DisplayAlert("Carrera cancelada", "La solicitud quedó cancelada en el servidor y el conductor verá la notificación.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error de conexión", $"No se confirmó la cancelación en el servidor: {ex.Message}", "OK");
            }
        }

        private string? ExtraerMensajeApi(string json)
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

        private async void BtnCerrarSesion_Clicked(object sender, EventArgs e)
        {
            if (!await DisplayAlert("Cerrar Sesión", "¿Estás seguro de salir?", "Sí", "No")) return;
            DetenerTodosLosMonitoreos();
            Preferences.Clear();
            await Shell.Current.GoToAsync("//LoginPage");
        }

        // --- MÉTODOS AUXILIARES JSON FLEXIBLES ---
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
                if (TryGetPropertyInsensitive(root, n, out var val) && val.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(val.GetString())) return val.GetString();
            }
            return null;
        }

        private double ObtenerPropiedadDoubleFlexible(JsonElement root, params string[] nombresPosibles)
        {
            foreach (var n in nombresPosibles)
            {
                if (TryGetPropertyInsensitive(root, n, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Number && val.TryGetDouble(out double d)) return d;
                    if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double pd)) return pd;
                }
            }
            return 0;
        }

        private int ObtenerPropiedadIntFlexible(JsonElement root, params string[] nombresPosibles)
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

        private decimal ObtenerPropiedadDecimalFlexible(JsonElement root, params string[] nombresPosibles)
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