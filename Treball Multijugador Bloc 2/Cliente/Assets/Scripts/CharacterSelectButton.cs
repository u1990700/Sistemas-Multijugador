using TMPro;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Samples;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectButton : MonoBehaviour
{
    public string characterName;

    public void OnSelect()
    {
        Debug.Log($"Cliente intenta seleccionar: {characterName}");

        if (ClientBehaviour.Instance == null)
        {
            Debug.LogError("ClientBehaviour no existe en esta escena.");
            return;
        }

        ClientBehaviour.Instance.SendCharacterSelection(characterName);
    }
}
