using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport.Samples; // Necesario para la struct CharacterSpawnData

public class GameManager : MonoBehaviour
{
    // 1. Singleton: Acceso estático a la única instancia
    public static GameManager Instance;

    // --- REFERENCIAS PÚBLICAS PARA EL SERVIDOR HOST (ASIGNAR EN INSPECTOR) ---
    [Header("Objetos Estáticos del Servidor")]
    public GameObject perroPersonaje;
    public GameObject creeperPersonaje;
    public GameObject goombaEnemy;
    public RectTransform gombaTransform;
    // -----------------------------------------------------------------------

    // Diccionario para almacenar instancias de personajes instanciados (solo CLIENTE)
    // El string es el CharacterName ("Perro", "Creeper").
    private Dictionary<string, GameObject> instancedCharacters = new Dictionary<string, GameObject>();

    // --- ESTRUCTURA DE DATOS PÚBLICA (para compartir entre scripts) ---
    public struct CharacterSpawnData
    {
        public string CharacterName;
        public Vector3 Position;
    }

    [System.Serializable]
    public struct CharacterPrefabMapping
    {
        public string characterName;
        public GameObject prefab;
    }

    // Asigna los prefabs de los personajes aquí en el Inspector (Solo Cliente)
    public List<CharacterPrefabMapping> characterPrefabs;

    // Referencia al prefab del script de control local del jugador
    public GameObject LocalPlayerControlPrefab;


    // =========================================================================
    // INICIALIZACIÓN
    // =========================================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }


    // Método que mapea el nombre del personaje al objeto estático (Host)
    private GameObject GetHostCharacterObject(string characterName)
    {
        if (characterName == "Perro")
        {
            return perroPersonaje;
        }
        else if (characterName == "Creeper")
        {
            return creeperPersonaje;

        }else if (characterName == "GombaEnemy")
        {
            return goombaEnemy;
        }
        return null;
    }

    // =========================================================================
    // SPAWNING INICIAL (MENSAJE 'P')
    // =========================================================================

    public void SpawnCharacters(List<CharacterSpawnData> spawnData, string localPlayerName)
    {
        Debug.Log($"Iniciando spawning de {spawnData.Count} personajes. Local player: {localPlayerName}");

        // El servidor llama con localPlayerName == ""
        bool isServerHost = string.IsNullOrEmpty(localPlayerName);

        foreach (var data in spawnData)
        {
            GameObject characterObject = null;
            string charName = data.CharacterName;

            if (isServerHost)
            {
                // --- LÓGICA DE SERVER HOST (Objetos Estáticos) ---
                characterObject = GetHostCharacterObject(charName);

                if (characterObject != null)
                {
                    characterObject.SetActive(true);
                    // Establecer la posición inicial del Rigidbody/Transform (el RectTransform se moverá con él)
                    characterObject.transform.position = data.Position;
                    Debug.Log($"SERVER HOST: Activado objeto estático '{charName}' en {data.Position}");
                }
                else
                {
                    Debug.LogError($"SERVER HOST ERROR: La referencia pública para '{charName}' no está asignada o el nombre es incorrecto.");
                }
            }
            else // LÓGICA DE CLIENTE (Instanciar Prefabs)
            {
                // 1. Encontrar el Prefab correcto
                GameObject prefab = characterPrefabs.Find(p => p.characterName == charName).prefab;
                if (prefab == null)
                {
                    Debug.LogError($"CLIENT ERROR: Prefab no encontrado para: {charName}");
                    continue;
                }

                // 2. Instanciar el personaje
                characterObject = Instantiate(prefab, data.Position, Quaternion.identity);

                // --- NUEVO DEBUG LOG: Confirmar que la instancia se creó y el papel ---
                if (charName != localPlayerName)
                {
                    Debug.Log($"[CLIENTE] INSTANCIA REMOTA CREADA: {charName}.");
                }

                // 3. Si es el personaje local, añadir el PlayerMovement2D (que contiene el script de envío 'M')
                if (charName == localPlayerName)
                {
                    // Nota: Si PlayerMovement2D ya está en el prefab, esta línea no es necesaria.
                    // Si estás usando un prefab de control, asegúrate de que tiene el RectTransform o busca en el padre.
                    // Asumimos que PlayerMovement2D ya está en el prefab principal.
                    Debug.Log($"CLIENT SPAWN: Jugador local {charName} creado.");
                }

                // 4. Guardar la instancia en el diccionario
                if (characterObject != null)
                {
                    instancedCharacters[charName] = characterObject;

                    // --- NUEVO DEBUG LOG: Confirmar que se añadió al diccionario ---
                    Debug.Log($"[CLIENTE] Añadido al diccionario: {charName}. Total de objetos: {instancedCharacters.Count}");
                    // --------------------------------------------------------------
                }
            }
        }
    }

    // =========================================================================
    // ACTUALIZACIÓN DE POSICIÓN REMOTA (MENSAJES 'R' y 'M')
    // =========================================================================

    public void UpdateRemotePlayerPosition(string characterName, Vector3 position)
    {
        GameObject playerObject = GetHostCharacterObject(characterName); // Intenta obtener el objeto estático

        print(characterName);

        if (playerObject == null) // -> Si GetHostCharacterObject es NULL, estamos en un cliente normal
        {
            // LÓGICA DE CLIENTE: Buscar el prefab instanciado en nuestro diccionario
            if (instancedCharacters.TryGetValue(characterName, out playerObject))
            {
                // Mover el objeto instanciado (Jugador Remoto)
                RectTransform rect = playerObject.GetComponent<RectTransform>();
                if (rect != null)
                {
                    // El cliente se mueve con coordenadas de UI (anchoredPosition)
                    rect.anchoredPosition = new Vector2(position.x, position.y);
                }
            }
            else
            {
                Debug.LogWarning($"CLIENTE: No se encontró la instancia de '{characterName}' para mover.");
                return;
            }
        }

        // --- CÓDIGO COMÚN (Host o Cliente Remoto) ---
        if (playerObject != null && playerObject.activeSelf)
        {
            // Si es un Host (el objeto estático se encontró arriba) o un Cliente que encontró su instancia:
            RectTransform rect = playerObject.GetComponent<RectTransform>();

            if (rect != null)
            {
                // 2. Actualizar la posición anclada con los datos recibidos
                rect.anchoredPosition = new Vector2(position.x, position.y);

                //Debug.Log($"HOST/CLIENTE UPDATED: {characterName} a {position.x:F2}, {position.y:F2}");
            }
        }
        else
        {
            Debug.LogWarning($"UpdateRemotePlayerPosition: Objeto '{characterName}' no está activo o no tiene RectTransform.");
        }
    }

    public void UpdateRemoteEnemyPosition(string characterName, Vector3 position)
    {
        GameObject playerObject = GetHostCharacterObject(characterName); // Intenta obtener el objeto estático

        print(characterName);

        RectTransform rect = gombaTransform;
        if (rect != null)
        {
            // El cliente se mueve con coordenadas de UI (anchoredPosition)
            rect.anchoredPosition = new Vector2(position.x, position.y);
        }
    }

    public void UpdateDamage(string characterName)
    {
        GameObject playerObject = GetHostCharacterObject(characterName); // Intenta obtener el objeto estático
        print(characterName + " RECIBE DAÑO");

        if (playerObject == null)
        {
            if (!instancedCharacters.TryGetValue(characterName, out playerObject))
            {
                Debug.LogWarning($"UpdateDamage: No encuentro el objeto de '{characterName}' para aplicar daño.");
                return;
            }
        }

        CharacterStats stats = playerObject.GetComponent<CharacterStats>();
        if (stats == null)
        {
            Debug.LogWarning($"UpdateDamage: El objeto '{characterName}' no tiene CharacterStats.");
            return;
        }

        stats.TakeDamage();


    }
}