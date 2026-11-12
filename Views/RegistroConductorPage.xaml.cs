using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ViandriveApp.Helpers;

namespace ViandriveApp.Views
{
    public partial class RegistroConductorPage : ContentPage
    {
        private int pasoActual = 1;

        // Nuevas variables para la verificación de correo
        private string tokenGenerado = string.Empty;
        private bool correoVerificado = false;

        public ObservableCollection<ColorVisual> ColoresVisuales { get; set; }
        public ObservableCollection<string> TodosLosModelos { get; set; }
        public ObservableCollection<string> ModelosFiltrados { get; set; }

        private string colorSeleccionado;
        private string modeloSeleccionado;
        private string selfieBase64;
        private string tarjetaBase64;
        private string soatBase64;
        private string vehiculoBase64;

        public RegistroConductorPage()
        {
            InitializeComponent();

            InicializarColoresVisuales();
            InicializarModelos();

            BindingContext = this;

            MostrarPaso(1);
        }

        private void InicializarColoresVisuales()
        {
            ColoresVisuales = new ObservableCollection<ColorVisual>
            {
                new ColorVisual { Nombre = "Negro", Hex = Color.FromArgb("#000000") },
                new ColorVisual { Nombre = "Blanco", Hex = Color.FromArgb("#FFFFFF") },
                new ColorVisual { Nombre = "Rojo", Hex = Color.FromArgb("#FF0000") },
                new ColorVisual { Nombre = "Azul", Hex = Color.FromArgb("#0000FF") },
                new ColorVisual { Nombre = "Gris", Hex = Color.FromArgb("#808080") },
                new ColorVisual { Nombre = "Verde", Hex = Color.FromArgb("#008000") }
            };
        }

        private static string EncriptarSHA256(string textoPlano)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(textoPlano);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

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

        private ConductorRegistroDto ConstruirObjetoConductor(string passwordPlano)
        {
            return new ConductorRegistroDto
            {
                Name = NombreEntry.Text?.Trim(),
                Age = int.TryParse(EdadEntry.Text, out var edad) ? edad : 0,
                Phone = TelefonoEntry.Text?.Trim(),
                Email = EmailEntry.Text?.Trim(),
                Document = DocumentoEntry.Text?.Trim(),
                LicenseType = TipoLicenciaEntry.Text?.Trim(),
                LicenseNumber = NumeroLicenciaEntry.Text?.Trim(),
                Plate = PlacaEntry.Text?.Trim(),
                Color = colorSeleccionado,
                Model = modeloSeleccionado,
                Year = int.TryParse(AnioVehiculoEntry.Text, out var anio) ? anio : 0,
                Password = string.IsNullOrWhiteSpace(passwordPlano) ? null : EncriptarSHA256(passwordPlano),
                SelfieBase64 = selfieBase64,
                PropertyCardBase64 = tarjetaBase64,
                SoatBase64 = soatBase64,
                VehiclePhotosBase64 = vehiculoBase64
            };
        }

        private void OnNombreChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;
            var texto = e.NewTextValue?.ToUpper() ?? "";
            if (entry.Text != texto)
            {
                entry.Text = texto;
            }
        }

        private void OnDocumentoChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;
            var soloNumeros = new string((e.NewTextValue ?? "").Where(char.IsDigit).ToArray());
            if (entry.Text != soloNumeros)
            {
                entry.Text = soloNumeros;
            }
        }

        private void OnTelefonoChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;
            var soloNumeros = new string((e.NewTextValue ?? "").Where(char.IsDigit).ToArray());
            if (entry.Text != soloNumeros)
            {
                entry.Text = soloNumeros;
            }
        }

        private void OnEmailChanged(object sender, TextChangedEventArgs e)
        {
            // Si cambia el correo después de haber sido verificado, reiniciamos el estado
            correoVerificado = false;

            var email = e.NewTextValue ?? "";
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                EmailEntry.TextColor = Colors.Red;
            }
            else
            {
                EmailEntry.TextColor = Color.FromArgb("#F5F7FA");
            }
        }

        private void OnEdadChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;
            var soloNumeros = new string((e.NewTextValue ?? "").Where(char.IsDigit).ToArray());
            if (entry.Text != soloNumeros)
            {
                entry.Text = soloNumeros;
            }
        }

        private void OnTipoLicenciaChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;
            var texto = e.NewTextValue?.ToUpper() ?? "";
            if (entry.Text != texto)
            {
                entry.Text = texto;
            }
        }

        private void OnNumeroLicenciaChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;
            var soloNumeros = new string((e.NewTextValue ?? "").Where(char.IsDigit).ToArray());
            if (entry.Text != soloNumeros)
            {
                entry.Text = soloNumeros;
            }
        }

        private void OnPlacaChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;
            var limpio = new string((e.NewTextValue ?? "").Where(c => char.IsLetterOrDigit(c)).ToArray()).ToUpper();
            if (entry.Text != limpio)
            {
                entry.Text = limpio;
            }
        }

        private void OnAnioChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;
            var soloNumeros = new string((e.NewTextValue ?? "").Where(char.IsDigit).ToArray());

            if (entry.Text != soloNumeros)
            {
                entry.Text = soloNumeros;
            }

            if (int.TryParse(soloNumeros, out int anio))
            {
                entry.TextColor = (anio < 1980 || anio > DateTime.Now.Year) ? Colors.Red : Color.FromArgb("#F5F7FA");
            }
        }

        private void InicializarModelos()
        {
            TodosLosModelos = new ObservableCollection<string>
            {
                "Spark", "Logan", "Mazda 3", "Hilux", "Duster", "Sail", "Kia Picanto", "Renault Sandero"
            };

            ModelosFiltrados = new ObservableCollection<string>(TodosLosModelos);
        }

        private void MostrarPaso(int paso)
        {
            pasoActual = paso;

            PanelDatosPersonales.IsVisible = paso == 1;
            PanelVehiculo.IsVisible = paso == 2;
            PanelSeguridad.IsVisible = paso == 3;

            // Colores Neon Futuristas
            Paso1Button.BackgroundColor = paso == 1 ? Color.FromArgb("#00E5FF") : Color.FromArgb("#1A1B22");
            Paso2Button.BackgroundColor = paso == 2 ? Color.FromArgb("#00E5FF") : Color.FromArgb("#1A1B22");
            Paso3Button.BackgroundColor = paso == 3 ? Color.FromArgb("#00E5FF") : Color.FromArgb("#1A1B22");

            Paso1Button.TextColor = paso == 1 ? Color.FromArgb("#0B0C10") : Color.FromArgb("#4A4C5A");
            Paso2Button.TextColor = paso == 2 ? Color.FromArgb("#0B0C10") : Color.FromArgb("#4A4C5A");
            Paso3Button.TextColor = paso == 3 ? Color.FromArgb("#0B0C10") : Color.FromArgb("#4A4C5A");
        }

        private void Paso1_Clicked(object sender, EventArgs e) => MostrarPaso(1);
        private void Paso2_Clicked(object sender, EventArgs e) => MostrarPaso(2);
        private void Paso3_Clicked(object sender, EventArgs e) => MostrarPaso(3);

        // --- LÓGICA DE VERIFICACIÓN DE CORREO ---
        private string GenerarToken()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private async Task<bool> SolicitarEnvioCorreoAsync(string correo, string token)
        {
            try
            {
                // Añadimos Tipo = "RegistroConductorPage" para que el backend sepa qué texto e interfaz enviar por correo
                var requestData = new { Email = correo, Token = token, Tipo = "RegistroConductorPage" };
                var json = JsonSerializer.Serialize(requestData);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                // IMPORTANTE: Si pruebas en un EMULADOR de Android, cambia "localhost" por "10.0.2.2"
                var url = ApiConfig.BuildUrl("api/EmailApi/enviar-token");
                using var client = new HttpClient();
                var response = await client.PostAsync(url, content);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async void OnSiguiente1Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NombreEntry.Text) ||
                string.IsNullOrWhiteSpace(EdadEntry.Text) ||
                string.IsNullOrWhiteSpace(TelefonoEntry.Text) ||
                string.IsNullOrWhiteSpace(EmailEntry.Text) ||
                string.IsNullOrWhiteSpace(DocumentoEntry.Text))
            {
                await DisplayAlert("Faltan datos", "Completa todos los campos personales antes de continuar.", "OK");
                return;
            }

            if (!correoVerificado)
            {
                tokenGenerado = GenerarToken();

                // Simulamos una carga para dar feedback al usuario
                var cargando = DisplayAlert("Procesando", "Enviando código de verificación...", "OK");

                bool enviado = await SolicitarEnvioCorreoAsync(EmailEntry.Text.Trim(), tokenGenerado);

                if (enviado)
                {
                    string input = await DisplayPromptAsync(
                        "Verificación de Correo",
                        $"Ingresa el código de 6 dígitos que enviamos a {EmailEntry.Text}",
                        "Verificar",
                        "Cancelar",
                        keyboard: Keyboard.Numeric,
                        maxLength: 6);

                    if (string.IsNullOrWhiteSpace(input)) return;

                    if (input == tokenGenerado)
                    {
                        correoVerificado = true;
                        await DisplayAlert("Éxito", "Correo electrónico verificado correctamente.", "OK");
                        MostrarPaso(2);
                    }
                    else
                    {
                        await DisplayAlert("Error", "El código ingresado es incorrecto. Intenta nuevamente.", "OK");
                    }
                }
                else
                {
                    await DisplayAlert("Error", "Hubo un problema intentando enviar el código a tu correo. Revisa tu conexión a la API.", "OK");
                }
            }
            else
            {
                MostrarPaso(2);
            }
        }

        private async void OnSiguiente2Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TipoLicenciaEntry.Text) ||
                string.IsNullOrWhiteSpace(NumeroLicenciaEntry.Text) ||
                string.IsNullOrWhiteSpace(PlacaEntry.Text) ||
                string.IsNullOrWhiteSpace(colorSeleccionado) ||
                string.IsNullOrWhiteSpace(modeloSeleccionado) ||
                string.IsNullOrWhiteSpace(AnioVehiculoEntry.Text))
            {
                await DisplayAlert("Faltan datos", "Completa todos los campos del vehículo antes de continuar.", "OK");
                return;
            }

            MostrarPaso(3);
        }

        private void OnAbrirSelectorColor(object sender, EventArgs e)
        {
            ColorPopup.IsVisible = !ColorPopup.IsVisible;
        }

        private void OnColorVisualSeleccionado(object sender, SelectionChangedEventArgs e)
        {
            var seleccionado = e.CurrentSelection.FirstOrDefault() as ColorVisual;
            if (seleccionado == null) return;

            colorSeleccionado = seleccionado.Nombre;
            ColorSelectorButton.Text = $"Color: {colorSeleccionado}";
            ColorSelectorButton.BackgroundColor = seleccionado.Hex;
            ColorPopup.IsVisible = false;
        }

        private void OnBuscarModelo(object sender, TextChangedEventArgs e)
        {
            var filtro = e.NewTextValue?.ToLower() ?? "";
            ModelosFiltrados.Clear();
            var coincidencias = TodosLosModelos.Where(m => m.ToLower().Contains(filtro)).ToList();

            foreach (var modelo in coincidencias)
            {
                ModelosFiltrados.Add(modelo);
            }

            ModelosView.IsVisible = coincidencias.Any();
        }

        private void OnModeloSeleccionado(object sender, SelectionChangedEventArgs e)
        {
            var seleccionado = e.CurrentSelection.FirstOrDefault()?.ToString();
            if (string.IsNullOrWhiteSpace(seleccionado)) return;

            modeloSeleccionado = seleccionado;
            ModeloSearchBar.Text = seleccionado;
            ModelosView.IsVisible = false;
            ModeloSearchBar.Unfocus();
        }

        private async void OnSubirFotosVehiculoClicked(object sender, EventArgs e)
        {
            try
            {
                var resultado = await MediaPicker.PickPhotoAsync();
                if (resultado == null) return;
                var stream = await resultado.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                vehiculoBase64 = Convert.ToBase64String(ms.ToArray());
                await DisplayAlert("Correcto", "La foto del vehículo fue cargada.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo cargar la foto del vehículo:\n\n{ex.Message}", "OK");
            }
        }

        private async void OnSubirSelfieClicked(object sender, EventArgs e)
        {
            try
            {
                var resultado = await MediaPicker.PickPhotoAsync();
                if (resultado == null) return;
                var stream = await resultado.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                selfieBase64 = Convert.ToBase64String(bytes);
                SelfiePreview.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                SelfiePreview.IsVisible = true;
                await DisplayAlert("Correcto", "La selfie fue cargada.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo cargar la imagen:\n\n{ex.Message}", "OK");
            }
        }

        private async void OnSubirTarjetaClicked(object sender, EventArgs e)
        {
            try
            {
                var resultado = await MediaPicker.PickPhotoAsync();
                if (resultado == null) return;
                var stream = await resultado.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                tarjetaBase64 = Convert.ToBase64String(ms.ToArray());
                await DisplayAlert("Correcto", "La tarjeta de propiedad fue cargada.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo cargar la tarjeta:\n\n{ex.Message}", "OK");
            }
        }

        private async void OnSubirSoatClicked(object sender, EventArgs e)
        {
            try
            {
                var resultado = await MediaPicker.PickPhotoAsync();
                if (resultado == null) return;
                var stream = await resultado.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                soatBase64 = Convert.ToBase64String(ms.ToArray());
                await DisplayAlert("Correcto", "El SOAT o seguro fue cargado.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo cargar el SOAT:\n\n{ex.Message}", "OK");
            }
        }

        private async void OnRegistrarClicked(object sender, EventArgs e)
        {
            try
            {
                if (!TerminosCheck.IsChecked)
                {
                    await DisplayAlert("Validación", "Debes aceptar los términos y condiciones.", "OK");
                    return;
                }

                var passwordPlano = PasswordEntry.Text?.Trim();
                var confirmarPlano = ConfirmarPasswordEntry.Text?.Trim();

                if (string.IsNullOrWhiteSpace(passwordPlano))
                {
                    await DisplayAlert("Validación", "Ingresa una contraseña.", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(confirmarPlano))
                {
                    await DisplayAlert("Validación", "Confirma la contraseña.", "OK");
                    return;
                }

                if (passwordPlano != confirmarPlano)
                {
                    await DisplayAlert("Validación", "Las contraseñas no coinciden.", "OK");
                    return;
                }

                var conductor = ConstruirObjetoConductor(passwordPlano);

                if (string.IsNullOrWhiteSpace(conductor.Name) ||
                    conductor.Age <= 0 ||
                    string.IsNullOrWhiteSpace(conductor.Phone) ||
                    string.IsNullOrWhiteSpace(conductor.Email) ||
                    string.IsNullOrWhiteSpace(conductor.Document) ||
                    string.IsNullOrWhiteSpace(conductor.LicenseType) ||
                    string.IsNullOrWhiteSpace(conductor.LicenseNumber) ||
                    string.IsNullOrWhiteSpace(conductor.Plate) ||
                    string.IsNullOrWhiteSpace(conductor.Color) ||
                    string.IsNullOrWhiteSpace(conductor.Model) ||
                    conductor.Year < 1980 ||
                    string.IsNullOrWhiteSpace(conductor.Password) ||
                    string.IsNullOrWhiteSpace(conductor.SelfieBase64) ||
                    string.IsNullOrWhiteSpace(conductor.PropertyCardBase64) ||
                    string.IsNullOrWhiteSpace(conductor.SoatBase64) ||
                    string.IsNullOrWhiteSpace(conductor.VehiclePhotosBase64))
                {
                    string faltantes =
                        $"Nombre: {(string.IsNullOrWhiteSpace(conductor.Name) ? "FALTA" : "OK")}\n" +
                        $"Edad: {(conductor.Age <= 0 ? "FALTA" : "OK")}\n" +
                        $"Teléfono: {(string.IsNullOrWhiteSpace(conductor.Phone) ? "FALTA" : "OK")}\n" +
                        $"Correo: {(string.IsNullOrWhiteSpace(conductor.Email) ? "FALTA" : "OK")}\n" +
                        $"Documento: {(string.IsNullOrWhiteSpace(conductor.Document) ? "FALTA" : "OK")}\n" +
                        $"Licencia: {(string.IsNullOrWhiteSpace(conductor.LicenseNumber) ? "FALTA" : "OK")}\n" +
                        $"Placa: {(string.IsNullOrWhiteSpace(conductor.Plate) ? "FALTA" : "OK")}\n" +
                        $"Color: {(string.IsNullOrWhiteSpace(conductor.Color) ? "FALTA" : "OK")}\n" +
                        $"Modelo: {(string.IsNullOrWhiteSpace(conductor.Model) ? "FALTA" : "OK")}\n" +
                        $"Año: {(conductor.Year < 1980 ? "FALTA" : "OK")}\n" +
                        $"Selfie: {(string.IsNullOrWhiteSpace(conductor.SelfieBase64) ? "FALTA" : "OK")}\n" +
                        $"Tarjeta: {(string.IsNullOrWhiteSpace(conductor.PropertyCardBase64) ? "FALTA" : "OK")}\n" +
                        $"SOAT: {(string.IsNullOrWhiteSpace(conductor.SoatBase64) ? "FALTA" : "OK")}\n" +
                        $"Fotos vehículo: {(string.IsNullOrWhiteSpace(conductor.VehiclePhotosBase64) ? "FALTA" : "OK")}";

                    await DisplayAlert("Datos faltantes", faltantes, "OK");
                    return;
                }

                await DisplayAlert("Registro", "Enviando información del conductor...", "OK");

                var json = JsonSerializer.Serialize(conductor);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

                // IMPORTANTE: Si pruebas en un EMULADOR de Android, cambia "localhost" por "10.0.2.2"
                var url = ApiConfig.BuildUrl("api/ConductorApi/registrar");

                var response = await client.PostAsync(url, content);
                var respuestaServidor = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Preferences.Set("ConductorNombre", conductor.Name);
                    Preferences.Set("ConductorPlaca", conductor.Plate);
                    Preferences.Set("ConductorModelo", conductor.Model);
                    Preferences.Set("ConductorColor", conductor.Color);
                    Preferences.Set("ConductorAnio", conductor.Year);

                    await DisplayAlert("Registro exitoso", "El conductor fue registrado correctamente.", "Continuar");

                    await Shell.Current.GoToAsync(
                        $"///registroExitoso" +
                        $"?nombre={Uri.EscapeDataString(conductor.Name)}" +
                        $"&placa={Uri.EscapeDataString(conductor.Plate)}" +
                        $"&color={Uri.EscapeDataString(conductor.Color)}");
                }
                else
                {
                    await DisplayAlert(
                        $"Error {(int)response.StatusCode}",
                        string.IsNullOrWhiteSpace(respuestaServidor) ? response.StatusCode.ToString() : respuestaServidor,
                        "OK");
                }
            }
            catch (HttpRequestException ex)
            {
                await DisplayAlert("Error de conexión", ex.Message, "OK");
            }
            catch (TaskCanceledException)
            {
                await DisplayAlert("Tiempo agotado", "El servidor no respondió dentro del tiempo esperado.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        public class ColorVisual
        {
            public string Nombre { get; set; }
            public Color Hex { get; set; }
        }
    }
}