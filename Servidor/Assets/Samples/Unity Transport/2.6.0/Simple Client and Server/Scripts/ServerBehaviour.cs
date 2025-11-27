using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEditor.VersionControl;
using System.Collections.Generic;

namespace Unity.Networking.Transport.Samples
{
    public class ServerBehaviour : MonoBehaviour
    {
        NetworkDriver m_Driver;
        NativeList<NetworkConnection> m_Connections;
        NetworkPipeline myPipeline;

        public struct ClientInfo
        {
            public string Name;
            public float ConnectionTime;
        }

        // Utilitza un diccionari per mapejar connexions a informació
        Dictionary<NetworkConnection, ClientInfo> m_ClientInfo = new Dictionary<NetworkConnection, ClientInfo>();
        const string SERVER_NAME = "ElNostrePrimerServer"; // Nom del servidor

        void Start()
        {
            m_Driver = NetworkDriver.Create();
            m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
            myPipeline = m_Driver.CreatePipeline(
                typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));

            var endpoint = NetworkEndpoint.AnyIpv4.WithPort(7777);
            if (m_Driver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port 7777.");
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
                    NetworkConnection previousConnection = m_Connections[m_Connections.Length - 2];
                    string previousClientName = m_ClientInfo[previousConnection].Name;

                    // Enviem missatge estès (H + Servidor + Nom + NomAnterior + Temps)
                    SendExtendedWelcomeMessage(c, clientName, SERVER_NAME, previousClientName, Time.time);
                }



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

                            //m_Driver.BeginSend(NetworkPipeline.Null, m_Connections[i], out var writer);
                            m_Driver.BeginSend(myPipeline, m_Connections[i], out var writer);
                            m_Driver.EndSend(writer);
                        }

                       
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        Debug.Log("Client desconectat del servidor");
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

            // NOTA: Unity.Collections.DataStreamWriter no té un mètode 'WriteString' simple, 
            // s'utilitzen tipus amb longitud fixa com FixedString40 o cal gestionar la longitud manualment.

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
    }

}