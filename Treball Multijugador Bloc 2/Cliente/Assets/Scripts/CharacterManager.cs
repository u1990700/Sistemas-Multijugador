using TMPro;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Samples;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.UI;

public class CharacterManager : MonoBehaviour
{

    private void Start()
    {
        GameObject canvas = GameObject.Find("Canvas");
        print(canvas);

        if (ClientBehaviour.Instance.perro == true) {

            Debug.Log("Perro visto");

            Transform perroTransform = canvas.transform.Find("perroPersonaje");
            if (perroTransform != null)
            {
                perroTransform.gameObject.SetActive(true);
            }
        }
        if (ClientBehaviour.Instance.creeper == true)
        {
            Transform creeperTransform = canvas.transform.Find("creeperPersonaje");
            if (creeperTransform != null)
            {
                creeperTransform.gameObject.SetActive(true);
            }
        }
    }
    private void Update()
    {
        
    }
}
