// LocalPlayerController.cs

using UnityEngine;
using Unity.Networking.Transport.Samples;

public class LocalPlayerController : MonoBehaviour
{
    // Usaremos RectTransform en lugar de Transform
    private RectTransform rectTransform;
    private Vector3 lastPosition;

    public float movementSpeed = 5.0f;
    public float positionUpdateThreshold = 0.1f; // Manda update solo si se mueve 10cm

    void Start()
    {
        // 1. Obtener el RectTransform
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("LocalPlayerController requiere un RectTransform (Objeto UI).");
            enabled = false;
            return;
        }

        // Leer la posición anclada local (la que se ve en el Inspector)
        lastPosition = rectTransform.anchoredPosition;
    }

    void Update()
    {
        if (rectTransform == null) return;

        // 1. Manejo de Input 
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(x, y, 0) * movementSpeed * Time.deltaTime;

        // 2. Aplicar el movimiento a la posición anclada
        rectTransform.anchoredPosition += (Vector2)movement;

        // 3. Comprobar y Enviar Actualización
        // Comparamos con la posición anclada
        if ((rectTransform.anchoredPosition - (Vector2)lastPosition).sqrMagnitude > positionUpdateThreshold * positionUpdateThreshold)
        {
            // Enviamos la posición anclada (la Pos X, Pos Y del Inspector)
            if (ClientBehaviour.Instance != null)
            {
                // Convertimos el Vector2 de anchoredPosition a Vector3 para el envío
                ClientBehaviour.Instance.SendMovementUpdate(new Vector3(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y, 0));
            }
            // Actualizamos la última posición enviada
            lastPosition = rectTransform.anchoredPosition;

            // --- DEBUG LOG ---
            Debug.Log($"CLIENTE ENVÍA [M]: Posición {lastPosition.x:F2}, {lastPosition.y:F2}");
        }
    }
}