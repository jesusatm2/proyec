using System;

namespace ChronosArena
{
    /// <summary>Los tres modos que se eligen en FormModoJuego.</summary>
    public enum ModoJuego
    {
        VsIA,       // 1 jugador contra la computadora
        LocalPvP,   // 2 jugadores en la misma PC
        OnlinePvP   // 2 jugadores por TCP/IP (anfitrion / cliente)
    }

    /// <summary>
    /// Estado global de la partida que se va a jugar. Reemplaza al antiguo
    /// par de banderas EsModoPvP / EsOnline: ahora hay UN solo estado y es
    /// imposible tener combinaciones incoherentes (p. ej. online + "vs IA").
    /// Compatible con C# 5 / SharpDevelop (sin interpolacion $ ni "?.").
    /// </summary>
    public static class PartidaConfig
    {
        public const int TamanoMazo = 5;

        private static ModoJuego modo = ModoJuego.VsIA;
        private static GestorRed red;

        public static ModoJuego Modo { get { return modo; } }

        /// <summary>Conexion activa; solo existe en ModoJuego.OnlinePvP.</summary>
        public static GestorRed Red { get { return red; } }

        public static bool EsVsIA    { get { return modo == ModoJuego.VsIA; } }
        public static bool EsLocalPvP { get { return modo == ModoJuego.LocalPvP; } }
        public static bool EsOnline  { get { return modo == ModoJuego.OnlinePvP; } }

        /// <summary>Humano contra humano (local u online).</summary>
        public static bool EsModoPvP { get { return modo != ModoJuego.VsIA; } }

        // ---------- Cambio de estado (lo llama FormModoJuego) ----------

        public static void IniciarVsIA()
        {
            Reiniciar();
            modo = ModoJuego.VsIA;
        }

        public static void IniciarLocalPvP()
        {
            Reiniciar();
            modo = ModoJuego.LocalPvP;
        }

        public static void IniciarOnline(GestorRed conexion)
        {
            if (conexion == null)
                throw new ArgumentNullException("conexion");

            Reiniciar();
            red = conexion;
            modo = ModoJuego.OnlinePvP;
        }

        /// <summary>Cierra el socket (si hay) y vuelve al modo por defecto.</summary>
        public static void Reiniciar()
        {
            if (red != null)
                red.Dispose();

            red = null;
            modo = ModoJuego.VsIA;
        }

        // Alias para no romper llamadas existentes (MainForm.finally, etc.).
        public static void ReiniciarOnline()
        {
            Reiniciar();
        }

        // ---------- Textos de interfaz segun el modo ----------

        public static string EtiquetaMazoPropio(int cartas)
        {
            string texto = EsLocalPvP ? "JUGADOR 1" : "TU MAZO";
            return texto + " (" + cartas + "/" + TamanoMazo + ")";
        }

        /// <summary>"MAZO IA (5/5)", "JUGADOR 2 (5/5)" o "MAZO RIVAL (5/5)".</summary>
        public static string EtiquetaMazoOponente(int cartas)
        {
            string texto;
            switch (modo)
            {
                case ModoJuego.LocalPvP:   texto = "JUGADOR 2"; break;
                case ModoJuego.OnlinePvP:  texto = "MAZO RIVAL"; break;
                default:                   texto = "MAZO IA"; break;
            }
            return texto + " (" + cartas + "/" + TamanoMazo + ")";
        }
    }
}
