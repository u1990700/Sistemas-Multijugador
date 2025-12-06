using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport.Samples; // Necesario para la struct CharacterSpawnData

public class GameManager : MonoBehaviour
{
    // 1. Singleton: Acceso estático a la única instancia
    public static GameManager Instance;

    private Dictionary<string, GameObject> activeCharacters = new Dictionary<string, GameObject>();

    [Header("Personajes")]
    public GameObject perroPersonaje;
    public GameObject creeperPersonaje;


    public struct CharacterSpawnData
    {
        public string CharacterName;
        public Vector3 Position;
    }



    private void Awake() // <<--- ¡MUY IMPORTANTE!
    {
        // 1. Asigna la instancia estática AQUI.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // NO AÑADAS DontDestroyOnLoad
    }


    private void Start()
    {
        // Llama al servidor para que sepa que ya puede procesar mensajes
        if (ServerBehaviour.Instance != null)
        {
            ServerBehaviour.Instance.NotifyGameSceneReady();
        }
    }


    public void SpawnCharacters(List<CharacterSpawnData> spawnData, string localPlayerName)
    {
        Debug.Log($"Iniciando spawning de {spawnData.Count} personajes. Local player: {localPlayerName}");

        // Determinar si estamos ejecutando en el servidor host
        bool isServerHost = string.IsNullOrEmpty(localPlayerName);

        foreach (var data in spawnData)
        {
            GameObject characterObject = null;
            string charName = data.CharacterName;

            if (isServerHost)
            {
                // --- LÓGICA DE SERVER HOST (Objetos Estáticos) ---

                // 1. Crear el nombre del GameObject estático que el servidor tiene en el Canvas
                string objectName = charName.ToLower() + "Personaje"; // "perroPersonaje", "creeperPersonaje"

                // 2. Buscar el objeto estático en la escena (Lento en el inicio, pero solo una vez)
                characterObject = GameObject.Find(objectName);

                if (characterObject != null)
                {
                    // 3. Activar el objeto que por defecto está desactivado
                    characterObject.SetActive(true);
                    // 4. Establecer la posición inicial
                    characterObject.transform.position = data.Position;
                    Debug.Log($"SERVER HOST: Activado objeto estático '{charName}' en {data.Position}");
                }
                else
                {
                    Debug.LogError($"SERVER HOST ERROR: No se encontró el objeto estático '{objectName}'.");
                }
            }
 
            

            // Almacenar el objeto (estático o instanciado) en el diccionario
            if (characterObject != null)
            {
                // Usamos el nombre base (ej. "Perro") como clave
                activeCharacters[charName] = characterObject;
            }
        }
    }

    // Fragmento de GameManager.cs (Método UpdateRemotePlayerPosition MODIFICADO)

    public void UpdateRemotePlayerPosition(string characterName, Vector3 position)
    {
        // 1. Obtener la referencia al GameObject
        GameObject playerObject = GetHostCharacterObject(characterName);

        // En el cliente, esta función necesitaría buscar el GameObject instanciado.
        // Pero asumiendo que el Host es el que llama a esta función con objetos estáticos,
        // usamos la referencia directa.

        if (playerObject != null && playerObject.activeSelf)
        {
            // 2. Actualizar la posición del GameObject que está en el Canvas
            playerObject.transform.position = position;
            Debug.Log($"SERVER HOST UPDATED: {characterName} a {position.x:F2}, {position.y:F2}");
        }
        else
        {
            // Este warning aparecerá en el cliente si recibe una actualización de un objeto que 
            // aún no se ha creado o en el host si falta asignar la referencia.
            Debug.LogWarning($"UpdateRemotePlayerPosition: No se encontró o no está activo el personaje '{characterName}' para actualizar en el Host.");
        }
    }

    // Este método es nuevo y nos permite mapear el nombre al objeto estático de forma segura
    private GameObject GetHostCharacterObject(string characterName)
    {
        // Es fundamental que los nombres ("Perro", "Creeper") coincidan con los de ServerBehaviour.cs
        if (characterName == "Perro")
        {
            return perroPersonaje;
        }
        else if (characterName == "Creeper")
        {
            return creeperPersonaje;
        }
        return null;
    }



}