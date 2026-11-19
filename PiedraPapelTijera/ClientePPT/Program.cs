using System;
using System.Net.Sockets;
using System.IO;
using System.Text;

namespace ClientePPT
{
    internal class Program
    {
        // Dirección IP del servidor.
        private const string SERVIDOR = "127.0.0.1";

        // Puerto por el que se comunicarán cliente y servidor.
        private const int PUERTO = 5000;

        static void Main(string[] args)
        {
            Console.WriteLine("===============================");
            Console.WriteLine("   JUEGO PIEDRA PAPEL TIJERA");
            Console.WriteLine("===============================");

            try
            {
                // Creamos el socket TCP del cliente.
                using (TcpClient cliente = new TcpClient())
                {
                    Console.WriteLine();

                    Console.WriteLine("Socket Cliente creado");

                    // Establecemos la conexión con el servidor
                    cliente.Connect(SERVIDOR, PUERTO);

                    // Obtenemos el flujo de comunicación de la conexión.
                    using (NetworkStream flujo = cliente.GetStream())

                    using (StreamReader lector = new StreamReader(flujo, new UTF8Encoding(false)))
                    using (StreamWriter escritor = new StreamWriter(flujo, new UTF8Encoding(false)))
                    {
                        // Envía automáticamente cada línea sin esperar
                        escritor.AutoFlush = true;

                        Console.WriteLine("Buffer de escritura y lectura creados.");
                        Console.WriteLine();

                        // Solicitamos el nombre con el que se registrará el jugador
                        Console.WriteLine("Indica el nombre con el que quiere inscribirse al juego.");

                        string nombre = Console.ReadLine();

                        // Enviamos el nombre al servidor
                        escritor.WriteLine(nombre);

                        // Esperamos la respuesta del servidor
                        string respuesta = lector.ReadLine();

                        // Comprobamos si el servidor está completo
                        if (respuesta == "ERROR|LLENO")
                        {
                            Console.WriteLine("El servidor está completo.");
                            return;
                        }

                        // Comprobamos si el registro se ha realizado correctamente
                        if (respuesta != "OK|REGISTRO")
                        {
                            Console.WriteLine("La inscripción no ha sido correcta.");
                            return;
                        }

                        Console.WriteLine();
                        Console.WriteLine("La inscripción fue correcta");

                        Console.WriteLine("\nInscripción realizada correctamente.");

                        Console.WriteLine("Bienvenido al juego, " + nombre);

                        // Variable que controla la permanencia en el menú
                        bool continuar = true;

                        while (continuar)
                        {
                            Console.WriteLine();
                            Console.WriteLine("Juega piedra papel o tijera");
                            Console.WriteLine("===========================");
                            Console.WriteLine("1: Jugar");
                            Console.WriteLine("2: Puntuación");
                            Console.WriteLine("3: Mostrar resultado");
                            Console.WriteLine("-1: Salir");
                            Console.WriteLine("===========================");

                            // Leemos la opción elegida por el usuario
                            string opcion = Console.ReadLine();

                            // Ejecutamos la operación correspondiente
                            switch (opcion)
                            {
                                // OPCIÓN 1: JUGAR
                                case "1":

                                    Console.WriteLine();
                                    Console.WriteLine("JUGAR:");
                                    Console.WriteLine();

                                    Console.WriteLine("Escoge piedra papel o tijera");
                                    Console.WriteLine("============================");
                                    Console.WriteLine("1: Piedra");
                                    Console.WriteLine("2: Papel");
                                    Console.WriteLine("3: Tijera");
                                    Console.WriteLine("============================");

                                    // Leemos la elección del jugador
                                    string eleccion = Console.ReadLine();

                                    // Convertimos el número elegido en el nombre de la jugada
                                    string jugada = "";

                                    switch (eleccion)
                                    {
                                        case "1":
                                            jugada = "PIEDRA";
                                            break;

                                        case "2":
                                            jugada = "PAPEL";
                                            break;

                                        case "3":
                                            jugada = "TIJERA";
                                            break;

                                        default:
                                            Console.WriteLine("Opción incorrecta.");
                                            break;
                                    }

                                    // Solo enviamos la jugada si es válida.
                                    if (jugada != "")
                                    {
                                        // JUGAR| indica que queremos jugar.
                                        escritor.WriteLine("JUGAR|" + jugada);

                                        // Esperamos la respuesta del servidor.
                                        string respuestaJuego = lector.ReadLine();

                                        // Comprobamos que hemos recibido un resultado
                                        if (respuestaJuego.StartsWith("RESULTADO|"))
                                        {
                                            // Eliminamos la etiqueta RESULTADO|
                                            string datos = respuestaJuego.Substring("RESULTADO|".Length);

                                            // Extraemos el número de partida
                                            string numero = datos.Split(' ')[1];

                                            Console.WriteLine(
                                                "El número de jugada es: " + numero);
                                        }
                                        else
                                        {
                                            // Mostramos posibles errores del servidor
                                            Console.WriteLine(respuestaJuego);
                                        }
                                    }

                                    break;

                                // OPCIÓN 2: PUNTUACIONES
                                case "2":

                                    Console.WriteLine();
                                    Console.WriteLine("PUNTUACION:");
                                    Console.WriteLine();

                                    Console.WriteLine("Has escogido puntuación:");

                                    // Solicitamos al servidor las puntuaciones
                                    escritor.WriteLine("PUNTUACION");

                                    // Recibimos la información de todos los jugadores
                                    string puntuacion = lector.ReadLine();

                                    // Comprobamos que la respuesta tiene el formato esperado
                                    if (puntuacion.StartsWith("PUNTUACION|"))
                                    {
                                        // Quitamos la etiqueta PUNTUACION|.
                                        string datos = puntuacion.Substring("PUNTUACION|".Length);

                                        // Separamos los jugadores.
                                        string[] jugadoresPuntuacion = datos.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                                        // Recorremos las puntuaciones recibidas.
                                        foreach (string jugador in jugadoresPuntuacion)
                                        {
                                            // Separamos el nombre y los puntos.
                                            string[] partes = jugador.Split(':');

                                            if (partes.Length == 2)
                                            {
                                                string nombreJugador = partes[0].Trim();

                                                // Eliminamos la palabra puntos
                                                string puntos = partes[1].Replace("puntos", "").Trim();

                                                Console.WriteLine("Puntuación idJugador:" + nombreJugador + ", número puntos:" + puntos + ".");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Mostramos cualquier mensaje de error
                                        Console.WriteLine(puntuacion);
                                    }

                                    break;

                                // OPCIÓN 3: RESULTADO
                                case "3":

                                    Console.WriteLine();
                                    Console.WriteLine("MOSTRAR RESULTADO:");
                                    Console.WriteLine();

                                    Console.WriteLine("Has escogido mostrar resultado");

                                    // Solicitamos el último resultado al servidor
                                    escritor.WriteLine("RESULTADO");

                                    // Recibimos el resultado almacenado en el servidor
                                    string resultado = lector.ReadLine();

                                    // Comprobamos que la respuesta es correcta
                                    if (resultado.StartsWith("RESULTADO|"))
                                    {
                                        // Si existe un ganador, extraemos su nombre
                                        if (resultado.Contains("Ganador: "))
                                        {
                                            string ganador = resultado.Split(new string[] { "Ganador: " }, StringSplitOptions.None)[1];

                                            Console.WriteLine("El ganador de la última jugada ha sido: " + ganador);
                                        }
                                        else
                                        {
                                            // Si no aparece un ganador, la partida terminó en empate
                                            Console.WriteLine("La última jugada ha terminado en empate.");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine(resultado);
                                    }

                                    break;

                                // OPCIÓN -1: SALIR DEL JUEGO
                                case "-1":

                                    // Informamos al servidor de que queremos salir
                                    escritor.WriteLine("SALIR");

                                    // Esperamos la confirmación de salida.
                                    lector.ReadLine();

                                    // Finalizamos el bucle del menú
                                    continuar = false;

                                    break;

                                // CONTROL DE OPCIONES INCORRECTAS
                                default:
                                    Console.WriteLine("Opción incorrecta.");
                                    break;
                            }
                        }
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
    }
}
