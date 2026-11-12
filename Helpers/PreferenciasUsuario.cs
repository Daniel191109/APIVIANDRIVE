namespace ViaDriveApp.Helpers
{
    public static class PreferenciasUsuario
    {
        private const string ConductorIdKey = "ConductorId";

        public static void GuardarConductorId(int id)
        {
            Preferences.Set(ConductorIdKey, id);
        }

        public static int ObtenerConductorId()
        {
            return Preferences.Get(ConductorIdKey, 0); // 0 si no existe
        }

        public static void LimpiarConductorId()
        {
            Preferences.Remove(ConductorIdKey);
        }
    }
}
