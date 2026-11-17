using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace ClientePPT
{
    internal class Program
    {
        private const string SERVIDOR = "127.0.0.1";
        private const int PUERTO = 5000;

        static void Main(string[] args)
        {
            MostrarCabecera();

            try
            {
                using (TcpClient cliente = new TcpClient())
                {
                    Console.WriteLine();
                    Console.WriteLine("Socket Cliente creado");

                    cliente.Connect(SERVIDOR, PUERTO);

                    Console.WriteLine("Conexión establecida.");

                    using (NetworkStream flujo = cliente.GetStream())
                    using (StreamReader lector = new StreamReader(flujo, new UTF8Encoding(false)))
                    using (StreamWriter escritor = new StreamWriter(flujo, new UTF8Encoding(false)))
                    {
                        escritor.AutoFlush = true;

                        Console.WriteLine("Flujos de lectura y escritura creados.");

                        if (!RegistrarJugador(lector, escritor))
                            return;

                        GestionarMenu(lector, escritor);
                    }
                }
            }
            catch (SocketException)
            {
                Console.WriteLine("\nNo se ha podido conectar con el servidor.");
                Console.WriteLine("Comprueba que el servidor está iniciado.");
            }
            catch (IOException ex)
            {
                Console.WriteLine("\nError de comunicación: " + ex.Message);
            }

            Console.WriteLine("\nPulsa una tecla para salir.");
            Console.ReadKey();
        }

        // Muestra el título del juego
        private static void MostrarCabecera()
        {
            Console.WriteLine("===============================");
            Console.WriteLine("   JUEGO PIEDRA PAPEL TIJERA");
            Console.WriteLine("===============================");
            Console.WriteLine();
        }

        // Registra al jugador en el servidor
        private static bool RegistrarJugador(
            StreamReader lector, StreamWriter escritor)
        {
            Console.WriteLine("Indica el nombre con el que quiere inscribirse en el juego:");
            string nombre = Console.ReadLine();

            escritor.WriteLine(nombre);

            string respuesta = lector.ReadLine();

            if (respuesta == "ERROR|LLENO")
            {
                Console.WriteLine("El servidor está completo.");
                return false;
            }

            if (respuesta != "OK|REGISTRO")
            {
                Console.WriteLine("La inscripción no ha sido correcta.");
                return false;
            }

            Console.WriteLine("\nLa inscripción fur correcta.");
            Console.WriteLine("Bienvenido al juego, " + nombre);

            return true;
        }

        // Gestiona las opciones del menú principal
        private static void GestionarMenu(
            StreamReader lector, StreamWriter escritor)
        {
            bool continuar = true;

            while (continuar)
            {
                MostrarMenu();

                string opcion = Console.ReadLine();

                switch (opcion)
                {
                    case "1":
                        Jugar(lector, escritor);
                        break;

                    case "2":
                        ConsultarPuntuaciones(lector, escritor);
                        break;

                    case "3":
                        MostrarResultado(lector, escritor);
                        break;

                    case "-1":
                        SalirJuego(lector, escritor);
                        continuar = false;
                        break;

                    default:
                        Console.WriteLine("Opción incorrecta.");
                        break;
                }
            }
        }

        // Muestra las opciones disponibles
        private static void MostrarMenu()
        {
            Console.WriteLine();
            Console.WriteLine("JUEGA PIEDRA, PAPEL O TIJERA");
            Console.WriteLine("===========================");
            Console.WriteLine("1: Jugar");
            Console.WriteLine("2: Puntuación");
            Console.WriteLine("3: Mostrar resultado");
            Console.WriteLine("-1: Salir");
            Console.WriteLine("===========================");
        }

        // Solicita la jugada y la envía al servidor
        private static void Jugar(StreamReader lector, StreamWriter escritor)
        {
            Console.WriteLine("\nJUGAR");
            Console.WriteLine("1: Piedra");
            Console.WriteLine("2: Papel");
            Console.WriteLine("3: Tijera");

            Console.Write("Escoge tu jugada: ");

            string eleccion = Console.ReadLine();

            string jugada = ConvertirEleccion(eleccion);

            if (jugada == null)
            {
                Console.WriteLine("Opción incorrecta.");
                return;
            }

            escritor.WriteLine("JUGAR|" + jugada);

            // Esperamos la respuesta del servidor
            string respuesta = lector.ReadLine();

            if (respuesta == null)
            {
                Console.WriteLine("El servidor ha cerrado la conexión.");
                return;
            }

            if (respuesta.StartsWith("RESULTADO|"))
            {
                string datos = respuesta.Substring("RESULTADO|".Length);

                string[] partes = datos.Split(' ');

                if (partes.Length > 1)
                    Console.WriteLine("Número de partida: " + partes[1]);
                else
                    Console.WriteLine(respuesta);
            }
            else
            {
                Console.WriteLine(respuesta);
            }
        }

        // Convierte la opción numérica en el nombre de la jugada
        private static string ConvertirEleccion(string eleccion)
        {
            switch (eleccion)
            {
                case "1": return "PIEDRA";
                case "2": return "PAPEL";
                case "3": return "TIJERA";

                default: return null;
            }
        }

        // Solicita las puntuaciones de los jugadores
        private static void ConsultarPuntuaciones(StreamReader lector, StreamWriter escritor)
        {
            Console.WriteLine("Has escogido puntuación");
            escritor.WriteLine("PUNTUACION");

            string respuesta = lector.ReadLine();

            if (respuesta == null)
            {
                Console.WriteLine("El servidor ha cerrado la conexión.");
                return;
            }

            if (!respuesta.StartsWith("PUNTUACION|"))
            {
                Console.WriteLine(respuesta);
                return;
            }

            string datos = respuesta.Substring("PUNTUACION|".Length);

            string[] jugadores = datos.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string jugador in jugadores)
            {
                string[] partes = jugador.Split(':');

                if (partes.Length == 2)
                {
                    string nombre = partes[0].Trim();

                    string puntos = partes[1].Replace("puntos", "").Trim();

                    Console.WriteLine("Puntuación idJugador:" + nombre + ", número puntos:" + puntos + ".");
                }
            }
        }

        // Solicita el resultado de la última partida
        private static void MostrarResultado(StreamReader lector, StreamWriter escritor)
        {
            Console.WriteLine("Has escogido resultado");
            // Solicitamos el resultado de la última partida
            escritor.WriteLine("RESULTADO");

            // Recibimos la respuesta del servidor
            string respuesta = lector.ReadLine();

            if (respuesta == null)
            {
                Console.WriteLine("El servidor ha cerrado la conexión.");
                return;
            }

            if (respuesta.StartsWith("RESULTADO|"))
            {
                string resultado = respuesta.Substring("RESULTADO|".Length);

                if (resultado.Contains("Ganador: "))
                {
                    // Extraemos el nombre del ganador
                    string ganador = resultado.Split(new string[] { "Ganador: " },StringSplitOptions.None)[1];

                    Console.WriteLine("El ganador de la última partida ha sido: " + ganador);
                }
                else if (resultado.Contains("Empate"))
                {
                    Console.WriteLine("La última partida ha terminado en empate.");
                }
                else
                {
                    Console.WriteLine(resultado);
                }
            }
            else
            {
                Console.WriteLine("Respuesta del servidor: " + respuesta);
            }
        }


        // Comunica al servidor que el jugador quiere salir
        private static void SalirJuego(StreamReader lector, StreamWriter escritor)
        {
            escritor.WriteLine("SALIR");

            lector.ReadLine();

            Console.WriteLine("Has salido del juego.");
        }
    }
}

