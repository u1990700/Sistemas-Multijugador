using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEditor.VersionControl;
using System.Collections.Generic;
using TMPro;
using static UnityEngine.InputSystem.InputRemoting;
using System.Net.Sockets;
using System.Net;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;


namespace Unity.Networking.Transport.Samples
{
    public class ServerBehaviour : MonoBehaviour
    {

        public static ServerBehaviour Instance;


        NetworkDriver m_Driver;
        NativeList<NetworkConnection> m_Connections;
        NetworkPipeline myPipeline;
        NetworkConnection previousConnection;

        public TMP_Text ipDisplay;

        public ushort idPort = 5001;

        // Llista de tots els personatges posibles
        private List<string> allCharacters = new List<string> {
            "Creeper", "Perro" // Ejemplo de personajes
        };



        public struct ClientInfo
        {
            public string Name;
            public float ConnectionTime;
        }

        // Utilitza un diccionari per mapejar connexions a informació
        Dictionary<NetworkConnection, ClientInfo> m_ClientInfo = new Dictionary<NetworkConnection, ClientInfo>();
        const string SERVER_NAME = "ElNostrePrimerServer"; // Nom del servidor

        // Diccionari per emmagatzemar les seleccions dels clients
        private Dictionary<NetworkConnection, string> m_ClientSelections =
        new Dictionary<NetworkConnection, string>();

        // Variable per guardar el client anterior
        string previousClientName = "";

        private bool m_GameSceneReady = false;


        private void Awake()
        {
            // 2. Implementar Singleton y DontDestroyOnLoad
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
        }


        void Start()
        {
            m_Driver = NetworkDriver.Create();
            m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
            myPipeline = m_Driver.CreatePipeline(
                typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));

            var endpoint = NetworkEndpoint.AnyIpv4.WithPort(idPort);


            string serverIP = GetLocalIPAddress();
            // Usamos el puerto que definiste para la conexión
            int serverPort = endpoint.Port;
            
            ipDisplay.text = $"**SERVIDOR ONLINE**\n\nIP para el Cliente:\n**{serverIP}:{serverPort}**";
            Debug.Log($"Server IP set to: {serverIP}:{serverPort}");


            if (m_Driver.Bind(endpoint) != 0)
            {
                Debug.LogError($"Failed to bind to port {serverPort}.");
                return;
            }
            m_Driver.Listen();
            // Inicialització del Diccionari
            m_ClientInfo = new Dictionary<NetworkConnection, ClientInfo>();

           

        }

        void OnDestroy()
        {
            if (m_Driver.IsCreated)
            {
                m_Driver.Dispose();
                m_Connections.Dispose();
            }
            m_ClientInfo.Clear();
        }

        void Update()
        {
            m_Driver.ScheduleUpdate().Complete();

            // Clean up connections.
            for (int i = 0; i < m_Connections.Length; i++)
            {
                if (!m_Connections[i].IsCreated)
                {
                    m_Connections.RemoveAtSwapBack(i);
                    i--;
                }
            }

            // Accept new connections.
            NetworkConnection c;
            while ((c = m_Driver.Accept()) != default)
            {
                m_Connections.Add(c);
                Debug.Log("Accepted a connection.");

                // 1. Generar nom i guardar info
                string clientName = $"Client_{m_Connections.Length}";
                ClientInfo info = new ClientInfo { Name = clientName, ConnectionTime = Time.time };
                m_ClientInfo.Add(c, info);

                Debug.Log($"Accepted a connection. {clientName} connected. Total: {m_Connections.Length}");


                if (m_Connections.Length == 1)
                {
                    //Enviar missatge de benvinguda(H +NOM_SERVIDOR + NOM_CLIENT + Temps)
                    SendWelcomeMessage(c, clientName, SERVER_NAME, Time.time);
                } 
                else
                {
                    // Obtenir el nom del client anterior
                    previousConnection = m_Connections[m_Connections.Length - 2];
                    previousClientName = m_ClientInfo[previousConnection].Name;

                    // Enviem missatge estès (H + Servidor + Nom + NomAnterior + Temps)
                    SendExtendedWelcomeMessage(c, clientName, SERVER_NAME, previousClientName, Time.time);
                }

                // 3. Obtener la IP del cliente que se conecta:
                NetworkEndpoint endpoint = m_Driver.GetRemoteEndpoint(c);
                string clientIP = endpoint.Address.ToString();
                string fullEndpoint = endpoint.ToString();

                // 4. Actualizar el Text Mesh Pro con la IP obtenida:
                if (ipDisplay != null)
                 {
                    //Mostramos la IP del último cliente conectado
                    ipDisplay.text  = $"Última IP Conectada:\n{fullEndpoint}";
                 }

                SendAvailableCharacters(c);

            }

            for (int i = 0; i < m_Connections.Length; i++)
            {
                DataStreamReader stream;
                NetworkEvent.Type cmd;
                while ((cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {

                        // Llegim el codi del missatge (CHAR)
                        char messageID = (char)stream.ReadByte();

                        if (messageID == 'A') // Missatge de client per defecte
                        {

                            Debug.Log($"Missatge per defecte del client '{messageID}'");

                            if (stream.Length > stream.GetBytesRead())
                            {
                                stream.ReadUInt();
                                Debug.Log("Consumido UInt pendiente de mensaje 'A'. Stream alineado.");
                            }

                            //m_Driver.BeginSend(NetworkPipeline.Null, m_Connections[i], out var writer);
                            m_Driver.BeginSend(myPipeline, m_Connections[i], out var writer);
                            m_Driver.EndSend(writer);
                        }


                        else if (messageID == 'C')
                        {
                            NetworkConnection clientConnection = m_Connections[i];

                            // 1. Leer la elección del cliente
                            // Usamos DataStreamReader.ReadFixedString32() para leer el personaje
                            var selectedCharFS = stream.ReadFixedString32();
                            string selectedChar = selectedCharFS.ToString();

                            while (stream.Length > stream.GetBytesRead())
                            {
                                stream.ReadByte();
                            }

                            Debug.Log($"Client {m_ClientInfo[clientConnection].Name} tries to select: {selectedChar}");

                            // 2. Verificar la disponibilidad (Punto 5)
                            List<string> available = GetAvailableCharacters();

                            if (available.Contains(selectedChar))
                            {
                                Debug.Log("Enviando mensaje de confirmación al cliente");

                                //¡Selección ACEPTADA! 
                                m_ClientSelections[clientConnection] = selectedChar; // Guardar la elección

                                //Enviar confirmación al cliente 
                                SendSelectionResponse(clientConnection, 'E', selectedChar);
                                Debug.Log($"Selection ACCEPTED: {selectedChar} for {m_ClientInfo[clientConnection].Name}");


                                //Notificar a *TODOS* los clientes sobre los nuevos disponibles
                                SendAvailableCharactersToAll();


                                // Empezar el juego para el cliente

                                m_Driver.BeginSend(myPipeline, m_Connections[i], out var writer0);
                                writer0.WriteByte((byte)'G');
                                m_Driver.EndSend(writer0);


                                if (AreAllReady())
                                {
                                    SendGameStartToAll();

                                    SceneManager.LoadScene("EscenaJuego", LoadSceneMode.Single);
                                    SendCharacterPositionsToAll();
                                }
                            }

                            else
                            {

                                // Enviar denegación al cliente
                                SendSelectionResponse(clientConnection, 'F', selectedChar);
                                Debug.Log($"Selection DENIED: {selectedChar} is already taken.");
                            }
                        }
                        else if (messageID == 'M')
                        {

                            //COMPROBACIÓN ANTES DE CUALQUIER OTRA COSA
                            if (!m_GameSceneReady)
                            {
                                // Descartamos mensajes de movimiento que llegan demasiado pronto
                                Debug.LogWarning("Mensaje 'M' recibido antes de que GameManager esté listo. Ignorando...");
                                // Asegúrate de consumir el resto del stream para no corromper el paquete.
                                while (stream.Length > stream.GetBytesRead()) { stream.ReadByte(); }
                                continue;
                            }

                            NetworkConnection senderConnection = m_Connections[i];

                            // 1. Leer la posición (X, Y)
                            float posX = stream.ReadFloat();
                            float posY = stream.ReadFloat();


                            Debug.Log($"Posicion x y {posX}, {posY}");

                            while (stream.Length > stream.GetBytesRead())
                            {
                                stream.ReadByte();
                            }

                            Vector3 newPosition = new Vector3(posX, posY, 0f);


                            if (m_GameSceneReady && GameManager.Instance != null)
                            {
                                // Obtener el nombre del personaje
                                string charN = m_ClientSelections.ContainsKey(senderConnection)
                                                ? m_ClientSelections[senderConnection]
                                                : "Unknown";

                                // Llamar al GameManager para que verifique la colisión y actualice la vida
                                // Pasamos el nombre del personaje y su NUEVA posición
                                GameManager.Instance.CheckCollisionAndUpdateHealth(charN, newPosition);
                                GameManager.Instance.UpdateRemotePlayerPosition(charN, newPosition);

                            }

                            BroadcastMovement(senderConnection, newPosition);


                        }
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        NetworkConnection disconnectedConnection = m_Connections[i];
                        Debug.Log("Client desconectat del servidor");

                        // Limpiar selección de personaje si existe
                        if (m_ClientSelections.ContainsKey(disconnectedConnection))
                        {
                            m_ClientSelections.Remove(disconnectedConnection);
                            SendAvailableCharactersToAll(); // Notificar a los demás que el personaje está libre
                        }

                        // Limpiar ClientInfo y la conexión
                        if (m_ClientInfo.ContainsKey(disconnectedConnection))
                        {
                            m_ClientInfo.Remove(disconnectedConnection);
                        }

                        m_Connections[i] = default;
                        break;
                    }

                }
            }
        }
        void SendWelcomeMessage(NetworkConnection connection, string clientName, string serverName, float time)
        {
            // Utilitza el pipeline configurat al punt 7
            m_Driver.BeginSend(myPipeline, connection, out var writer);

            writer.WriteByte((byte)'H');           // Codi del missatge (CHAR) 
            writer.WriteFixedString32(serverName); // NOM_SERVIDOR 
            writer.WriteFixedString32(clientName); // NOM_CLIENT 
            writer.WriteFloat(time);               // Temps (Time.time) 

            m_Driver.EndSend(writer);
        }
        void SendExtendedWelcomeMessage(NetworkConnection connection, string clientName, string serverName, string previousClientName, float time)
        {
            m_Driver.BeginSend(myPipeline, connection, out var writer);

            // ESTRUCTURA ESTESA (Punt 9): H + NOM_SERVIDOR + NOM_CLIENT + NOM_CLIENT_ANTERIOR + Temps
            writer.WriteByte((byte)'H');                  // Codi del missatge [cite: 44]
            writer.WriteFixedString32(serverName);        // NOM_SERVIDOR [cite: 45]
            writer.WriteFixedString32(clientName);        // NOM_CLIENT [cite: 46]
            writer.WriteFixedString32(previousClientName); // NOM_CLIENT_ANTERIOR 
            writer.WriteFloat(time);                      // Temps (Time.time) [cite: 48, 50]

            m_Driver.EndSend(writer);
        }

        // Dentro de ServerBehaviour.cs

        // **A. Envía el resultado de la selección (Aceptado 'E' o Denegado 'F')**
        void SendSelectionResponse(NetworkConnection connection, char responseID, string characterName)
        {
            m_Driver.BeginSend(myPipeline, connection, out var writer);

            // CÓDIGO (E o F)
            writer.WriteByte((byte)responseID);
            // NOMBRE DEL PERSONAJE
            writer.WriteFixedString32(characterName);

            m_Driver.EndSend(writer);
        }

        // **B. Envía la lista de personajes disponibles a un cliente específico (Mensaje 'D')**
        void SendAvailableCharacters(NetworkConnection connection)
        {
            List<string> available = GetAvailableCharacters();

            m_Driver.BeginSend(myPipeline, connection, out var writer);

            // CÓDIGO 'D'
            writer.WriteByte((byte)'D');

            // 1. Escribe la CANTIDAD de personajes disponibles
            writer.WriteInt(available.Count);

            // 2. Escribe cada personaje en la lista
            foreach (string character in available)
            {
                
                writer.WriteFixedString32(character);
            }

            m_Driver.EndSend(writer);
        }

        // **C. Envía la lista de personajes disponibles a TODOS los clientes**
        void SendAvailableCharactersToAll()
        {
            for (int i = 0; i < m_Connections.Length; i++)
            {
                if (m_Connections[i].IsCreated)
                {
                    SendAvailableCharacters(m_Connections[i]);
                }
            }
        }

        // Enviamos a los clientes que el juego ha empezado
        void SendGameStartToAll()
        {
            m_Driver.BeginSend(myPipeline, m_Connections[0], out var writer0);
            writer0.WriteByte((byte)'G');
            m_Driver.EndSend(writer0);

            m_Driver.BeginSend(myPipeline, m_Connections[1], out var writer1);
            writer1.WriteByte((byte)'G');
            m_Driver.EndSend(writer1);

            Debug.Log("GAME START: Sending 'G' message to all clients.");
            SendCharacterPositionsToAll();
            SceneManager.LoadScene("EscenaJuego");
            
        }


        // Obtener los personajes que *aún no han sido elegidos*
        private List<string> GetAvailableCharacters()
        {
            // Coge la lista completa y elimina los que ya están en m_ClientSelections.Values
            List<string> available = new List<string>(allCharacters);
            foreach (string selectedChar in m_ClientSelections.Values)
            {
                available.Remove(selectedChar);
            }
            return available;
        }

        private bool AreAllReady()
        {

           
            
            // 1. Debe haber al menos 2 conexiones activas para empezar
            if (m_Connections.Length < 2)
            {
                return false;
            }

            // 2. El número de selecciones debe ser igual al número de conexiones
            return m_ClientSelections.Count == m_Connections.Length;
            
        }

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                // Solo nos interesa la IP de la red local (IPv4)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            // Si no encuentra una IP local válida, usa una dirección por defecto
            return "127.0.0.1";
        }


        void SendCharacterPositionsToAll()
        {
            // 1. Definir posiciones de aparición (hardcodeadas o generadas)
            Vector2[] spawnPoints = { new Vector2(-75, 0f), new Vector2(100f, 100f) };

            // Crear la lista usando la estructura pública del GameManager
            List<GameManager.CharacterSpawnData> spawnData = new List<GameManager.CharacterSpawnData>();

            int i = 0;
            foreach (var entry in m_ClientSelections)
            {
                if (i < spawnPoints.Length)
                {
                    // Llenar la estructura del GameManager
                    GameManager.CharacterSpawnData data = new GameManager.CharacterSpawnData
                    {
                        CharacterName = entry.Value,
                        Position = spawnPoints[i]
                    };
                    spawnData.Add(data);
                    i++;
                }
            }

            // 2. Enviar a cada cliente
            for (int j = 0; j < m_Connections.Length; j++)
            {
                if (m_Connections[j].IsCreated)
                {
                    m_Driver.BeginSend(myPipeline, m_Connections[j], out var writer);

                    writer.WriteByte((byte)'P'); // CÓDIGO 'P'
                    writer.WriteInt(spawnData.Count); // CONTEO DE PERSONAJES

                    foreach (var data in spawnData)
                    {
                        writer.WriteFixedString32(data.CharacterName);
                        writer.WriteFloat(data.Position.x);
                        writer.WriteFloat(data.Position.y);

                    }
                    
                    m_Driver.EndSend(writer);
                }
            }
            Debug.Log("Sent 'P' message with character positions to all clients.");

            if (GameManager.Instance != null)
            {
                // En un servidor dedicado, el localPlayerName puede ser una cadena vacía ""
                // En un Host, podrías pasar el nombre de un jugador si el host también es jugador.
                GameManager.Instance.SpawnCharacters(spawnData, "");
                Debug.Log("Spawned characters in local Server scene.");
            }
        }

        // Fragmento de ServerBehaviour.cs (NUEVO MÉTODO)

        void BroadcastMovement(NetworkConnection sender, Vector3 position)
        {
            // 1. Obtener el nombre del personaje que se movió
            string characterName = m_ClientSelections.ContainsKey(sender)
                                 ? m_ClientSelections[sender]
                                 : "";

            if (string.IsNullOrEmpty(characterName))
                return;

            // 2. Enviar a todos (excepto al que lo envió)
            for (int i = 0; i < m_Connections.Length; i++)
            {
                NetworkConnection recipient = m_Connections[i];

                // No enviamos el paquete de vuelta al remitente original
                if (recipient.IsCreated && recipient != sender)
                {
                    m_Driver.BeginSend(myPipeline, recipient, out var writer);

                    writer.WriteByte((byte)'R'); // CÓDIGO 'R' (Movimiento Remoto)

                    // ¿Quién se movió?
                    writer.WriteFixedString32(characterName);

                    // ¿A dónde se movió?
                    writer.WriteFloat(position.x);
                    writer.WriteFloat(position.y);

                    m_Driver.EndSend(writer);
                }
            }
        }

        public void NotifyGameSceneReady()
        {
            m_GameSceneReady = true;
            Debug.Log("SERVIDOR: GameManager listo. Procesando mensajes de juego.");
        }

        // El código para el enemigo es 'Z' para evitar colisión con 'E' (Accepted Selection)
        public void UpdateRemoteEnemyPositioni(Vector2 position)
        {
            // Enviamos un mensaje 'Z' a todos los clientes activos.
            for (int i = 0; i < m_Connections.Length; i++)
            {
                NetworkConnection recipient = m_Connections[i];

                if (recipient.IsCreated)
                {
                    m_Driver.BeginSend(myPipeline, recipient, out var writer);

                    writer.WriteByte((byte)'Z');

                    // Enviamos solo las coordenadas (X e Y)
                    writer.WriteFixedString32("GoombaEnemy");
                    writer.WriteFloat(position.x);
                    writer.WriteFloat(position.y);

                    print(position.x);

                    m_Driver.EndSend(writer);
                }
            }
        }
        // Fragmento de ServerBehaviour.cs (NUEVO MÉTODO)

        public void BroadcastHealthUpdate(string playerName, int newHealth)
        {
            // Envía el mensaje 'L' (Vida/Health Update) a todos los clientes
            for (int i = 0; i < m_Connections.Length; i++)
            {
                NetworkConnection recipient = m_Connections[i];

                if (recipient.IsCreated)
                {
                    m_Driver.BeginSend(myPipeline, recipient, out var writer);

                    writer.WriteByte((byte)'X');

                    writer.WriteFixedString32(playerName);

                    m_Driver.EndSend(writer);
                }
            }
        }

    }

}