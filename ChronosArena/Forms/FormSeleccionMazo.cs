using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ChronosArena
{
    public partial class FormSeleccionMazo : Form
    {
        private List<Personaje> catalogo;

        private List<Personaje> disponibles;

        private List<Personaje> mazoJugador1 =
            new List<Personaje>();

        private List<Personaje> mazoJugador2 =
            new List<Personaje>();

        private GestorBatalla gestor =
            new GestorBatalla();

        private Random random =
            new Random();

        private bool reemplazo;

        // Fase de selección en modo PvP: 1 = Jugador 1, 2 = Jugador 2.
        // En modo vs IA se queda siempre en 1 (flujo original).
        private int faseJugador = 1;

        private Personaje cartaPendiente;

        // ----- Modo en línea -----
        private bool yoListo;                       // ya envié mi mazo
        private bool yoComienzo;                    // ya pulsé COMENZAR BATALLA
        private bool rivalComienzo;                 // el rival pulsó COMENZAR
        private List<Personaje> mazoRivalPendiente; // recibido, oculto hasta que yo envíe el mío
        private bool cerrando;

        // Dirección de la imagen original del reverso.
        private string urlReverso;

        // Dirección de la imagen mostrada actualmente como vista previa.
        private string urlVistaPreviaActual;

        // Mazo del Jugador 1.
        public List<Personaje> MazoJugador
        {
            get { return mazoJugador1; }
        }

        // Mazo del oponente: la IA (vs IA) o el Jugador 2 (PvP).
        public List<Personaje> MazoIA
        {
            get { return mazoJugador2; }
        }

        // Mazo que está recibiendo cartas en este momento.
        private List<Personaje> MazoDeFase()
        {
            if (PartidaConfig.EsLocalPvP && faseJugador == 2)
                return mazoJugador2;

            return mazoJugador1;
        }

        private string NombreSelector()
        {
            if (PartidaConfig.EsLocalPvP)
                return "El Jugador " + faseJugador;

            return "El jugador";
        }

        public FormSeleccionMazo()
        {
            InitializeComponent();
            PantallaCompleta.Instalar(this);
            EscaladorInterfaz.Instalar(this);

            // El diseñador muestra el layout base sin ejecutar carga de datos ni imágenes remotas.
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            catalogo = GestorMazo.ObtenerMazoCompleto();

            Dictionary<string, string> recursos =
                GestorMazo.ObtenerUrls();

            // Cargar el reverso de la carta.
            if (recursos.TryGetValue(
                "reverso:carta", out urlReverso))
            {
                CargarReversoOriginal();
            }

            disponibles = new List<Personaje>(catalogo);

            // Filtros.
            cmbFiltro.Items.Add("Todos los personajes");

            cmbFiltro.Items.Add("Clase: Tanque");

            cmbFiltro.Items.Add("Clase: Mago");

            cmbFiltro.Items.Add("Clase: Guerrero");

            cmbFiltro.Items.Add("Facción: Homura");

            cmbFiltro.Items.Add("Facción: Mizushiro");

            cmbFiltro.Items.Add("Facción: Kazekiri");

            cmbFiltro.Items.Add("Facción: Daichi");

            cmbFiltro.SelectedIndex = 0;

            ActualizarListas();

            DibujarCatalogo();

            btnElegir.Enabled = false;

            btnRetirar.Enabled = false;

            btnComenzar.Enabled = false;

            if (PartidaConfig.EsLocalPvP)
            {
                ActualizarRotulosPvP();
                lblEstado.Text =
                    "JUGADOR 1: elige tus 5 cartas del catálogo.";
            }

            if (PartidaConfig.EsOnline)
            {
                btnComenzar.Text = "LISTO";
                lblEstado.Text =
                    "Elige tus 5 cartas. Tu rival elige las suyas en su PC.";
            }
        }

        // Título y listas según la fase actual del modo PvP.
        private void ActualizarRotulosPvP()
        {
            lblTitulo.Text =
                "SELECCIÓN - JUGADOR " + faseJugador;
        }

        // Cargar el reverso original.
        private void CargarReversoOriginal()
        {
            urlVistaPreviaActual = null;

            if (String.IsNullOrWhiteSpace(urlReverso))
                return;

            try
            {
                picReverso.LoadAsync(urlReverso);
            }
            catch
            {
                // Se conserva la imagen que estuviera cargada.
            }
        }

        // Mostrar la imagen del personaje al pasar el cursor.
        private void MostrarVistaPrevia(string url)
        {
            if (String.IsNullOrWhiteSpace(url))
                return;

            if (url == urlVistaPreviaActual)
                return;

            try
            {
                picReverso.LoadAsync(url);

                urlVistaPreviaActual = url;
            }
            catch
            {
                urlVistaPreviaActual = null;
            }
        }

        // Restaurar el reverso cuando el cursor abandona una carta.
        private void RestaurarVistaPrevia()
        {
            if (urlVistaPreviaActual == null)
                return;

            CargarReversoOriginal();
        }

        private void CmbFiltro_SelectedIndexChanged(
            object sender, EventArgs e)
        {
            DibujarCatalogo();
        }

        private void DibujarCatalogo()
        {
            // Liberar tarjetas anteriores para evitar acumular controles e imágenes.
            while (panelCatalogo.Controls.Count > 0)
            {
                Control anterior = panelCatalogo.Controls[0];
                panelCatalogo.Controls.RemoveAt(0);
                anterior.Dispose();
            }

            IEnumerable<Personaje> lista = disponibles;

            string filtro =
                cmbFiltro.SelectedItem == null
                ? "Todos los personajes"
                : cmbFiltro.SelectedItem.ToString();

            if (filtro.StartsWith("Clase: "))
            {
                string clase = filtro.Substring(7);

                lista = lista.Where(
                    p => p.Clase == clase);
            }
            else if (filtro.StartsWith("Facción: "))
            {
                string faccion = filtro.Substring(9);

                lista = lista.Where(
                    p => p.Faccion == faccion);
            }

            foreach (Personaje personaje in lista)
            {
                panelCatalogo.Controls.Add(
                    CrearCarta(personaje));
            }
        }

        private Panel CrearCarta(Personaje p)
        {
            Panel tarjeta = new Panel();

            tarjeta.Size = new Size(260, 120);

            tarjeta.Margin = new Padding(6);

            tarjeta.BackColor =
                Color.FromArgb(39, 48, 66);

            tarjeta.BorderStyle =
                BorderStyle.FixedSingle;

            tarjeta.Cursor = Cursors.Hand;

            // Nombre del personaje.
            Label nombre = new Label();

            nombre.AutoSize = false;
            nombre.Text = p.Nombre;

            nombre.Font = new Font(
                "Courier New", 11, FontStyle.Bold);

            nombre.ForeColor = Color.Gold;

            nombre.Location = new Point(8, 5);

            nombre.Size = new Size(175, 20);

            // Estadísticas.
            Label estadisticas = new Label();

            estadisticas.AutoSize = false;
            estadisticas.Text =
                p.Faccion + " - " + p.Elemento +
                "\r\nClase: " + p.Clase + "  Vida: " + p.VidaMax +
                "\r\nAtaque: " + p.Ataque + "  Defensa: " + p.Defensa +
                "\r\nManá: " + p.Mana + "  Energía: " + p.Energia +
                "\r\nEmpatía: " + p.Empatia +
                "\r\nHabilidad: " + p.HabilidadEspecial;

            estadisticas.Font =
                new Font("Courier New", 7.4F);

            estadisticas.ForeColor =
                Color.WhiteSmoke;

            estadisticas.Location =
                new Point(8, 27);

            estadisticas.Size =
                new Size(185, 80);

            // Imagen pequeña de la carta.
            PictureBox foto = new PictureBox();

            foto.Location = new Point(202, 5);

            foto.Size = new Size(50, 90);

            foto.SizeMode =
                PictureBoxSizeMode.Zoom;

            foto.BackColor =
                Color.FromArgb(25, 30, 42);

            if (!String.IsNullOrEmpty(p.ImagenUrl))
            {
                try
                {
                    foto.LoadAsync(p.ImagenUrl);
                }
                catch
                {
                    // La carta sigue funcionando aunque no cargue la imagen.
                }
            }

            // Texto inferior.
            Label elegir = new Label();

            elegir.AutoSize = false;
            elegir.Text = "CLIC PARA SELECCIONAR";

            elegir.Font = new Font(
                "Courier New", 8, FontStyle.Bold);

            elegir.ForeColor = Color.LightSkyBlue;

            elegir.Location = new Point(8, 101);

            elegir.Size = new Size(180, 15);

            // Selección de la carta.
            // Pasar el cursor no modifica cartaPendiente.
            EventHandler seleccionar = delegate(
                object sender, EventArgs e)
            {
                if (MazoDeFase().Count >= 5 && !reemplazo)
                    return;

                cartaPendiente = p;

                lblEstado.Text =
                    "Carta seleccionada: " + p.Nombre +
                    ". Pulsa Confirmar carta.";

                btnElegir.Enabled = true;
            };

            tarjeta.Click += seleccionar;

            nombre.Click += seleccionar;

            estadisticas.Click += seleccionar;

            foto.Click += seleccionar;

            elegir.Click += seleccionar;

            // Vista previa al entrar con el mouse.
            EventHandler mostrar = delegate(
                object sender, EventArgs e)
            {
                MostrarVistaPrevia(p.ImagenUrl);
            };

            // Al salir de un control, comprobamos que el cursor
            // haya abandonado realmente toda la tarjeta.
            EventHandler salir = delegate(
                object sender, EventArgs e)
            {
                Rectangle limites =
                    tarjeta.RectangleToScreen(
                        tarjeta.ClientRectangle);

                if (!limites.Contains(Cursor.Position))
                {
                    RestaurarVistaPrevia();
                }
            };

            Control[] controlesVistaPrevia =
                new Control[]
                {
                    tarjeta,
                    nombre,
                    estadisticas,
                    foto,
                    elegir
                };

            foreach (Control control in controlesVistaPrevia)
            {
                control.MouseEnter += mostrar;

                control.MouseLeave += salir;
            }

            tarjeta.Controls.Add(nombre);

            tarjeta.Controls.Add(estadisticas);

            tarjeta.Controls.Add(foto);

            tarjeta.Controls.Add(elegir);

            return tarjeta;
        }

        private void ElegirIA(Personaje referencia)
        {
            if (mazoJugador2.Count >= 5 || disponibles.Count == 0 || referencia == null)
                return;

            // La IA toma una carta del grupo que aún no eligió ninguno de los
            // dos equipos. Busca personajes de potencia parecida a la carta
            // seleccionada por el jugador para que la partida sea más justa.
            List<Personaje> opcionesIA = disponibles
                .Where(p => p.Nombre != referencia.Nombre &&
                    !mazoJugador2.Any(x => x.Nombre == p.Nombre))
                .OrderBy(p => Math.Abs(CalcularPoder(p) - CalcularPoder(referencia)))
                .Take(3)
                .ToList();

            if (opcionesIA.Count == 0)
                return;

            // Hay una pequeña variación entre las tres cartas más cercanas;
            // la IA no escoge siempre exactamente la misma combinación.
            Personaje elegido = opcionesIA[random.Next(opcionesIA.Count)];

            mazoJugador2.Add(elegido.Clonar());
            disponibles.Remove(elegido);

            gestor.Registrar(
                "La IA seleccionó " + elegido.Nombre +
                " (carta de nivel similar a " + referencia.Nombre + ").");
        }

        private int CalcularPoder(Personaje personaje)
        {
            // Pesos sencillos: vida, ataque, defensa y recursos cuentan
            // para comparar la fuerza general de dos personajes.
            return personaje.VidaMax +
                (personaje.Ataque * 3) +
                (personaje.Defensa * 2) +
                personaje.Energia +
                personaje.Mana;
        }

        private void SeleccionarPersonaje(Personaje p)
        {
            if (p == null || !disponibles.Contains(p))
                return;

            List<Personaje> mazoActual = MazoDeFase();

            if (!reemplazo && mazoActual.Count >= 5)
                return;

            mazoActual.Add(p);

            disponibles.Remove(p);

            gestor.Registrar(
                NombreSelector() + " seleccionó " + p.Nombre + ".");

            if (reemplazo)
            {
                reemplazo = false;
            }
            else if (PartidaConfig.EsVsIA)
            {
                // Solo en el modo clásico responde la IA.
                ElegirIA(p);
            }

            cartaPendiente = null;

            btnElegir.Enabled = false;

            DibujarCatalogo();

            ActualizarListas();

            if (PartidaConfig.EsOnline)
            {
                lblEstado.Text =
                    (mazoJugador1.Count == PartidaConfig.TamanoMazo)
                        ? "Mazo completo. Pulsa LISTO para enviarlo (o reemplaza una carta)."
                        : "Carta añadida (" + mazoJugador1.Count +
                          "/5). Selecciona la siguiente.";
                return;
            }

            if (PartidaConfig.EsLocalPvP)
            {
                AvanzarFasePvP();
                return;
            }

            if (mazoJugador1.Count == 5 &&
                mazoJugador2.Count == 5)
            {
                btnRetirar.Enabled = true;

                btnComenzar.Enabled = true;

                lblEstado.Text =
                    "¡Mazos completos! Puedes reemplazar una carta " +
                    "o comenzar la batalla.";
            }
            else
            {
                lblEstado.Text =
                    "Carta añadida. La IA eligió una carta de nivel parecido. " +
                    "Selecciona la siguiente.";
            }
        }

        // Control de fases del modo PvP (J1 -> aviso -> J2 -> listo).
        private void AvanzarFasePvP()
        {
            if (mazoJugador1.Count == 5 && mazoJugador2.Count == 5)
            {
                lblTitulo.Text = "SELECCIÓN DE PERSONAJES";

                lblEstado.Text =
                    "¡Ambos mazos completos! Cualquiera puede reemplazar " +
                    "una carta o pueden comenzar la batalla.";

                return;
            }

            if (faseJugador == 1 && mazoJugador1.Count == 5)
            {
                faseJugador = 2;

                ActualizarRotulosPvP();
                ActualizarListas();

                lblEstado.Text =
                    "JUGADOR 2: elige tus 5 cartas del catálogo.";

                MessageBox.Show(
                    "El Jugador 1 completó su mazo.\r\n\r\n" +
                    "Cede el control al Jugador 2: ahora elegirá sus " +
                    "5 cartas desde el catálogo restante.",
                    "Cambio de turno",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            lblEstado.Text =
                "JUGADOR " + faseJugador + ": carta añadida (" +
                MazoDeFase().Count + "/5). Selecciona la siguiente.";
        }

        private void BtnElegir_Click(
            object sender, EventArgs e)
        {
            if (cartaPendiente == null)
            {
                MessageBox.Show(
                    "Haz clic primero en una carta del catálogo.",
                    "Elegir carta");

                return;
            }

            Personaje elegida = cartaPendiente;

            SeleccionarPersonaje(elegida);
        }

        private void BtnRetirar_Click(
            object sender, EventArgs e)
        {
            if (!MazosCompletos())
                return;

            // En PvP el reemplazo puede ser de cualquiera de los dos mazos.
            if (PartidaConfig.EsLocalPvP)
            {
                if (lstMazoJugador.SelectedIndex >= 0)
                    faseJugador = 1;
                else if (lstMazoIA.SelectedIndex >= 0)
                    faseJugador = 2;
            }

            ListBox lista =
                faseJugador == 2 ? lstMazoIA : lstMazoJugador;

            if (lista.SelectedIndex < 0)
            {
                MessageBox.Show(
                    PartidaConfig.EsLocalPvP
                    ? "Selecciona primero la carta de alguno de los mazos que deseas retirar."
                    : "Selecciona primero la carta de tu mazo que deseas retirar.");

                return;
            }

            int indice =
                lista.SelectedIndex;

            List<Personaje> mazoActual = MazoDeFase();

            Personaje retirado =
                mazoActual[indice];

            mazoActual.RemoveAt(indice);

            disponibles.Add(retirado);

            reemplazo = true;

            cartaPendiente = null;

            btnComenzar.Enabled = false;

            btnElegir.Enabled = false;

            btnRetirar.Enabled = false;

            if (PartidaConfig.EsLocalPvP)
                ActualizarRotulosPvP();

            lblEstado.Text =
                (PartidaConfig.EsLocalPvP
                    ? "El Jugador " + faseJugador + " retiró "
                    : "Retiraste ") + retirado.Nombre +
                ". Selecciona una carta disponible para reemplazarla.";

            DibujarCatalogo();

            ActualizarListas();
        }

        private void BtnComenzar_Click(
            object sender, EventArgs e)
        {
            if (PartidaConfig.EsOnline)
            {
                // 1.er clic: LISTO (envía mi mazo).
                if (!yoListo)
                {
                    EnviarMiMazo();
                    return;
                }

                // 2.º clic: COMENZAR BATALLA (cuando ya veo el mazo rival).
                if (mazoJugador2.Count != PartidaConfig.TamanoMazo || yoComienzo)
                    return;

                yoComienzo = true;
                btnComenzar.Enabled = false;
                lblEstado.Text = "Esperando a que el rival comience...";
                PartidaConfig.Red.Enviar("INICIO");
                CerrarSiAmbosComienzan();
                return;
            }

            if (mazoJugador1.Count != 5 ||
                mazoJugador2.Count != 5)
            {
                MessageBox.Show(
                    "Cada equipo debe tener cinco cartas.");

                return;
            }

            DialogResult = DialogResult.OK;

            Close();
        }

        // Volver al menú de modo de juego.
        private void BtnVolverModo_Click(
            object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;

            Close();
        }

        private void ActualizarListas()
        {
            lstMazoJugador.Items.Clear();

            foreach (Personaje p in mazoJugador1)
            {
                lstMazoJugador.Items.Add(
                    p.Nombre + " - " + p.Clase +
                    " - " + p.Elemento);
            }

            lstMazoIA.Items.Clear();

            foreach (Personaje p in mazoJugador2)
            {
                lstMazoIA.Items.Add(
                    p.Nombre + " - " + p.Clase +
                    " - " + p.Elemento);
            }

            if (PartidaConfig.EsLocalPvP)
            {
                lblJugador.Text =
                    "JUGADOR 1 (" + mazoJugador1.Count + "/5)";

                lblIA.Text =
                    "JUGADOR 2 (" + mazoJugador2.Count + "/5)";
            }
            else
            {
                // VS IA -> "MAZO IA (n/5)"   ONLINE -> "MAZO RIVAL (n/5)"
                lblJugador.Text =
                    PartidaConfig.EtiquetaMazoPropio(mazoJugador1.Count);

                lblIA.Text =
                    PartidaConfig.EtiquetaMazoOponente(mazoJugador2.Count);
            }

            bool completo = MazosCompletos() && !reemplazo;

            btnRetirar.Enabled = completo;

            btnComenzar.Enabled =
                completo ||
                (PartidaConfig.EsOnline && yoListo && !yoComienzo &&
                 mazoJugador2.Count == PartidaConfig.TamanoMazo);
        }

        // En línea basta con mi mazo (y antes de enviarlo); en los demás modos, los dos.
        private bool MazosCompletos()
        {
            if (PartidaConfig.EsOnline)
                return mazoJugador1.Count == PartidaConfig.TamanoMazo && !yoListo;

            return mazoJugador1.Count == PartidaConfig.TamanoMazo &&
                   mazoJugador2.Count == PartidaConfig.TamanoMazo;
        }

        // ---------- MODO EN LÍNEA ----------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!PartidaConfig.EsOnline)
                return;

            GestorRed red = PartidaConfig.Red;
            red.MensajeDisponible += Red_MensajeDisponible;
            red.ErrorOcurrido += Red_ErrorOcurrido;
            red.Desconectado += Red_Desconectado;

            if (!red.EstaConectado)
            {
                Abortar("Se perdió la conexión con el oponente.");
                return;
            }

            ProcesarBandejaSeleccion();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            GestorRed red = PartidaConfig.Red;
            if (red != null)
            {
                red.MensajeDisponible -= Red_MensajeDisponible;
                red.ErrorOcurrido -= Red_ErrorOcurrido;
                red.Desconectado -= Red_Desconectado;
            }

            base.OnFormClosed(e);
        }

        // Eventos de red (hilo secundario) -> hilo de la interfaz.
        private void Red_MensajeDisponible(object sender, EventArgs e)
        {
            EnInterfaz(ProcesarBandejaSeleccion);
        }

        private void Red_ErrorOcurrido(object sender, ErrorRedEventArgs e)
        {
            string motivo = "Error de red: " + e.Mensaje;
            EnInterfaz(delegate { Abortar(motivo); });
        }

        private void Red_Desconectado(object sender, EventArgs e)
        {
            EnInterfaz(delegate { Abortar("Tu oponente se desconectó."); });
        }

        // BeginInvoke: el hilo de red nunca espera a la interfaz.
        private void EnInterfaz(MethodInvoker accion)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(accion);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        // Solo consume MAZO e INICIO; cualquier otro mensaje (ATAQUE...) se
        // queda en la bandeja para que lo procese FormBatalla.
        private void ProcesarBandejaSeleccion()
        {
            GestorRed red = PartidaConfig.Red;
            MensajeRed m;

            while (!cerrando && red != null && red.TryPeek(out m))
            {
                if (m.Tipo != ProtocoloMazo.TipoMensaje && m.Tipo != "INICIO")
                    break;

                red.TryLeer(out m);

                if (m.Tipo == "INICIO")
                {
                    rivalComienzo = true;
                }
                else
                {
                    List<Personaje> rival;
                    string error;

                    if (!ProtocoloMazo.TryDeserializar(m.Datos, out rival, out error))
                    {
                        Abortar(error);
                        return;
                    }

                    mazoRivalPendiente = rival;
                }
            }

            RevelarRival();
            CerrarSiAmbosComienzan();
        }

        private void EnviarMiMazo()
        {
            if (mazoJugador1.Count != PartidaConfig.TamanoMazo || yoListo)
                return;

            yoListo = true;
            btnElegir.Enabled = false;
            btnRetirar.Enabled = false;
            btnComenzar.Enabled = false;

            PartidaConfig.Red.Enviar(
                ProtocoloMazo.TipoMensaje,
                ProtocoloMazo.Serializar(mazoJugador1));

            lblEstado.Text = "Mazo enviado. Esperando al rival...";
            RevelarRival();
        }

        // El mazo rival solo se muestra cuando el mío ya salió (juego limpio).
        private void RevelarRival()
        {
            if (!yoListo || mazoRivalPendiente == null || mazoJugador2.Count > 0)
                return;

            mazoJugador2.Clear();
            mazoJugador2.AddRange(mazoRivalPendiente);

            ActualizarListas();   // lblIA = "MAZO RIVAL (5/5)"

            btnComenzar.Text = "COMENZAR BATALLA";
            btnComenzar.Enabled = true;
            lblEstado.Text = "¡Mazo rival recibido! Pulsa COMENZAR BATALLA.";
        }

        private void CerrarSiAmbosComienzan()
        {
            if (cerrando || !yoComienzo || !rivalComienzo)
                return;

            cerrando = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Abortar(string motivo)
        {
            if (cerrando)
                return;

            cerrando = true;

            MessageBox.Show(
                motivo,
                "Chronos Arena",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ActualizarEstado()
        {
            lblEstado.Text =
                "Mazos completos. Puedes comenzar la batalla.";
        }
		void LstMazoIASelectedIndexChanged(object sender, EventArgs e)
		{
	
		}
    }
}