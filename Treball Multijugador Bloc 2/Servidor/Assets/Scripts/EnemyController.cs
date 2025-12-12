using UnityEngine;
using Unity.Networking.Transport.Samples; // Para acceder a ServerBehaviour

public class EnemyController : MonoBehaviour
{
    // Rango de movimiento en coordenadas del Canvas (anchoredPosition)
    [Header("Movement Settings (Canvas Coordinates)")]
    public float moveSpeed = 50f;
    public float rangeX = 150f; // Distancia total de movimiento (ej., de -75 a +75)

    // Referencia al RectTransform para mover el objeto UI
    public RectTransform rectTransform;
    private Vector2 startPosition;

    void Awake()
    {
        // Solo necesitamos este script si somos el servidor (o el Host)
        if (ServerBehaviour.Instance == null)
        {
            // Si es un cliente puro, este script puede no ser necesario o debe ser desactivado.
            // Opcional: Destroy(this); si el cliente solo debe recibir la posición.
            return;
        }

        rectTransform = GetComponent<RectTransform>();

        print(rectTransform);
        if (rectTransform == null)
        {
            Debug.LogError("EnemyController requiere un RectTransform.");
            Destroy(this);
            return;
        }

        // Determinar el punto de inicio para el cálculo del seno
        startPosition = rectTransform.anchoredPosition;
    }

    void Update()
    {


        // 1. Lógica de Movimiento (Ejemplo de movimiento sinusoidal simple de ida y vuelta)

        // Calculamos la nueva posición X basada en el tiempo
        // Math.Sin(Time.time) oscila entre -1 y 1.
        float newX = startPosition.x + Mathf.Sin(Time.time * moveSpeed / rangeX) * rangeX;

        //print(newX);

        // 2. Aplicar la nueva posición al RectTransform
        Vector2 newPosition = new Vector2(newX, startPosition.y);
        rectTransform.anchoredPosition = newPosition;

        //print(newPosition);

        // 3. ¡SINCRONIZACIÓN! Enviar la nueva posición a todos los clientes
        ServerBehaviour.Instance.UpdateRemoteEnemyPositioni(newPosition);
    }
}