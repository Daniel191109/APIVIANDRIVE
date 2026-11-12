using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ViandriveApp.Helpers;
using ViandriveApp.Models; // Necesario para ApiConfig.BaseUrl

namespace ViandriveApp.Views
{
    public partial class Usuario : ContentPage
    {
        private readonly HttpClient _httpClient;
        private int _clienteId;

        public Usuario()
        {
            InitializeComponent();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 1. Mostrar datos guardados inmediatamente (evita ver "Cargando...")
            CargarDatosLocales();

            // 2. Traer información real en segundo plano
            _clienteId = Preferences.Get("ClienteId", 0);
            if (_clienteId > 0)
            {
                await CargarDatosRealesDesdeApiAsync();
            }
        }

        private void CargarDatosLocales()
        {
            string nombre = Preferences.Get("Nombre", "Usuario VianDrive");
            string correo = Preferences.Get("UsuarioCorreo", "Actualizando...");
            string telefono = Preferences.Get("UsuarioTelefono", "Actualizando...");
            string viajes = Preferences.Get("UsuarioViajes", "0");
            string calificacion = Preferences.Get("UsuarioCalificacion", "5.0 ★");

            lblNombre.Text = nombre;
            lblNombreCuenta.Text = nombre;

            lblCorreo.Text = correo;
            lblCorreoCuenta.Text = correo;

            lblTelefonoCuenta.Text = telefono;
            lblViajes.Text = viajes;
            lblCalificacion.Text = calificacion;

            if (!string.IsNullOrWhiteSpace(nombre))
            {
                lblInicial.Text = nombre.Trim().Substring(0, 1).ToUpper();
            }
            else
            {
                lblInicial.Text = "U";
            }
        }

        private async Task CargarDatosRealesDesdeApiAsync()
        {
            try
            {
                // Usamos ApiConfig en lugar de localhost
                string url = $"{ApiConfig.BaseUrl}/api/ClienteApi/{_clienteId}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string correoReal = ObtenerTextoFlexible(root, "correo", "email") ?? lblCorreo.Text;
                    string telefonoReal = ObtenerTextoFlexible(root, "telefono", "celular") ?? "Sin registrar";
                    string viajesReales = ObtenerTextoFlexible(root, "viajes", "totalViajes", "cantidadViajes") ?? "0";
                    double calificacionReal = ObtenerPropiedadDoubleFlexible(root, "calificacion", "rating", "puntuacion");
                    if (calificacionReal == 0) calificacionReal = 5.0;

                    // Actualizar vista en el hilo principal
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        lblCorreo.Text = correoReal;
                        lblCorreoCuenta.Text = correoReal;
                        lblTelefonoCuenta.Text = telefonoReal;
                        lblViajes.Text = viajesReales;
                        lblCalificacion.Text = $"{calificacionReal:0.0} ★";
                    });

                    // Guardar para la próxima vez
                    Preferences.Set("UsuarioCorreo", correoReal);
                    Preferences.Set("UsuarioTelefono", telefonoReal);
                    Preferences.Set("UsuarioViajes", viajesReales);
                    Preferences.Set("UsuarioCalificacion", $"{calificacionReal:0.0} ★");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar datos de API: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (lblCorreo.Text == "Actualizando...") lblCorreo.Text = "Sin registrar";
                    if (lblCorreoCuenta.Text == "Actualizando...") lblCorreoCuenta.Text = "Sin registrar";
                    if (lblTelefonoCuenta.Text == "Actualizando...") lblTelefonoCuenta.Text = "Sin registrar";
                });
            }
        }

        #region Editar Perfil

        private void EditarPerfil_Tapped(object sender, EventArgs e)
        {
            EditNombreEntry.Text = Preferences.Get("Nombre", "");
            EditTelefonoEntry.Text = Preferences.Get("UsuarioTelefono", "");
            EditProfileModal.IsVisible = true;
        }

        private void OnCancelarEdicionClicked(object sender, EventArgs e)
        {
            EditProfileModal.IsVisible = false;
        }

        private async void OnGuardarPerfilClicked(object sender, EventArgs e)
        {
            string nuevoNombre = EditNombreEntry.Text?.Trim();
            string nuevoTelefono = EditTelefonoEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(nuevoNombre))
            {
                await DisplayAlert("Atención", "El nombre no puede estar vacío.", "OK");
                return;
            }

            Preferences.Set("Nombre", nuevoNombre);
            if (!string.IsNullOrWhiteSpace(nuevoTelefono))
            {
                Preferences.Set("UsuarioTelefono", nuevoTelefono);
            }

            CargarDatosLocales();
            EditProfileModal.IsVisible = false;
            await DisplayAlert("Perfil actualizado", "Tus datos se han guardado correctamente.", "OK");
        }

        #endregion

        #region Configuración

        private async void Configuracion_Tapped(object sender, EventArgs e)
        {
            string opcion = await DisplayActionSheet(
                "Configuración",
                "Cancelar",
                null,
                "Notificaciones",
                "Privacidad y Seguridad",
                "Términos y Condiciones");

            if (opcion == "Notificaciones")
            {
                await DisplayAlert("Notificaciones", "Las notificaciones de viaje están activadas.", "OK");
            }
            else if (opcion == "Privacidad y Seguridad")
            {
                await DisplayAlert("Seguridad", "Tus datos de viaje se cifran mediante protocolo SSL/TLS.", "OK");
            }
            else if (opcion == "Términos y Condiciones")
            {
                await DisplayAlert("VianDrive", "Versión 1.0.0 - Tu seguridad es nuestro camino.", "OK");
            }
        }

        #endregion

        #region Cerrar Sesión Definitivo

        private async void CerrarSesion_Tapped(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Cerrar sesión", "¿Estás seguro de que deseas salir de tu cuenta?", "Sí, salir", "Cancelar");
            if (confirmar)
            {
                await EjecutarCierreDeSesionAsync();
            }
        }

        private async Task EjecutarCierreDeSesionAsync()
        {
            Preferences.Clear();
            SecureStorage.Default.RemoveAll();
            await Shell.Current.GoToAsync("//LoginPage");
        }

        #endregion

        // Métodos JSON Auxiliares
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
                if (val.ValueKind == JsonValueKind.Number)
                    return val.GetRawText();
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
    }
}