using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport.Samples; // Necesario para la struct CharacterSpawnData

public class GameManager : MonoBehaviour
{
    // 1. Singleton: Acceso est�tico a la �nica instancia
    public static GameManager Instance;
    [SerializeField] private GameObject perro;
    [SerializeField] private GameObject creeper;

    // --- NUEVA ESTRUCTURA DE DATOS P�BLICA (para compartir entre scripts) ---
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



    // Asigna los prefabs de los personajes aqu� en el Inspector
    public List<CharacterPrefabMapping> characterPrefabs;

    // Referencia al prefab del script de control local del jugador (p. ej., un componente de movimiento)
    public GameObject LocalPlayerControlPrefab;

    // Fragmento de GameManager.cs (M�todo SpawnCharacters)





    public void SpawnCharacters(List<CharacterSpawnData> spawnData, string localPlayerName)
    {
        Debug.Log($"Iniciando spawning de {spawnData.Count} personajes. Local player: {localPlayerName}");

        foreach (var data in spawnData)
        {
            Debug.Log("Hola");
            // 1. Encontrar el Prefab correcto
            GameObject charPrefab = characterPrefabs.Find(p => p.characterName == data.CharacterName).prefab;

            if (charPrefab == null)
            {
                Debug.LogError($"Prefab no encontrado para el personaje: {data.CharacterName}. Revise la lista 'characterPrefabs' en el Inspector.");
                continue;
            }

            // 2. Instanciar el personaje
            GameObject newChar = Instantiate(charPrefab, data.Position, Quaternion.identity);

            // 3. L�gica de Control: A�adir controles al jugador local
            if (data.CharacterName == localPlayerName && LocalPlayerControlPrefab != null)
            {
                // Este es MI personaje.
                Debug.Log($"SPAWN -> JUGADOR LOCAL: {localPlayerName}");
                // Instanciar el prefab que contiene el script de movimiento/input del jugador
                // newChar.AddComponent<LocalMovementController>(); // Si es un componente
                Instantiate(LocalPlayerControlPrefab, newChar.transform); // Si es un objeto hijo
            }
            else
            {
                // Este es el personaje de otro cliente o del servidor (s�lo representaci�n).
                Debug.Log($"SPAWN -> JUGADOR REMOTO: {data.CharacterName}");
                // Si tiene componentes de Input/Movimiento, �DEBEN DESACTIVARSE aqu�!
                // Example: Destroy(newChar.GetComponent<LocalMovementController>()); 
            }
        }
    }

    // Fragmento de GameManager.cs (NUEVO M�TODO)

    public void UpdateRemotePlayerPosition(string characterName, Vector3 position)
    {
        // Buscar el GameObject del personaje por su nombre
        // NOTA: Esto asume que el GameObject del personaje tiene un script que almacena
        // su nombre, o que el nombre del GameObject es �nico y coincide con characterName.

        // Una forma simple (si los personajes son pocos y sabes sus nombres):
        GameObject remotePlayer = GameObject.Find(characterName + "(Clone)"); // Asumiendo que usas (Clone)

        if (remotePlayer != null)
        {
            // Simple teleport: lo mueve a la nueva posici�n
            remotePlayer.transform.position = position;

            // Si necesitas un movimiento m�s suave (interpolaci�n):
            // remotePlayer.GetComponent<RemoteMovementComponent>().targetPosition = position; 
        }
        else
        {
            Debug.LogWarning($"No se encontr� el GameObject para el personaje remoto: {characterName}");
        }
    }



}