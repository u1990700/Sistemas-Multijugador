using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Networking.Transport.Samples
{
    public class ClientBehaviour : MonoBehaviour
    {
        NetworkDriver m_Driver;
        NetworkConnection m_Connection;
        NetworkPipeline myPipeline;

        [SerializeField] private string ip = "0.0.0.0";
        [SerializeField] private ushort port;

        bool isConnected = false;
        public static ClientBehaviour Instance;

        void Start()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (m_Driver.IsCreated)
                m_Driver.Dispose();
        }

        public void ConnectToServer(string ip, ushort port)
        {
            this.ip = ip;
            this.port = port;

            m_Driver = NetworkDriver.Create();

            myPipeline = m_Driver.CreatePipeline(
                typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));

            var endpoint = NetworkEndpoint.Parse(this.ip, this.port);
            m_Connection = m_Driver.Connect(endpoint);

            Debug.Log($"Intentando conectar a {this.ip}:{this.port}");
        }

        void Update()
        {
            if (!m_Driver.IsCreated)
                return;

            m_Driver.ScheduleUpdate().Complete();

            if (!m_Connection.IsCreated)
                return;

            DataStreamReader stream;
            NetworkEvent.Type cmd;

            while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    Debug.Log("Conectado al servidor.");

                    if (!isConnected)
                    {
                        isConnected = true;
                        SceneManager.LoadScene("EscogerPersonaje");
                    }

                    m_Driver.BeginSend(myPipeline, m_Connection, out var writer);
                    writer.WriteByte((byte)'A');
                    writer.WriteUInt(1);
                    m_Driver.EndSend(writer);
                }

                else if (cmd == NetworkEvent.Type.Data)
                {
                    Debug.Log("Entra");
                    if (stream.Length <= 0)
                        continue;

                    char messageID = (char)stream.ReadByte();
                    Debug.Log(messageID);
                    Debug.Log($"Mensaje recibido: {messageID}");

                    switch (messageID)
                    {
                        case 'H':
                            HandleWelcomeMessage(ref stream);
                            break;

                        case 'D':
                            HandleAvailableCharacters(ref stream);
                            break;

                        case 'E':
                            HandleAcceptedSelection(ref stream);
                            break;

                        case 'F':
                            HandleDeniedSelection(ref stream);
                            break;

                        case 'G':
                            Debug.Log("El servidor indica: ¡EMPIEZA EL JUEGO!");
                            // SceneManager.LoadScene("EscenaJuego");
                            break;

                        default:
                            Debug.LogWarning($"Mensaje desconocido: {messageID}");
                            break;
                    }
                }

                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.LogError("Cliente desconectado del servidor.");
                    m_Connection = default;
                }
            }
        }



        public void SendCharacterSelection(string characterName)
        {
            if (!m_Connection.IsCreated)
            {
                Debug.LogError("No hay conexión activa con el servidor.");
                return;
            }

            Debug.Log($"Enviando selección al servidor: {characterName}");

            m_Driver.BeginSend(myPipeline, m_Connection, out var writer);

            // Código del mensaje
            writer.WriteByte((byte)'C');

            // Nombre del personaje
            writer.WriteFixedString32(characterName);

            m_Driver.EndSend(writer);
        }


        void HandleWelcomeMessage(ref DataStreamReader stream)
        {
            string serverName = stream.ReadFixedString32().ToString();
            string clientName = stream.ReadFixedString32().ToString();

            // Comprobamos si hay datos suficientes para un nombre extra
            string previousName = "N/A";
            if (stream.Length - stream.GetBytesRead() > 4)
                previousName = stream.ReadFixedString32().ToString();

            float time = stream.ReadFloat();

            Debug.Log($"[H] Servidor: {serverName} | Cliente: {clientName} | Anterior: {previousName} | Tiempo: {time}");
        }


        void HandleAvailableCharacters(ref DataStreamReader stream)
        {
            int count = stream.ReadInt();
            Debug.Log($"[D] {count} personajes disponibles:");

            for (int i = 0; i < count; i++)
            {
                string ch = stream.ReadFixedString32().ToString();
                Debug.Log($"   -> {ch}");
            }
        }


        void HandleAcceptedSelection(ref DataStreamReader stream)
        {
            string accepted = stream.ReadFixedString32().ToString();
            Debug.Log($"[E] Selección ACEPTADA: {accepted}");

            // Logica para seleccion aceptada
        }

        void HandleDeniedSelection(ref DataStreamReader stream)
        {
            string denied = stream.ReadFixedString32().ToString();
            Debug.LogError($"[F] Selección DENEGADA: {denied} ya está elegido.");

            // Logica para seleccion denegada
        }


    }
}