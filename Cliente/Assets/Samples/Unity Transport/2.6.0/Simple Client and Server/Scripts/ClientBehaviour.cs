using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;


namespace Unity.Networking.Transport.Samples
{
    public class ClientBehaviour : MonoBehaviour
    {
        NetworkDriver m_Driver;
        NetworkConnection m_Connection;
        NetworkPipeline myPipeline;

        [SerializeField] private string ip = "0.0.0.0";
        [SerializeField] private ushort port;

        void Start()
        {

            m_Driver = NetworkDriver.Create();

            // Iniciem la pipeline per enviar missatges fragmentats i fiables
            myPipeline = m_Driver.CreatePipeline(
                typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));


            //var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(7777);
            var endpoint = NetworkEndpoint.Parse(ip, port);
            m_Connection = m_Driver.Connect(endpoint);
        }

        void OnDestroy()
        {
            m_Driver.Dispose();
        }

        void Update()
        {
            m_Driver.ScheduleUpdate().Complete();

            if (!m_Connection.IsCreated)
            {
                return;
            }

            DataStreamReader stream;
            NetworkEvent.Type cmd;
            while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    Debug.Log("Estem conectats al servidor");

                    // Afegim l'identificador 'A'  i el valor
                    m_Driver.BeginSend(myPipeline, m_Connection, out var writer);
                    writer.WriteByte((byte)'A'); // Identificador de missatge (CHAR)
                    writer.WriteUInt(1);
                    m_Driver.EndSend(writer);
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    // Llegir el CHAR identificador
                    char messageId = (char)stream.ReadByte();
                    if (messageId == 'H') // Missatge de benvinguda del servidor (Punt 8)
                    {
                        // LLegir el NOM_SERVIDOR
                        string serverName = stream.ReadFixedString32().ToString();

                        // LLegir el NOM_CLIENT
                        string clientName = stream.ReadFixedString32().ToString();

                        // Inicialitzar el NOM_CLIENT_PREV
                        string previousClientName = "N/A (Primer Client)";

                        // La mida total de les dades menys el que ja hem llegit.

                        int bytesTotal = stream.Length;

                        int bytesRead = stream.GetBytesRead();

                        int bytesRemaining = bytesTotal - bytesRead;


                        if (bytesRemaining > 4)
                        {
                            // LLegir el NOM_CLIENT_ANTERIOR 
                            previousClientName = stream.ReadFixedString32().ToString();
                        }

                        // LLegir el Temps
                        float serverTime = stream.ReadFloat();

                        // 5. Mostrar la informació per consola (Punt 10)
                        if (previousClientName == "N/A (Primer Client)")
                        {
                            Debug.Log($"[H] Benvingut! Nom Servidor: **{serverName}**, El meu Nom: **{clientName}**, Temps Encès: **{serverTime:F2}s**.");
                        }
                        else
                        {
                            Debug.Log($"[H] Benvingut! Nom Servidor: **{serverName}**, El meu Nom: **{clientName}**, Client Anterior: **{previousClientName}**, Temps Encès: **{serverTime:F2}s**.");
                        }


                    } else
                    {
                        m_Connection.Disconnect(m_Driver);
                        m_Connection = default;
                    }
                }
                    
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client got disconnected from server.");
                    m_Connection = default;
                }
            }
        }
    }
}