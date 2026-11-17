using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServidorPPT
{
    internal class Program
    {
        // Puerto TCP utilizado por el servidor
        private const int PUERTO = 5000;

        // Número máximo de jugadores
        private const int MAXIMO_JUGADORES = 2;

        // Lista de conexiones de los jugadores
        private static readonly List<TcpClient> jugadores = new List<TcpClient>();

        // Relaciona cada conexión con su nombre
        private static readonly Dictionary<TcpClient, string> nombres = new Dictionary<TcpClient, string>();

        // Almacena los puntos de cada jugador
        private static readonly Dictionary<TcpClient, int> puntuaciones = new Dictionary<TcpClient, int>();

        // Almacena las jugadas de la partida actual
        private static readonly Dictionary<TcpClient, string> jugadas = new Dictionary<TcpClient, string>();

        // Candado para proteger los datos compartidos
        private static readonly object candado = new object();

        // Número de partidas realizadas
        private static int numeroPartida = 0;

        // Resultado de la última partida
        private static string ultimoResultado = "RESULTADO|Todavía no se ha jugado ninguna partida.";

        static void Main(string[] args)
        {
            TcpListener servidor =
                new TcpListener(IPAddress.Any, PUERTO);

            try
            {
                // Iniciamos el servidor
                servidor.Start();

                MostrarEstadoServidor();

                // Esperamos continuamente nuevas conexiones
                while (true)
                {
                    TcpClient cliente = servidor.AcceptTcpClient();

                    AceptarCliente(cliente);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Error de conexión: " + ex.Message);
            }
            finally
            {
                servidor.Stop();
                Console.WriteLine("Servidor detenido.");
            }
        }

        // Muestra información del servidor
        private static void MostrarEstadoServidor()
        {
            Console.WriteLine("================================");
            Console.WriteLine("   SERVIDOR PIEDRA PAPEL TIJERA");
            Console.WriteLine("================================");
            Console.WriteLine("Servidor iniciado.");
            Console.WriteLine("Esperando jugadores...");
            Console.WriteLine("Puerto: " + PUERTO);
        }

        // Acepta nuevos clientes
        private static void AceptarCliente(TcpClient cliente)
        {
            bool aceptado = false;

            // Comprobamos si hay plazas disponibles
            lock (candado)
            {
                if (jugadores.Count < MAXIMO_JUGADORES)
                {
                    jugadores.Add(cliente);
                    aceptado = true;
                }
            }

            if (aceptado)
            {
                Console.WriteLine("\nNuevo cliente conectado.");

                Console.WriteLine("Jugadores conectados: " + ObtenerNumeroJugadores());

                // Atendemos al cliente en una tarea independiente
                Task.Run(() => AtenderCliente(cliente));
            }
            else
            {
                RechazarCliente(cliente);
            }
        }

        // Rechaza a los clientes si el servidor esta lleno
        private static void RechazarCliente(TcpClient cliente)
        {
            Console.WriteLine("\nConexión rechazada: máximo de 2 jugadores.");

            try
            {
                using (NetworkStream flujo = cliente.GetStream())
                using (StreamWriter escritor =
                    new StreamWriter(flujo, new UTF8Encoding(false)))
                {
                    escritor.AutoFlush = true;

                    escritor.WriteLine("ERROR|LLENO");
                }
            }
            finally
            {
                cliente.Close();
            }
        }

        // Atiende a cada jugador
        private static void AtenderCliente(TcpClient cliente)
        {
            try
            {
                using (NetworkStream flujo = cliente.GetStream())
                using (StreamReader lector = new StreamReader(flujo, new UTF8Encoding(false)))
                using (StreamWriter escritor = new StreamWriter(flujo, new UTF8Encoding(false)))
                {
                    escritor.AutoFlush = true;

                    // Registramos al jugador
                    if (!RegistrarJugador(cliente, lector, escritor))
                        return;

                    string mensaje;

                    // Recibimos mensajes continuamente
                    while ((mensaje = lector.ReadLine()) != null)
                    {
                        Console.WriteLine(nombres[cliente] + " envía: " + mensaje);

                        // Procesamos la petición recibida
                        bool continuar = ProcesarMensaje(cliente, mensaje, escritor);

                        if (!continuar)
                            break;
                    }
                }
            }
            catch (IOException)
            {
                Console.WriteLine("Se ha perdido la conexión con un jugador.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error atendiendo al jugador: " + ex.Message);
            }
            finally
            {
                // Eliminamos al jugador cuando termina su conexión.
                EliminarJugador(cliente);
            }
        }

        // Registra cada jugador
        private static bool RegistrarJugador(TcpClient cliente, StreamReader lector, StreamWriter escritor)
        {
            // Recibimos el nombre enviado por el cliente
            string nombre = lector.ReadLine();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                Console.WriteLine("Un cliente se ha desconectado sin registrarse.");
                return false;
            }

            // Guardamos el nombre y su puntuación inicial
            lock (candado)
            {
                nombres[cliente] = nombre;
                puntuaciones[cliente] = 0;
            }

            Console.WriteLine("Jugador registrado: " + nombre);

            // Confirmamos el registro al cliente.
            escritor.WriteLine("OK|REGISTRO");

            return true;
        }

        // Procesa los mensajes del cliente
        private static bool ProcesarMensaje(TcpClient cliente, string mensaje, StreamWriter escritor)
        {
            if (mensaje.StartsWith("JUGAR|"))
            {
                ProcesarPeticionJugar(cliente, mensaje, escritor);
                return true;
            }
            else if (mensaje == "PUNTUACION")
            {
                escritor.WriteLine(ConsultarPuntuaciones());
                return true;
            }
            else if (mensaje == "RESULTADO")
            {
                escritor.WriteLine(ObtenerUltimoResultado());
                return true;
            }
            else if (mensaje == "SALIR")
            {
                escritor.WriteLine("OK|SALIR");

                // Indicamos que debe terminar la comunicación
                return false;
            }
            else
            {
                escritor.WriteLine("ERROR|COMANDO");
                return true;
            }
        }

        // Procesa la petición de jugar
        private static void ProcesarPeticionJugar(TcpClient cliente, string mensaje, StreamWriter escritor)
        {
            // Separamos el comando y la jugada.
            string[] partes = mensaje.Split('|');

            if (partes.Length != 2)
            {
                escritor.WriteLine("ERROR|Formato incorrecto.");
                return;
            }

            string jugada = partes[1].ToUpper();

            // Procesamos la jugada
            string resultado = ProcesarJugada(cliente, jugada);

            // Enviamos la respuesta al cliente
            escritor.WriteLine(resultado);
        }

        // Consulta las puntuaciones
        private static string ConsultarPuntuaciones()
        {
            lock (candado)
            {
                StringBuilder respuesta = new StringBuilder("PUNTUACION|");

                foreach (TcpClient jugador in jugadores)
                {
                    if (nombres.ContainsKey(jugador) && puntuaciones.ContainsKey(jugador))
                    {
                        respuesta.Append(nombres[jugador]).Append(": ").Append(puntuaciones[jugador]).Append(" puntos; ");
                    }
                }

                return respuesta.ToString();
            }
        }

        // Consulta el último resultado
        private static string ObtenerUltimoResultado()
        {
            lock (candado)
            {
                return ultimoResultado;
            }
        }

        // Procesa las jugadas
        private static string ProcesarJugada(
            TcpClient cliente,
            string jugada)
        {
            lock (candado)
            {
                // Comprobamos que haya dos jugadores
                if (jugadores.Count < MAXIMO_JUGADORES)
                {
                    return "ERROR|Se necesitan dos jugadores.";
                }

                // Validamos la jugada recibida
                if (!EsJugadaValida(jugada))
                {
                    return "ERROR|Jugada no válida.";
                }

                int partidaSolicitada = numeroPartida + 1;

                // Guardamos la jugada del jugador
                jugadas[cliente] = jugada;

                Console.WriteLine(nombres[cliente] + " ha elegido " + jugada);

                // Si falta la jugada del otro jugador, esperamos
                // Mostramos el mensaje una sola vez
                Console.WriteLine("Esperando la jugada del otro jugador...");

                while (jugadas.Count < MAXIMO_JUGADORES &&
                       numeroPartida < partidaSolicitada &&
                       jugadores.Contains(cliente))
                {
                    // Esperamos a que llegue la jugada del segundo jugador
                    Monitor.Wait(candado, 500);

                    if (ConexionCerrada(cliente))
                    {
                        QuitarDatosJugador(cliente);

                        Monitor.PulseAll(candado);

                        return "ERROR|El jugador se ha desconectado.";
                    }
                }

                // Comprobamos que ambos jugadores siguen conectados
                if (!jugadores.Contains(cliente) || jugadores.Count < MAXIMO_JUGADORES)
                {
                    return "ERROR|No se ha podido completar la partida.";
                }

                // Si tenemos las dos jugadas, resolvemos la partida
                if (numeroPartida < partidaSolicitada && jugadas.Count == MAXIMO_JUGADORES)
                {
                    ResolverPartida();

                    // Despertamos al jugador que estaba esperando
                    Monitor.PulseAll(candado);
                }

                return ultimoResultado;
            }
        }

        // Valida la jugada
        private static bool EsJugadaValida(string jugada)
        {
            return jugada == "PIEDRA" ||
                   jugada == "PAPEL" ||
                   jugada == "TIJERA";
        }

        // Resuelve la partida
        private static void ResolverPartida()
        {
            // Obtenemos los dos jugadores
            TcpClient[] participantes = jugadores.ToArray();

            TcpClient jugador1 = participantes[0];
            TcpClient jugador2 = participantes[1];

            // Recuperamos sus jugadas
            string jugada1 = jugadas[jugador1];
            string jugada2 = jugadas[jugador2];

            // Calculamos quién ha ganado
            string resultado = CalcularGanador(jugador1, jugador2, jugada1, jugada2);

            // Guardamos el resultado.
            GuardarResultado(jugador1, jugador2, jugada1, jugada2, resultado);

            // Eliminamos las jugadas para la siguiente partida.
            jugadas.Remove(jugador1);
            jugadas.Remove(jugador2);
        }

        // Calcula el ganador
        private static string CalcularGanador(TcpClient jugador1, TcpClient jugador2, string jugada1, string jugada2)
        {
            // Comprobamos si existe empate.
            if (jugada1 == jugada2)
            {
                return "Empate.";
            }

            // Aplicamos las reglas del juego.
            bool ganaJugador1 =
                (jugada1 == "PIEDRA" && jugada2 == "TIJERA") ||
                (jugada1 == "PAPEL" && jugada2 == "PIEDRA") ||
                (jugada1 == "TIJERA" && jugada2 == "PAPEL");

            if (ganaJugador1)
            {
                puntuaciones[jugador1]++;

                return "Ganador: " + nombres[jugador1];
            }

            puntuaciones[jugador2]++;

            return "Ganador: " + nombres[jugador2];
        }

        // Guarda el resultado
        private static void GuardarResultado(TcpClient jugador1, TcpClient jugador2, string jugada1, string jugada2, string resultado)
        {
            // Incrementamos el número de partida
            numeroPartida++;

            // Construimos el mensaje del resultado
            ultimoResultado =
                "RESULTADO|Partida " + numeroPartida +
                " - " + nombres[jugador1] + ": " + jugada1 +
                " - " + nombres[jugador2] + ": " + jugada2 +
                " - " + resultado;

            Console.WriteLine("\n" + ultimoResultado);
        }

        // Elimina jugador
        private static void EliminarJugador(TcpClient cliente)
        {
            lock (candado)
            {
                QuitarDatosJugador(cliente);

                // Despertamos a los posibles jugadores que esperan
                Monitor.PulseAll(candado);
            }

            cliente.Close();

            Console.WriteLine("Un jugador se ha desconectado.");

            Console.WriteLine("Jugadores conectados: " + ObtenerNumeroJugadores());
        }
       
        // Quita los datos del servidor
        // Este método se ejecuta teniendo adquirido el candado
        private static void QuitarDatosJugador(TcpClient cliente)
        {
            jugadores.Remove(cliente);
            nombres.Remove(cliente);
            puntuaciones.Remove(cliente);
            jugadas.Remove(cliente);
        }

        // Cuenta los jugadores conectados
        private static int ObtenerNumeroJugadores()
        {
            lock (candado)
            {
                return jugadores.Count;
            }
        }

        // Comprueba si un cliente se ha desconectado
        private static bool ConexionCerrada(TcpClient cliente)
        {
            try
            {
                return cliente.Client.Poll(
                    0, SelectMode.SelectRead) &&
                    cliente.Client.Available == 0;
            }
            catch
            {
                return true;
            }
        }
    }
}