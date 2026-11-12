namespace ViandriveApp.Helpers
{
    public static class ApiConfig
    {
        // Mantener la URL sin barra final evita producir rutas como //api/...
        public static string BaseUrl => "https://gerentefinantec-001-site9.ktempurl.com";

        public static string BuildUrl(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("La ruta de la API es obligatoria.", nameof(relativePath));

            return $"{BaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
        }
    }
}
