using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace ServidorPPT
{
    internal class Program
    {
        // Puerto TCP en el que escucha el servidor
        private const int PUERTO = 5000;

        // Lista que almacena las conexiones TCP de los jugadores
        private static List<TcpClient> jugadores = new List<TcpClient>();

        // Diccionario que relaciona cada conexión con el nombre del jugador
        private static Dictionary<TcpClient, string> nombres = new Dictionary<TcpClient, string>();

        // Diccionario que almacena los puntos de cada jugador
        private static Dictionary<TcpClient, int> puntuaciones = new Dictionary<TcpClient, int>();

        // Diccionario que guarda las jugadas realizadas
        private static Dictionary<TcpClient, string> jugadas = new Dictionary<TcpClient, string>();

        // Objeto utilizado como candado para proteger las listas y diccionarios compartidos entre tareas
        private static readonly object candado = new object();

        // Contador que identifica cada partida.
        private static int numeroPartida = 0;

        // Almacena el resultado de la última partida
        private static string ultimoResultado = "RESULTADO|Todavía no se ha jugado ninguna partida.";

        static void Main(string[] args)
        {
            // Creamos el servidor TCP
            TcpListener servidor = new TcpListener(IPAddress.Any, PUERTO);

            try
            {
                // Ponemos el servidor a escuchar conexiones.
                servidor.Start();

                Console.WriteLine("================================");
                Console.WriteLine("   SERVIDOR PIEDRA PAPEL TIJERA");
                Console.WriteLine("================================");
                Console.WriteLine("Servidor iniciado.");
                Console.WriteLine("Esperando jugadores...");
                Console.WriteLine("Puerto: " + PUERTO);

                while (true)
                {
                    // AcceptTcpClient espera hasta que se conecta un cliente
                    TcpClient cliente = servidor.AcceptTcpClient();

                    bool aceptado = false;

                    // Protegemos el acceso a la lista de jugadores
                    lock (candado)
                    {
                        // El juego admite como máximo dos jugadores
                        if (jugadores.Count < 2)
                        {
                            jugadores.Add(cliente);
                            aceptado = true;
                        }
                    }

                    if (aceptado)
                    {
                        Console.WriteLine("\nNuevo cliente conectado.");

                        // Ejecutamos la atención del cliente en una tarea independiente
                        Task.Run(() => AtenderCliente(cliente));
                    }
                    else
                    {
                        // Si ya hay dos jugadores, rechazamos la conexión.
                        Console.WriteLine("\nConexión rechazada: máximo de 2 jugadores.");

                        // Enviamos al cliente un mensaje indicando que el servidor está lleno
                        using (NetworkStream flujo = cliente.GetStream())
                        using (StreamWriter escritor = new StreamWriter(flujo, new UTF8Encoding(false)))
                        {
                            escritor.AutoFlush = true;
                            escritor.WriteLine("ERROR|LLENO");
                        }

                        cliente.Close();
                    }
                }
            }
            catch (SocketException ex)
            {
                // Capturamos posibles errores relacionados con los sockets
                Console.WriteLine("Error de conexión: " + ex.Message);
            }
            finally
            {
                // Detenemos el servidor y liberamos el puerto
                servidor.Stop();
            }
        }

        // MÉTODO QUE ATIENDE A CADA JUGADOR
        private static void AtenderCliente(TcpClient cliente)
        {
            try
            {
                using (NetworkStream flujo = cliente.GetStream())

                using (StreamReader lector = new StreamReader(flujo, new UTF8Encoding(false)))
                using (StreamWriter escritor = new StreamWriter(flujo, new UTF8Encoding(false)))
                {
                    // Enviamos cada mensaje inmediatamente
                    escritor.AutoFlush = true;

                    // Recibimos el nombre del jugador
                    string nombre = lector.ReadLine();

                    // Comprobamos que el nombre no esté vacío
                    if (string.IsNullOrWhiteSpace(nombre))
                    {
                        Console.WriteLine("Un cliente se ha desconectado sin registrarse.");
                        return;
                    }

                    // Guardamos el nombre y la puntuación inicial del jugador
                    lock (candado)
                    {
                        nombres[cliente] = nombre;
                        puntuaciones[cliente] = 0;
                    }

                    Console.WriteLine("Jugador registrado: " + nombre);

                    // Confirmamos al cliente que el registro ha sido correcto
                    escritor.WriteLine("OK|REGISTRO");

                    // Bucle que recibe y procesa continuamente las peticiones del jugador
                    string mensaje;

                    while ((mensaje = lector.ReadLine()) != null)
                    {
                        Console.WriteLine(nombre + " envía: " + mensaje);

                        // JUGAR
                        if (mensaje.StartsWith("JUGAR|"))
                        {
                            // Separamos el comando de la jugada
                            string[] partes = mensaje.Split('|');

                            if (partes.Length == 2)
                            {
                                // Convertimos la jugada a mayúsculas
                                string jugada = partes[1].ToUpper();

                                // Procesamos la jugada y esperamos a que se complete la partida con el otro jugador
                                string resultado = ProcesarJugada(cliente, jugada);

                                // Enviamos la respuesta al cliente
                                escritor.WriteLine(resultado);
                            }
                            else
                            {
                                // Respondemos si el mensaje no tiene el formato esperado
                                escritor.WriteLine("ERROR|Formato incorrecto.");
                            }
                        }

                        // PUNTUACIONES
                        else if (mensaje == "PUNTUACION")
                        {
                            // Protegemos los datos mientras se consultan
                            lock (candado)
                            {
                                // Creamos el mensaje que se enviará al cliente
                                string respuesta = "PUNTUACION|";

                                // Recorremos todos los jugadores conectados
                                foreach (TcpClient jugador in jugadores)
                                {
                                    // Comprobamos que el jugador tenga un nombre registrado
                                    if (nombres.ContainsKey(jugador))
                                    {
                                        // Añadimos nombre y puntos al mensaje
                                        respuesta += nombres[jugador] + ": " + puntuaciones[jugador] + " puntos; ";
                                    }
                                }

                                // Enviamos todas las puntuaciones al cliente que las solicitó
                                escritor.WriteLine(respuesta);
                            }
                        }

                        // RESULTADO
                        else if (mensaje == "RESULTADO")
                        {
                            // Protegemos el acceso al resultado compartido
                            lock (candado)
                            {
                                // Enviamos el resultado de la última partida
                                escritor.WriteLine(ultimoResultado);
                            }
                        }

                        // SALIR DEL JUEGO
                        else if (mensaje == "SALIR")
                        {
                            // Confirmamos la salida
                            escritor.WriteLine("OK|SALIR");

                            // Salimos del bucle y finalizamos la atención del cliente
                            break;
                        }

                        // COMANDO NO RECONOCIDO
                        else
                        {
                            escritor.WriteLine("ERROR|COMANDO");
                        }
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
                // Eliminamos al jugador de las estructuras compartidas.
                lock (candado)
                {
                    jugadores.Remove(cliente);
                    nombres.Remove(cliente);
                    puntuaciones.Remove(cliente);
                    jugadas.Remove(cliente);

                    // Avisamos a los hilos que estén esperando a que se complete una partida.
                    Monitor.PulseAll(candado);
                }

                // Cerramos la conexión TCP del jugador
                cliente.Close();

                Console.WriteLine("Un jugador se ha desconectado.");

                // Mostramos cuántos jugadores siguen conectados
                Console.WriteLine("Jugadores conectados: " + ObtenerNumeroJugadores());
            }
        }

        // MÉTODO QUE DEVUELVE EL NÚMERO DE JUGADORES CONECTADOS
        private static int ObtenerNumeroJugadores()
        {
            // Bloqueamos el acceso mientras consultamos la lista
            lock (candado)
            {
                return jugadores.Count;
            }
        }

        // MÉTODO QUE PROCESA LAS JUGADAS Y CALCULA EL RESULTADO
        private static string ProcesarJugada(
            TcpClient cliente, string jugada)
        {
            // Bloqueamos el acceso porque varios clientes pueden ejecutar este método simultáneamente
            lock (candado)
            {
                // Comprobamos que haya dos jugadores conectados
                if (jugadores.Count < 2)
                {
                    return "ERROR|Se necesitan dos jugadores.";
                }

                // Validamos que la jugada sea una de las tres permitidas
                if (jugada != "PIEDRA" && jugada != "PAPEL" && jugada != "TIJERA")
                {
                    return "ERROR|Jugada no válida.";
                }

                // Calculamos el número de la partida que se va a jugar
                int partidaSolicitada = numeroPartida + 1;

                // Guardamos la jugada del jugador que ha enviado el mensaje
                jugadas[cliente] = jugada;

                Console.WriteLine(
                    nombres[cliente] + " ha elegido " + jugada);

                // Si solo hay una jugada, esperamos al segundo jugador.
                if (jugadas.Count < 2)
                {
                    Console.WriteLine("Esperando la jugada del otro jugador...");

                    // Monitor.Wait libera temporalmente el candado para permitir que el otro cliente pueda entrar
                    while (jugadas.Count < 2 && numeroPartida < partidaSolicitada && jugadores.Contains(cliente))
                    {
                        // Esperamos 500 milisegundos antes de volver a comprobar
                        Monitor.Wait(candado, 500);

                        // Comprobamos si el cliente ha cerrado la conexión
                        if (ConexionCerrada(cliente))
                        {
                            jugadores.Remove(cliente);
                            nombres.Remove(cliente);
                            puntuaciones.Remove(cliente);
                            jugadas.Remove(cliente);

                            // Despertamos a los posibles hilos que estén esperando
                            Monitor.PulseAll(candado);

                            return "ERROR|El jugador se ha desconectado.";
                        }
                    }
                }

                // Comprobamos que ambos jugadores sigan conectados
                if (!jugadores.Contains(cliente) || jugadores.Count < 2)
                {
                    return "ERROR|No se ha podido completar la partida.";
                }

                // Si hay dos jugadas pendientes, podemos resolver la partida
                if (numeroPartida < partidaSolicitada && jugadas.Count == 2)
                {
                    // Obtenemos los dos participantes
                    TcpClient[] participantes = jugadores.ToArray();

                    TcpClient jugador1 = participantes[0];
                    TcpClient jugador2 = participantes[1];

                    // Recuperamos las jugadas realizadas por cada participante
                    string jugada1 = jugadas[jugador1];
                    string jugada2 = jugadas[jugador2];

                    string resultado;

                    // COMPROBACIÓN DE EMPATE
                    if (jugada1 == jugada2)
                    {
                        resultado = "Empate.";
                    }
                    else
                    {
                        // Aplicamos las reglas del juego:
                        bool ganaJugador1 =
                            (jugada1 == "PIEDRA" && jugada2 == "TIJERA") ||
                            (jugada1 == "PAPEL" && jugada2 == "PIEDRA") ||
                            (jugada1 == "TIJERA" && jugada2 == "PAPEL");

                        // Actualizamos la puntuación del ganador.
                        if (ganaJugador1)
                        {
                            puntuaciones[jugador1]++;
                            resultado = "Ganador: " + nombres[jugador1];
                        }
                        else
                        {
                            puntuaciones[jugador2]++;
                            resultado = "Ganador: " + nombres[jugador2];
                        }
                    }

                    // Incrementamos el contador de partidas
                    numeroPartida = partidaSolicitada;

                    // Construimos y guardamos el resultado completo
                    ultimoResultado = "RESULTADO|Partida " + numeroPartida +
                                    " - " + nombres[jugador1] + ": " + jugada1 +
                                    " - " + nombres[jugador2] + ": " + jugada2 +
                                    " - " + resultado;

                    // Mostramos el resultado en la consola del servidor
                    Console.WriteLine("\n" + ultimoResultado);

                    // Eliminamos las dos jugadas para preparar la siguiente partida
                    jugadas.Remove(jugador1);
                    jugadas.Remove(jugador2);

                    // Despertamos al primer jugador que estaba esperando
                    Monitor.PulseAll(candado);
                }

                // Devolvemos el resultado al método AtenderCliente
                return ultimoResultado;
            }
        }

        // MÉTODO QUE COMPRUEBA SI UN CLIENTE HA CERRADO SU CONEXIÓN
        private static bool ConexionCerrada(TcpClient cliente)
        {
            try
            {
                // Si está preparado y Available es 0, significa que el otro extremo ha cerrado la conexión
                return cliente.Client.Poll(0, SelectMode.SelectRead) && cliente.Client.Available == 0;
            }
            catch
            {
                // Si se produce un error al consultar el socket, consideramos que la conexión está cerrada
                return true;
            }
        }
    }
}