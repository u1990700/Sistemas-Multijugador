// LocalPlayerController.cs (NUEVO SCRIPT)
using UnityEngine;
using Unity.Networking.Transport.Samples; // Necesario para acceder a ClientBehaviour

public class LocalPlayerController : MonoBehaviour
{
    private Vector3 lastPosition;
    public float movementSpeed = 5.0f;
    public float positionUpdateThreshold = 0.1f; // Manda update solo si se mueve 10cm

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        // 1. Manejo de Input (Ejemplo básico 2D/3D)
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(x, y, 0) * movementSpeed * Time.deltaTime;
        transform.position += movement;

        // 2. Comprobar y Enviar Actualización
        if ((transform.position - lastPosition).sqrMagnitude > positionUpdateThreshold * positionUpdateThreshold)
        {
            // La posición ha cambiado lo suficiente, enviar al servidor.
            if (ClientBehaviour.Instance != null)
            {
                ClientBehaviour.Instance.SendMovementUpdate(transform.position);
                Debug.Log($"CLIENTE ENVÍA [M]: Posición {transform.position.x:F2}, {transform.position.y:F2}");
            }
            lastPosition = transform.position;
        }
    }
}