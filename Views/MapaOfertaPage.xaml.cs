using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ViandriveApp.Helpers;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;

namespace ViandriveApp.Views
{
    [QueryProperty(nameof(SolicitudIdParam), "solicitudId")]
    public partial class MapaOfertaPage : ContentPage
    {
        private readonly HttpClient cliente;
        private int solicitudId;
        private decimal tarifaSugerida = 8000;
        private int conductorIdActual = 0;

        private int faseActual = 1;
        private CancellationTokenSource? ctsGps;
        private CancellationTokenSource? ctsMonitoreoCliente;
        private CancellationTokenSource? ctsMonitoreoSolicitudes;
        private CancellationTokenSource? ctsMonitoreoViaje;
        private bool monitoreandoViaje = false;
        private bool monitoreandoCliente = false;
        private bool monitoreandoSolicitudes = false;

        private int calificacionSeleccionada = 5;

        private double latConductor = 0;
        private double lngConductor = 0;
        private double latOrigenPasajero = 0;
        private double lngOrigenPasajero = 0;
        private double latDestinoPasajero = 0;
        private double lngDestinoPasajero = 0;

        public int SolicitudIdParam
        {
            set => solicitudId = value;
        }

        public MapaOfertaPage() : this(0) { }

        public MapaOfertaPage(int idSolicitud)
        {
            InitializeComponent();
            solicitudId = idSolicitud;
            cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            LblTarifaSugerida.Text = $"${tarifaSugerida:N0}";
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            conductorIdActual = Preferences.Get("ConductorId", 0);
            if (conductorIdActual == 0)
            {
                await DisplayAlert("Sesión Expirada", "Por favor, inicia sesión como conductor para continuar.", "OK");
                await Shell.Current.GoToAsync("//LoginPage");
                return;
            }

            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("⚠️ Permiso", "Requerimos GPS para trabajar.", "OK");
                    return;
                }
            }

            await InicializarDatosEnTiempoRealAsync();
            IniciarBucleGpsEnVivo();

            if (faseActual == 1) IniciarMonitoreoSolicitudesEntrantes();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            DetenerTodosLosMonitoreos();
        }

        private void DetenerTodosLosMonitoreos()
        {
            monitoreandoCliente = false;
            monitoreandoSolicitudes = false;
            ctsGps?.Cancel();
            ctsMonitoreoCliente?.Cancel();
            ctsMonitoreoSolicitudes?.Cancel();
            monitoreandoViaje = false;
            ctsMonitoreoViaje?.Cancel();
        }

        private void IniciarMonitoreoSolicitudesEntrantes()
        {
            if (monitoreandoSolicitudes) return;
            monitoreandoSolicitudes = true;
            ctsMonitoreoSolicitudes = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (monitoreandoSolicitudes && !ctsMonitoreoSolicitudes.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (solicitudId <= 0) await BuscarSolicitudPendienteMasRecienteAsync();
                        else await ObtenerDetallesSolicitudApiAsync();

                        await Task.Delay(2500, ctsMonitoreoSolicitudes.Token);
                    }
                    catch { break; }
                }
            }, ctsMonitoreoSolicitudes.Token);
        }

        private async Task BuscarSolicitudPendienteMasRecienteAsync()
        {
            try
            {
                var response = await cliente.GetAsync($"{ApiConfig.BaseUrl}/api/solicitudes/pendientes");
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        if (root[0].TryGetProperty("id", out var idProp) || root[0].TryGetProperty("Id", out idProp))
                        {
                            solicitudId = idProp.GetInt32();
                            if (solicitudId > 0) await ObtenerDetallesSolicitudApiAsync();
                        }
                    }
                }
            }
            catch { }
        }

        private async Task InicializarDatosEnTiempoRealAsync()
        {
            await ObtenerGpsConductorAsync();
            await ObtenerDetallesSolicitudApiAsync();

            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("mapa_oferta.html");
                using var reader = new StreamReader(stream);
                string htmlTemplate = await reader.ReadToEndAsync();

                string htmlFinal = htmlTemplate
                    .Replace("{{origenLat}}", latOrigenPasajero.ToString(CultureInfo.InvariantCulture))
                    .Replace("{{origenLng}}", lngOrigenPasajero.ToString(CultureInfo.InvariantCulture))
                    .Replace("{{destinoLat}}", latDestinoPasajero.ToString(CultureInfo.InvariantCulture))
                    .Replace("{{destinoLng}}", lngDestinoPasajero.ToString(CultureInfo.InvariantCulture));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MapaWeb.Source = new HtmlWebViewSource
                    {
                        Html = htmlFinal,
                        BaseUrl = DeviceInfo.Platform == DevicePlatform.Android ? "file:///android_asset/" : ""
                    };
                });
            }
            catch { }
        }

        private async Task ObtenerGpsConductorAsync()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(5));
                var ubicacion = await Geolocation.GetLocationAsync(request) ?? await Geolocation.GetLastKnownLocationAsync();
                if (ubicacion != null)
                {
                    latConductor = ubicacion.Latitude;
                    lngConductor = ubicacion.Longitude;
                }
            }
            catch { }
        }

        private async Task ObtenerDetallesSolicitudApiAsync()
        {
            if (solicitudId <= 0) return;
            try
            {
                var response = await cliente.GetAsync($"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0) root = root[0];
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (TryGetPropertyInsensitive(root, "solicitud", out var innerSol)) root = innerSol;
                        else if (TryGetPropertyInsensitive(root, "data", out var innerData)) root = innerData;
                    }

                    // EXTRACCIÓN DINÁMICA SÚPER FLEXIBLE DEL PASAJERO
                    latOrigenPasajero = ObtenerPropiedadDoubleFlexible(root, "latOrigen", "latitudOrigen", "origenLat", "lat");
                    lngOrigenPasajero = ObtenerPropiedadDoubleFlexible(root, "lngOrigen", "longitudOrigen", "origenLng", "lng", "lon");
                    latDestinoPasajero = ObtenerPropiedadDoubleFlexible(root, "latDestino", "latitudDestino", "destinoLat");
                    lngDestinoPasajero = ObtenerPropiedadDoubleFlexible(root, "lngDestino", "longitudDestino", "destinoLng");

                    decimal ofertaExtraida = ExtraerOfertaPasajeroUltraFlexible(root);
                    if (ofertaExtraida > 0) tarifaSugerida = ofertaExtraida;

                    string origenTxt = ObtenerTextoFlexible(root, "origen", "origenTexto", "direccionOrigen") ?? "Ubicación actual";
                    string destinoTxt = ObtenerTextoFlexible(root, "destino", "destinoTexto", "direccionDestino") ?? "Destino solicitado";

                    // Extraer nombre verificando objetos anidados (cliente, pasajero, usuario)
                    string nombreTxt = ObtenerTextoFlexible(root, "nombrePasajero", "usuario", "nombreCliente", "clienteNombre", "nombre") ?? "Pasajero Viandrive";
                    if (TryGetPropertyInsensitive(root, "cliente", out var cliObj) ||
                        TryGetPropertyInsensitive(root, "pasajero", out cliObj) ||
                        TryGetPropertyInsensitive(root, "usuario", out cliObj))
                    {
                        nombreTxt = ObtenerTextoFlexible(cliObj, "nombre", "nombreCompleto", "nombres") ?? nombreTxt;
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LblTarifaSugerida.Text = $"${tarifaSugerida:N0}";
                        LblOrigenTexto.Text = origenTxt;
                        LblDestinoTexto.Text = destinoTxt;
                        LblNombrePasajero.Text = nombreTxt;
                    });
                }
            }
            catch { }
        }

        private void IniciarBucleGpsEnVivo()
        {
            ctsGps?.Cancel();
            ctsGps = new CancellationTokenSource();
            var token = ctsGps.Token;
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var req = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(3));
                        var ubi = await Geolocation.GetLocationAsync(req, token);
                        if (ubi != null)
                        {
                            latConductor = ubi.Latitude;
                            lngConductor = ubi.Longitude;
                            await EjecutarJsMapaAsync($"actualizarPosicionConductor({latConductor.ToString(CultureInfo.InvariantCulture)}, {lngConductor.ToString(CultureInfo.InvariantCulture)});");
                            await PublicarUbicacionConductorAsync(token);
                        }
                        await Task.Delay(3000, token);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Error GPS conductor: " + ex.Message);
                        try { await Task.Delay(3000, token); } catch (OperationCanceledException) { break; }
                    }
                }
            }, token);
        }

        private async Task PublicarUbicacionConductorAsync(CancellationToken token)
        {
            if (conductorIdActual <= 0 || latConductor == 0 || lngConductor == 0) return;
            var payload = new { ConductorId = conductorIdActual, Latitud = latConductor, Longitud = lngConductor };
            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await cliente.PostAsync(
                $"{ApiConfig.BaseUrl}/api/conductores/actualizar-ubicacion", content, token);
            if (!response.IsSuccessStatusCode) return;

            if (solicitudId > 0 && faseActual >= 2)
            {
                using var viajeContent = new StringContent(json, Encoding.UTF8, "application/json");
                using var viajeResponse = await cliente.PutAsync(
                    $"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}/ubicacion-conductor", viajeContent, token);
            }
        }

        private async Task EjecutarJsMapaAsync(string script)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (MapaWeb != null) await MapaWeb.EvaluateJavaScriptAsync($"try {{ {script} }} catch(e) {{ }}");
            });
        }

        private async void OnAceptarTarifaOriginalClicked(object sender, EventArgs e)
        {
            if (faseActual == 1) await EnviarPostulacionServidorAsync(tarifaSugerida);
            else if (faseActual == 2)
            {
                try
                {
                    var payloadRecogido = new { ConductorId = conductorIdActual };
                    using var contentRecogido = new StringContent(JsonSerializer.Serialize(payloadRecogido), Encoding.UTF8, "application/json");
                    using var respuestaRecogido = await cliente.PutAsync($"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}/marcar-recogido", contentRecogido);
                    string bodyRecogido = await respuestaRecogido.Content.ReadAsStringAsync();
                    if (!respuestaRecogido.IsSuccessStatusCode)
                    {
                        await DisplayAlert("No se pudo iniciar el viaje", ObtenerMensajeApi(bodyRecogido) ?? $"El servidor respondió {(int)respuestaRecogido.StatusCode}.", "OK");
                        return;
                    }

                    faseActual = 3;
                    await EjecutarJsMapaAsync($"actualizarRutaYEstado({latConductor.ToString(CultureInfo.InvariantCulture)}, {lngConductor.ToString(CultureInfo.InvariantCulture)}, {latDestinoPasajero.ToString(CultureInfo.InvariantCulture)}, {lngDestinoPasajero.ToString(CultureInfo.InvariantCulture)}, 'Hacia destino final');");

                    if (sender is Button btn)
                    {
                        btn.Text = "FINALIZAR SERVICIO";
                        btn.BackgroundColor = Color.FromArgb("#FF3366");
                    }

                    LblTituloEstado.Text = "Viaje en Curso";
                    LblBadgeEstado.Text = "EN RUTA";
                    LblBadgeEstado.TextColor = Color.FromArgb("#00E676");
                    BadgeEstadoContenedor.Stroke = Color.FromArgb("#00E676");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error de conexión", "No se pudo confirmar la recogida en el servidor: " + ex.Message, "OK");
                }
            }
            else if (faseActual == 3)
            {
                PanelInferior.IsVisible = false;
                LblNombreCalificar.Text = LblNombrePasajero.Text;
                OverlayCalificacion.IsVisible = true;
            }
        }

        private async void OnEnviarContraofertaClicked(object sender, EventArgs e)
        {
            // El mismo botón secundario pasa de OFERTAR a CANCELAR después de la aceptación.
            // Reutilizamos su evento XAML y despachamos según la fase real del viaje.
            if (faseActual >= 2)
            {
                OnCancelarViajeClicked(sender, e);
                return;
            }

            if (TxtContraoferta == null)
            {
                await DisplayAlert("Error", "No se encontró el campo de texto.", "OK");
                return;
            }

            string textoIngresado = TxtContraoferta.Text ?? string.Empty;
            var limpio = textoIngresado.Replace(".", "").Replace(",", "").Trim();

            if (decimal.TryParse(limpio, out decimal nuevaTarifa) && nuevaTarifa > 0)
            {
                await EnviarPostulacionServidorAsync(nuevaTarifa);
            }
            else
            {
                await DisplayAlert("Monto inválido", "Por favor ingresa una tarifa válida para la contraoferta.", "OK");
            }
        }

        private async Task EnviarPostulacionServidorAsync(decimal tarifa)
        {
            if (conductorIdActual == 0) return;
            if (solicitudId <= 0) return;

            try
            {
                var payload = new { SolicitudId = solicitudId, ConductorId = conductorIdActual, TarifaOfrecida = tarifa };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await cliente.PostAsync($"{ApiConfig.BaseUrl}/api/postulaciones", content);

                if (response.IsSuccessStatusCode)
                {
                    monitoreandoSolicitudes = false;
                    ctsMonitoreoSolicitudes?.Cancel();

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LayoutContraoferta.IsVisible = false;
                        BtnAccionSecundaria.IsVisible = false;
                        GridBotonesAccion.ColumnDefinitions[0].Width = new GridLength(0);

                        BtnAccionPrincipal.Text = "ESPERANDO AL PASAJERO...";
                        BtnAccionPrincipal.IsEnabled = false;
                        BtnAccionPrincipal.BackgroundColor = Color.FromArgb("#333545");
                        LblTituloEstado.Text = "Oferta Enviada";
                        LblBadgeEstado.Text = "EN ESPERA";
                    });

                    _ = MonitorearEstadoPostulacionAsync(conductorIdActual);
                }
            }
            catch { }
        }

        private async Task MonitorearEstadoPostulacionAsync(int conductorId)
        {
            monitoreandoCliente = true;
            ctsMonitoreoCliente = new CancellationTokenSource();

            while (monitoreandoCliente && !ctsMonitoreoCliente.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2500, ctsMonitoreoCliente.Token);
                    var response = await cliente.GetAsync($"{ApiConfig.BaseUrl}/api/postulaciones/estado?solicitudId={solicitudId}&conductorId={conductorId}", ctsMonitoreoCliente.Token);
                    if (!response.IsSuccessStatusCode) continue;

                    var json = await response.Content.ReadAsStringAsync(ctsMonitoreoCliente.Token);
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("estado", out var estadoProp))
                    {
                        string estado = estadoProp.GetString() ?? "";

                        if (estado == "Aceptada")
                        {
                            monitoreandoCliente = false;
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                faseActual = 2;
                                await EjecutarJsMapaAsync($"actualizarRutaYEstado({latConductor.ToString(CultureInfo.InvariantCulture)}, {lngConductor.ToString(CultureInfo.InvariantCulture)}, {latOrigenPasajero.ToString(CultureInfo.InvariantCulture)}, {lngOrigenPasajero.ToString(CultureInfo.InvariantCulture)}, 'Recogiendo');");

                                LayoutInfoPasajero.IsVisible = true;
                                BtnAccionPrincipal.IsVisible = true;
                                BtnAccionPrincipal.IsEnabled = true;
                                BtnAccionPrincipal.Text = "YA LLEGUÉ AL ORIGEN";
                                BtnAccionPrincipal.BackgroundColor = Color.FromArgb("#FF9800");

                                BtnAccionSecundaria.IsVisible = true;
                                BtnAccionSecundaria.Text = "CANCELAR";
                                GridBotonesAccion.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);

                                LblTituloEstado.Text = "¡Fuiste Seleccionado!";
                                LblBadgeEstado.Text = "EN CAMINO";
                            });
                            IniciarMonitoreoEstadoViaje();
                            break;
                        }
                        else if (estado == "Cancelada")
                        {
                            monitoreandoCliente = false;
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await DisplayAlert("Solicitud cancelada", "El pasajero canceló la solicitud. Ya no está disponible.", "OK");
                                DetenerTodosLosMonitoreos();
                                if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync(); else await Shell.Current.GoToAsync("..");
                            });
                            break;
                        }
                        else if (estado == "Rechazada")
                        {
                            monitoreandoCliente = false;
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await DisplayAlert("Oferta No Elegida", "El cliente seleccionó a otro conductor.", "OK");
                                if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync(); else await Shell.Current.GoToAsync("..");
                            });
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private void Estrella_Tapped(object sender, TappedEventArgs e)
        {
            if (sender is Label lbl && int.TryParse(lbl.ClassId, out int rating))
            {
                calificacionSeleccionada = rating;
                Star1.TextColor = calificacionSeleccionada >= 1 ? Color.FromArgb("#FFD700") : Color.FromArgb("#4A4C5A");
                Star2.TextColor = calificacionSeleccionada >= 2 ? Color.FromArgb("#FFD700") : Color.FromArgb("#4A4C5A");
                Star3.TextColor = calificacionSeleccionada >= 3 ? Color.FromArgb("#FFD700") : Color.FromArgb("#4A4C5A");
                Star4.TextColor = calificacionSeleccionada >= 4 ? Color.FromArgb("#FFD700") : Color.FromArgb("#4A4C5A");
                Star5.TextColor = calificacionSeleccionada >= 5 ? Color.FromArgb("#FFD700") : Color.FromArgb("#4A4C5A");
            }
        }

        private async void EnviarCalificacion_Clicked(object sender, EventArgs e)
        {
            BtnEnviarCalificacion.Text = "ENVIANDO...";
            BtnEnviarCalificacion.IsEnabled = false;
            DetenerTodosLosMonitoreos();

            try
            {
                var payload = new { ConductorId = conductorIdActual, CalificacionPasajero = calificacionSeleccionada, TarifaFinal = tarifaSugerida };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await cliente.PutAsync($"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}/finalizar", content);
            }
            catch { }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(async () => { if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync(); else await Shell.Current.GoToAsync(".."); });
            }
        }

        private async void OnCancelarViajeClicked(object sender, EventArgs e)
        {
            if (solicitudId <= 0) return;
            if (!await Shell.Current.DisplayAlert("Cancelar Viaje", "¿Seguro que deseas cancelar? El pasajero será informado.", "Sí, cancelar", "No")) return;

            try
            {
                var payload = new
                {
                    Rol = "conductor",
                    ConductorId = conductorIdActual,
                    Motivo = "Cancelado por el conductor"
                };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await cliente.PutAsync($"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}/cancelar", content);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    await DisplayAlert("No se pudo cancelar", ObtenerMensajeApi(body) ?? $"El servidor respondió {(int)response.StatusCode}. La carrera sigue activa.", "OK");
                    return;
                }

                DetenerTodosLosMonitoreos();
                await DisplayAlert("Carrera cancelada", "La solicitud quedó cancelada en el servidor. El pasajero verá la notificación.", "OK");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync();
                    else await Shell.Current.GoToAsync("..");
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error de conexión", "No se confirmó la cancelación en el servidor: " + ex.Message, "OK");
            }
        }

        private string? ObtenerMensajeApi(string json)
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

        private void IniciarMonitoreoEstadoViaje()
        {
            if (monitoreandoViaje || solicitudId <= 0) return;
            monitoreandoViaje = true;
            ctsMonitoreoViaje = new CancellationTokenSource();
            var token = ctsMonitoreoViaje.Token;

            Task.Run(async () =>
            {
                while (monitoreandoViaje && !token.IsCancellationRequested)
                {
                    try
                    {
                        using var response = await cliente.GetAsync($"{ApiConfig.BaseUrl}/api/solicitudes/{solicitudId}", token);
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync(token);
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (TryGetPropertyInsensitive(root, "solicitud", out var nested)) root = nested;
                            if (TryGetPropertyInsensitive(root, "estado", out var estadoProp))
                            {
                                string estado = estadoProp.GetString() ?? string.Empty;
                                if (estado.Equals("Cancelada", StringComparison.OrdinalIgnoreCase) ||
                                    estado.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
                                {
                                    string cancelador = TryGetPropertyInsensitive(root, "canceladaPor", out var quien) ? quien.GetString() ?? string.Empty : string.Empty;
                                    monitoreandoViaje = false;
                                    await MainThread.InvokeOnMainThreadAsync(async () =>
                                    {
                                        string mensaje = cancelador.StartsWith("cliente", StringComparison.OrdinalIgnoreCase)
                                            ? "El pasajero canceló la carrera. La solicitud quedó anulada para ambos."
                                            : "La carrera fue cancelada y ya no está activa.";
                                        await DisplayAlert("Carrera cancelada", mensaje, "OK");
                                        DetenerTodosLosMonitoreos();
                                        if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync();
                                        else await Shell.Current.GoToAsync("..");
                                    });
                                    break;
                                }
                            }
                        }
                        await Task.Delay(2500, token);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Error monitoreando cancelación remota: " + ex.Message);
                        try { await Task.Delay(2500, token); } catch (OperationCanceledException) { break; }
                    }
                }
            }, token);
        }

        private async void BtnCerrarSesionConductor_Clicked(object sender, EventArgs e)
        {
            if (!await DisplayAlert("Cerrar Sesión", "¿Estás seguro de salir?", "Sí", "No")) return;
            DetenerTodosLosMonitoreos();
            Preferences.Clear();
            await Shell.Current.GoToAsync("//LoginPage");
        }

        private async void VolverMapa_Clicked(object sender, EventArgs e)
        {
            DetenerTodosLosMonitoreos();
            if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync(); else await Shell.Current.GoToAsync("..");
        }

        private decimal ExtraerOfertaPasajeroUltraFlexible(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) return 0;
            string[] nombres = { "ofertaPasajero", "oferta", "tarifaOfrecida", "monto", "precio" };
            foreach (var nombre in nombres)
            {
                if (TryGetPropertyInsensitive(root, nombre, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out decimal d)) return d;
                    if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pd)) return pd;
                }
            }
            return 0;
        }

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

        private string? ObtenerTextoFlexible(JsonElement root, params string[] nombresPosibles)
        {
            foreach (var n in nombresPosibles)
            {
                if (TryGetPropertyInsensitive(root, n, out var val) && val.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(val.GetString())) return val.GetString();
            }
            return null;
        }
    }
}