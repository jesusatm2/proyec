using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ChronosArena
{
    public partial class FormBatalla : Form
    {
        private List<Personaje> equipoJugador;
        private List<Personaje> equipoIA;

        private int indiceJugador;
        private int indiceIA;

        private GestorBatalla gestor;
        private NucleoBatalla nucleo;

        private Timer temporizadorTurno;
        private int segundosRestantes;

        private bool batallaTerminada;
        private bool turnoIAEnProceso;
        private Random random = new Random();
        private DecisorIA decisorIA;
        private Timer temporizadorIA;

        // ----- Modo PvP local -----
        private bool modoPvP;
        private int turnoActual = 1; // 1 = Jugador 1 (izquierda), 2 = Jugador 2 / IA (derecha)
        private Color colorPnlJugadorOriginal;
        private Color colorPnlIAOriginal;
        private Color colorPnlAccionesOriginal;
        private Color colorAnuncioOriginal;

        private static readonly Color ColorTurnoJ1 = Color.FromArgb(25, 75, 150);
        private static readonly Color ColorTurnoJ2 = Color.FromArgb(150, 30, 40);

        private Personaje JugadorActivo
        {
            get
            {
                if (indiceJugador >= 0 &&
                    indiceJugador < equipoJugador.Count)
                    return equipoJugador[indiceJugador];

                return null;
            }
        }

        private Personaje IAActiva
        {
            get
            {
                if (indiceIA >= 0 &&
                    indiceIA < equipoIA.Count)
                    return equipoIA[indiceIA];

                return null;
            }
        }


        // Personaje que ejecuta la acción en este turno.
        private Personaje Actor
        {
            get
            {
                return (modoPvP && turnoActual == 2) ? IAActiva : JugadorActivo;
            }
        }

        // Personaje que recibe la acción en este turno.
        private Personaje Objetivo
        {
            get
            {
                return (modoPvP && turnoActual == 2) ? JugadorActivo : IAActiva;
            }
        }

        private Personaje ActivoDeEquipo(int equipo)
        {
            return equipo == 1 ? JugadorActivo : IAActiva;
        }

        private void AvanzarIndice(int equipo)
        {
            if (equipo == 1)
                indiceJugador++;
            else
                indiceIA++;
        }

        private bool PuedeRelevarActual()
        {
            if (modoPvP && turnoActual == 2)
                return indiceIA < equipoIA.Count - 1;

            return indiceJugador < equipoJugador.Count - 1;
        }

        private string NombreEquipo(int equipo)
        {
            if (modoPvP)
                return "Jugador " + equipo;

            return equipo == 1 ? "El jugador" : "La computadora";
        }

        // El núcleo devuelve "Jugador" / "IA" / "Empate"; en PvP se traduce.
        private string NombreGanadorHistorial(string ganador)
        {
            if (!modoPvP)
                return ganador;

            if (ganador == "Jugador") return "Jugador 1";
            if (ganador == "IA") return "Jugador 2";
            return ganador;
        }

        public FormBatalla()
        {
            InitializeComponent();
            PantallaCompleta.Instalar(this);
            EscaladorInterfaz.Instalar(this);
        }

        public FormBatalla(
            List<Personaje> jugador,
            List<Personaje> ia) : this()
        {
            if (jugador == null || ia == null)
                throw new ArgumentNullException("Los dos equipos deben existir.");

            equipoJugador = jugador.Where(p => p != null)
                .Select(p => p.Clonar()).ToList();
            equipoIA = ia.Where(p => p != null)
                .Select(p => p.Clonar()).ToList();

            if (equipoJugador.Count == 0 || equipoIA.Count == 0)
                throw new ArgumentException("Cada equipo debe contener al menos un personaje.");

            gestor = new GestorBatalla();
            nucleo = new NucleoBatalla();
            random = new Random();

            decisorIA = new DecisorIA(random);
            temporizadorIA = new Timer();
            temporizadorIA.Interval = 900; // la IA "piensa" 0,9 s sin congelar la ventana
            temporizadorIA.Tick += TemporizadorIA_Tick;

            modoPvP = PartidaConfig.EsModoPvP;
            turnoActual = 1;
            gestor.ModoPvP = modoPvP;

            colorPnlJugadorOriginal = pnlJugador.BackColor;
            colorPnlIAOriginal = pnlIA.BackColor;
            colorPnlAccionesOriginal = pnlAcciones.BackColor;
            colorAnuncioOriginal = lblAnuncio.BackColor;

            indiceJugador = 0;
            indiceIA = 0;
            batallaTerminada = false;
            turnoIAEnProceso = false;

            temporizadorTurno = new Timer();
            temporizadorTurno.Interval = 1000;
            temporizadorTurno.Tick += TemporizadorTurno_Tick;

            CargarFondoArena();
            ActualizarInterfaz();

            AgregarRegistro("La batalla de Chronos Arena ha comenzado.");
            if (modoPvP)
                AgregarRegistro("Turno del Jugador 1. Tiene 30 segundos para realizar su acción.");
            else
                AgregarRegistro("Tienes 30 segundos para realizar tu acción.");
            IniciarTemporizadorTurno();

            FormClosing += FormBatalla_FormClosing;
            FormClosed += delegate
            {
                if (temporizadorTurno != null)
                    temporizadorTurno.Dispose();
                if (temporizadorIA != null)
                    temporizadorIA.Dispose();
            };
        }

        private void CargarFondoArena()
        {
            string url;
            Dictionary<string, string> urls = GestorMazo.ObtenerUrls();
            if (!urls.TryGetValue("fondo:batalla", out url) ||
                String.IsNullOrWhiteSpace(url))
            {
                url = String.Empty;
            }

            picFondo.BackColor = Color.FromArgb(18, 23, 35);
            picFondo.LoadCompleted += delegate(
                object sender,
                System.ComponentModel.AsyncCompletedEventArgs e)
            {
                if (e.Error != null || e.Cancelled)
                {
                    picFondo.Image = null;
                    picFondo.BackColor = Color.FromArgb(18, 23, 35);
                }
            };

            try
            {
                if (!String.IsNullOrWhiteSpace(url))
                    picFondo.LoadAsync(url);
            }
            catch (Exception)
            {
                picFondo.Image = null;
            }
        }

        private void AgregarRegistro(string mensaje)
        {
            txtRegistro.AppendText(
                DateTime.Now.ToString("HH:mm:ss") +
                "  " + mensaje +
                Environment.NewLine);

            txtRegistro.SelectionStart =
                txtRegistro.Text.Length;

            txtRegistro.ScrollToCaret();

            gestor.Registrar(mensaje);
        }

        private void ActualizarInterfaz()
        {
            Personaje jugador = JugadorActivo;
            Personaje ia = IAActiva;

            if (jugador == null || ia == null)
            {
                FinalizarBatalla();
                return;
            }

            lblJugador.Text =
                (modoPvP ? "JUGADOR 1: " : "JUGADOR: ") + jugador.Nombre + " (" + jugador.Clase + ")" +
                "\r\nFacción: " + jugador.Faccion + " | Elemento: " + jugador.Elemento +
                "\r\nAtaque: " + jugador.Ataque + " | Defensa: " + jugador.Defensa;

            lblIA.Text =
                (modoPvP ? "JUGADOR 2: " : "COMPUTADORA: ") + ia.Nombre + " (" + ia.Clase + ")" +
                "\r\nFacción: " + ia.Faccion + " | Elemento: " + ia.Elemento +
                "\r\nAtaque: " + ia.Ataque + " | Defensa: " + ia.Defensa;

            barraVidaJugador.Maximum =
                Math.Max(1, jugador.VidaMax);

            barraVidaJugador.Value =
                Math.Max(0, Math.Min(
                    jugador.Vida,
                    barraVidaJugador.Maximum));

            barraVidaIA.Maximum =
                Math.Max(1, ia.VidaMax);

            barraVidaIA.Value =
                Math.Max(0, Math.Min(
                    ia.Vida,
                    barraVidaIA.Maximum));

            lblVidaJugador.Text =
                jugador.Vida + " / " +
                jugador.VidaMax + " HP";

            lblVidaIA.Text =
                ia.Vida + " / " +
                ia.VidaMax + " HP";

            barraEnergiaJugador.Value = Math.Max(0, Math.Min(100, jugador.Energia));
            barraManaJugador.Value = Math.Max(0, Math.Min(100, jugador.Mana));
            barraEnergiaIA.Value = Math.Max(0, Math.Min(100, ia.Energia));
            barraManaIA.Value = Math.Max(0, Math.Min(100, ia.Mana));

            lblEnergiaJugador.Text = "ENERGÍA: " + jugador.Energia + " / 100";
            lblManaJugador.Text = "MANÁ: " + jugador.Mana + " / 100";
            lblEnergiaIA.Text = "ENERGÍA: " + ia.Energia + " / 100";
            lblManaIA.Text = "MANÁ: " + ia.Mana + " / 100";

            CargarImagen(picJugador, jugador.ImagenUrl);
            CargarImagen(picIA, ia.ImagenUrl);

            if (modoPvP)
            {
                lblAnuncio.Text =
                    (esOnline
                        ? (EsMiTurno ? "TU TURNO" : "TURNO DEL RIVAL") +
                          " (JUGADOR " + turnoActual + ")"
                        : "TURNO DEL JUGADOR " + turnoActual) +
                    "  |  EQUIPOS: " + (equipoJugador.Count - indiceJugador) +
                    " vs " + (equipoIA.Count - indiceIA);

                AplicarIndicadorTurnoPvP();
            }
            else
            {
                lblAnuncio.Text =
                    (turnoIAEnProceso ? "TURNO DE LA IA" : "TU TURNO") +
                    "  |  EQUIPOS: " + (equipoJugador.Count - indiceJugador) +
                    " vs " + (equipoIA.Count - indiceIA);
            }

            bool habilitado =
                !batallaTerminada && !turnoIAEnProceso && EsMiTurno;

            btnAtaque.Enabled = habilitado;
            btnEspecial.Enabled = habilitado;
            btnDefensa.Enabled = habilitado;

            btnRelevar.Enabled =
                habilitado &&
                PuedeRelevarActual();
        }

        // Resalta el panel del jugador en turno y tiñe la barra de acciones
        // con su color, para que quede claro quién debe jugar.
        private void AplicarIndicadorTurnoPvP()
        {
            bool turnoJ1 = (turnoActual == 1);

            pnlJugador.BackColor = turnoJ1 ? ColorTurnoJ1 : colorPnlJugadorOriginal;
            pnlIA.BackColor = turnoJ1 ? colorPnlIAOriginal : ColorTurnoJ2;
            pnlAcciones.BackColor = turnoJ1 ? ColorTurnoJ1 : ColorTurnoJ2;
            lblAnuncio.BackColor = turnoJ1 ? ColorTurnoJ1 : ColorTurnoJ2;
        }

        private void CargarImagen(
            PictureBox cuadro,
            string url)
        {
            if (cuadro == null)
                return;

            if (String.IsNullOrEmpty(url))
            {
                cuadro.Image = null;
                return;
            }

            try
            {
                cuadro.LoadAsync(url);
            }
            catch
            {
                cuadro.Image = null;
            }
        }

        // ATAQUE NORMAL
        private void BtnAtaque_Click(
            object sender,
            EventArgs e)
        {
            if (!PuedeActuar())
                return;

            if (!nucleo.PuedeUsarEnergia(Actor, 10))
            {
                MessageBox.Show(
                    "No tienes suficiente energía para atacar. Usa DEFENSA / RECUPERAR para restaurar energía y maná.",
                    "Chronos Arena",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                IniciarTemporizadorTurno();
                return;
            }

            EnviarAccionRed("ATAQUE");

            int dano = nucleo.EjecutarAtaque(
                Actor,
                Objetivo);

            AgregarRegistro(
                Actor.Nombre +
                " ataca a " + Objetivo.Nombre +
                " e inflige " + dano + " de daño.");

            ResolverAccionJugador();
        }

        // HABILIDAD ESPECIAL
        private void BtnEspecial_Click(
            object sender,
            EventArgs e)
        {
            if (!PuedeActuar())
                return;

            if (!nucleo.PuedeUsarMana(Actor, 20))
            {
                MessageBox.Show(
                    "No tienes suficiente maná para usar la habilidad especial.",
                    "Chronos Arena",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                IniciarTemporizadorTurno();
                return;
            }

            EnviarAccionRed("ESPECIAL");

            string resultado =
                nucleo.EjecutarHabilidad(
                    Actor,
                    Objetivo);

            AgregarRegistro(resultado);

            ResolverAccionJugador();
        }

        // DEFENSA
        private void BtnDefensa_Click(
            object sender,
            EventArgs e)
        {
            if (!PuedeActuar())
                return;

            Personaje defensor = Actor;
            int energiaAnterior = defensor.Energia;
            int manaAnterior = defensor.Mana;
            int defensaAnterior = defensor.Defensa;

            EnviarAccionRed("DEFENSA");

            nucleo.EjecutarDefensa(defensor);

            AgregarRegistro(
                defensor.Nombre + " se defiende: +" +
                (defensor.Energia - energiaAnterior) + " energía, +" +
                (defensor.Mana - manaAnterior) + " maná y +" +
                (defensor.Defensa - defensaAnterior) + " defensa.");

            ResolverAccionJugador();
        }

        // RELEVAR PERSONAJE
        private void BtnRelevar_Click(
            object sender,
            EventArgs e)
        {
            if (!PuedeActuar())
                return;

            if (!PuedeRelevarActual())
            {
                MessageBox.Show(
                    "No tienes más personajes disponibles para relevar.",
                    "Chronos Arena");
                IniciarTemporizadorTurno();
                return;
            }

            EnviarAccionRed("RELEVAR");

            int equipo = (modoPvP && turnoActual == 2) ? 2 : 1;

            AvanzarIndice(equipo);

            AgregarRegistro(
                NombreEquipo(equipo) +
                " releva a su personaje. Entra " +
                ActivoDeEquipo(equipo).Nombre + ".");

            if (modoPvP)
            {
                ResolverAccionPvP();
                return;
            }

            ActualizarInterfaz();

            EjecutarTurnoIA();
        }

        private bool PuedeActuar()
        {
            if (batallaTerminada || turnoIAEnProceso)
                return false;

            // En línea solo actúa quien tiene el turno (salvo que se esté
            // reproduciendo la acción del rival).
            if (esOnline && !aplicandoRemoto && turnoActual != miEquipo)
                return false;

            if (Actor == null || Objetivo == null)
            {
                FinalizarBatalla();
                return false;
            }

            temporizadorTurno.Stop();

            return true;
        }

        // ---------- MODO PvP LOCAL ----------

        // Cierra el turno del jugador actual: resuelve derrotas,
        // pasa el control al otro jugador y reinicia el reloj.
        private void ResolverAccionPvP()
        {
            ResolverDerrotasPvP();

            if (batallaTerminada)
                return;

            turnoActual = (turnoActual == 1) ? 2 : 1;

            AgregarRegistro(
                "Turno del " + NombreEquipo(turnoActual) + ".");

            ActualizarInterfaz();

            IniciarTemporizadorTurno();
        }

        private void ResolverDerrotasPvP()
        {
            for (int equipo = 1; equipo <= 2; equipo++)
            {
                Personaje activo = ActivoDeEquipo(equipo);

                if (activo != null && activo.Vida <= 0)
                {
                    AgregarRegistro(
                        activo.Nombre + " ha sido derrotado.");

                    AvanzarIndice(equipo);

                    Personaje nuevo = ActivoDeEquipo(equipo);

                    if (nuevo == null)
                    {
                        FinalizarBatalla();
                        return;
                    }

                    AgregarRegistro(
                        NombreEquipo(equipo) + " envía a " +
                        nuevo.Nombre + ".");
                }
            }
        }

        private void ResolverAccionJugador()
        {
            if (modoPvP)
            {
                ResolverAccionPvP();
                return;
            }

            if (IAActiva != null && IAActiva.Vida <= 0)
            {
                AgregarRegistro(
                    IAActiva.Nombre + " ha sido derrotado.");

                indiceIA++;

                if (IAActiva == null)
                {
                    FinalizarBatalla();
                    return;
                }

                AgregarRegistro(
                    "La computadora envía a " +
                    IAActiva.Nombre + ".");
            }

            ActualizarInterfaz();

            EjecutarTurnoIA();
        }

        // TURNO DE LA COMPUTADORA
        private void EjecutarTurnoIA()
        {
            if (batallaTerminada)
                return;

            temporizadorTurno.Stop();

            if (JugadorActivo == null || IAActiva == null)
            {
                FinalizarBatalla();
                return;
            }

            turnoIAEnProceso = true;
            ActualizarInterfaz();     // "TURNO DE LA IA" con los botones bloqueados
            temporizadorIA.Start();   // Timer de WinForms: la ventana sigue respondiendo
        }

        private void TemporizadorIA_Tick(object sender, EventArgs e)
        {
            temporizadorIA.Stop();

            if (batallaTerminada)
                return;

            if (IAActiva != null && IAActiva.Vida <= 0)
                indiceIA++;

            Personaje ia = IAActiva;
            Personaje jugador = JugadorActivo;

            if (ia == null || jugador == null)
            {
                turnoIAEnProceso = false;
                FinalizarBatalla();
                return;
            }

            bool puedeRelevar = indiceIA < equipoIA.Count - 1;

            switch (decisorIA.Decidir(ia, jugador, puedeRelevar))
            {
                case AccionIA.Atacar:
                    {
                        int dano = nucleo.EjecutarAtaque(ia, jugador);
                        AgregarRegistro(
                            "La computadora ataca con " + ia.Nombre +
                            " e inflige " + dano + " de daño.");
                        break;
                    }
                case AccionIA.Especial:
                    {
                        AgregarRegistro(
                            "La computadora usa su especial: " +
                            nucleo.EjecutarHabilidad(ia, jugador));
                        break;
                    }
                case AccionIA.Curar:
                    {
                        int curado = nucleo.EjecutarCuracion(ia);
                        AgregarRegistro(
                            ia.Nombre + " recupera " + curado +
                            " puntos de vida.");
                        break;
                    }
                case AccionIA.Relevar:
                    {
                        indiceIA++;
                        AgregarRegistro(
                            "La computadora releva a su personaje. Entra " +
                            IAActiva.Nombre + ".");
                        break;
                    }
                default: // Defender
                    {
                        int energiaAnterior = ia.Energia;
                        int manaAnterior = ia.Mana;
                        int defensaAnterior = ia.Defensa;
                        nucleo.EjecutarDefensa(ia);
                        AgregarRegistro(
                            ia.Nombre + " se defiende: +" +
                            (ia.Energia - energiaAnterior) + " energía, +" +
                            (ia.Mana - manaAnterior) + " maná y +" +
                            (ia.Defensa - defensaAnterior) + " defensa.");
                        break;
                    }
            }

            turnoIAEnProceso = false;

            ResolverDerrotasDespuesDeIA();
        }

        private void ResolverDerrotasDespuesDeIA()
        {
            if (JugadorActivo != null &&
                JugadorActivo.Vida <= 0)
            {
                AgregarRegistro(
                    JugadorActivo.Nombre +
                    " ha sido derrotado.");

                indiceJugador++;

                if (JugadorActivo == null)
                {
                    FinalizarBatalla();
                    return;
                }

                AgregarRegistro(
                    "Entra " + JugadorActivo.Nombre +
                    " al combate.");
            }

            if (IAActiva != null &&
                IAActiva.Vida <= 0)
            {
                AgregarRegistro(
                    IAActiva.Nombre +
                    " ha sido derrotado.");

                indiceIA++;

                if (IAActiva == null)
                {
                    FinalizarBatalla();
                    return;
                }

                AgregarRegistro(
                    "La computadora envía a " +
                    IAActiva.Nombre + ".");
            }

            ActualizarInterfaz();

            IniciarTemporizadorTurno();
        }

        // TEMPORIZADOR
        private void IniciarTemporizadorTurno()
        {
            if (batallaTerminada)
                return;

            segundosRestantes =
                nucleo.DuracionTurnoSegundos;

            lblTemporizador.Text =
                "TIEMPO: " + segundosRestantes + " s";

            temporizadorTurno.Start();
        }

        private void TemporizadorTurno_Tick(
            object sender,
            EventArgs e)
        {
            if (batallaTerminada)
            {
                temporizadorTurno.Stop();
                return;
            }

            segundosRestantes--;

            lblTemporizador.Text =
                "TIEMPO: " + segundosRestantes + " s";

            if (segundosRestantes <= 0)
            {
                temporizadorTurno.Stop();

                // En línea solo el jugador en turno decide el fin del tiempo
                // y avisa al rival; el otro espera el mensaje TIEMPO.
                if (esOnline)
                {
                    if (!EsMiTurno)
                        return;

                    EnviarAccionRed("TIEMPO");
                }

                AgregarRegistro(
                    "Se agotó el tiempo. " + NombreEquipo(turnoActual) +
                    " pierde su turno.");

                if (modoPvP)
                {
                    ResolverAccionPvP();
                    return;
                }

                EjecutarTurnoIA();
            }
        }

        // RENDICIÓN Y REGRESO AL MENÚ
        private void BtnVolver_Click(
            object sender,
            EventArgs e)
        {
            if (batallaTerminada)
            {
                Close();
                return;
            }

            int rindeEquipo = esOnline ? miEquipo : turnoActual;
            int ganaEquipo = (rindeEquipo == 1) ? 2 : 1;

            DialogResult respuesta = MessageBox.Show(
                modoPvP
                    ? "Si vuelves al menú, el Jugador " + rindeEquipo +
                      " se rendirá y el Jugador " + ganaEquipo +
                      " ganará la partida.\r\n\r\n¿Deseas continuar?"
                    : "Si vuelves al menú, te rendirás y la computadora ganará la partida.\r\n\r\n¿Deseas continuar?",
                "Confirmar rendición",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (respuesta != DialogResult.Yes)
                return;

            if (esOnline)
                red.Enviar("RENDIR");

            batallaTerminada = true;
            temporizadorTurno.Stop();

            string ganadorTexto;
            string mensajeFinal;

            if (modoPvP)
            {
                ganadorTexto = "Jugador " + ganaEquipo;
                AgregarRegistro(
                    "El Jugador " + rindeEquipo +
                    " se ha rendido. Gana el Jugador " + ganaEquipo + ".");
                mensajeFinal = "El Jugador " + ganaEquipo + " ha ganado la partida.";
            }
            else
            {
                ganadorTexto = "IA";
                AgregarRegistro(
                    "El jugador se ha rendido. La computadora gana.");
                mensajeFinal = "La computadora ha ganado la partida.";
            }

            if (!gestor.GuardarHistorial(
                ganadorTexto,
                equipoJugador,
                equipoIA))
            {
                MessageBox.Show(
                    "La partida terminó, pero no se pudo guardar el historial: " + gestor.UltimoError,
                    "Chronos Arena",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            MessageBox.Show(
                mensajeFinal,
                "Chronos Arena",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.Cancel;
            Close();
        }

        // Si se cierra la ventana con la X, no se pierde el registro.
        // Las partidas ya finalizadas o guardadas por rendición no se duplican.
        private void FormBatalla_FormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            if (batallaTerminada || gestor == null)
                return;

            // Cerrar la ventana en línea equivale a rendirse.
            if (esOnline)
                red.Enviar("RENDIR");

            batallaTerminada = true;
            if (temporizadorTurno != null)
                temporizadorTurno.Stop();

            gestor.Registrar("La ventana se cerró antes de finalizar. Partida interrumpida.");
            if (!gestor.GuardarHistorial(
                "Interrumpida",
                equipoJugador,
                equipoIA))
            {
                MessageBox.Show(
                    "No se pudo guardar el historial de la partida: " + gestor.UltimoError,
                    "Chronos Arena",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        // FIN DE LA PARTIDA
        private void FinalizarBatalla()
        {
            if (batallaTerminada)
                return;

            batallaTerminada = true;
            temporizadorTurno.Stop();

            string ganador = nucleo.ObtenerGanador(
                equipoJugador,
                equipoIA);

            if (String.IsNullOrEmpty(ganador))
                ganador = "Empate";

            if (!gestor.GuardarHistorial(
                NombreGanadorHistorial(ganador),
                equipoJugador,
                equipoIA))
            {
                MessageBox.Show(
                    "No se pudo guardar el historial de la partida: " + gestor.UltimoError,
                    "Chronos Arena",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            btnAtaque.Enabled = false;
            btnEspecial.Enabled = false;
            btnDefensa.Enabled = false;
            btnRelevar.Enabled = false;

            lblAnuncio.Text =
                "PARTIDA FINALIZADA";

            MessageBox.Show(
                modoPvP
                    ? (ganador == "Jugador"
                        ? "¡Victoria del Jugador 1!"
                        : ganador == "IA"
                            ? "¡Victoria del Jugador 2!"
                            : "La partida terminó en empate.")
                : ganador == "Jugador"
                    ? "¡Victoria! Has derrotado a la computadora."
                    : ganador == "IA"
                        ? "La computadora ha ganado la partida."
                        : "La partida terminó en empate.",
                "Chronos Arena",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
