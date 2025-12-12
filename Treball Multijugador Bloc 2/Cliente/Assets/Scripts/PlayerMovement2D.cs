using System;
using TMPro;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Samples;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.UI;

public class PlayerMovement2D : MonoBehaviour
{
    public float speed = 100f;      // Velocidad horizontal
    public float jumpForce = 100f;  // Fuerza del salto
    Rigidbody2D rb;
    bool isGrounded = false;      // Para saber si est� tocando el suelo


    // Variables de sincronización de red
    private RectTransform rectTransform; // Para leer/escribir la posición UI
    private Vector2 lastPositionSent;
    public float positionUpdateThreshold = 0.5f; // Umbral más alto para física

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); // Obtenemos el Rigidbody2D del personaje


        // Obtenemos el RectTransform para la sincronización de UI
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("PlayerMovement2D requiere un RectTransform para sincronización de red.");
            enabled = false;
        }

    }

    private void Start()
    {
        string nombreObjeto = gameObject.name;
        if (ClientBehaviour.Instance.perro && nombreObjeto != "perroPersonaje") {
            enabled = false;
            return;

        }

        if (ClientBehaviour.Instance.creeper && nombreObjeto != "creeperPersonaje")
        {
            enabled = false;
            return;
        }

        if (enabled)
        {
            // Inicializar la última posición enviada con la posición anclada inicial
            rectTransform.anchoredPosition = new Vector2(-75f, 0f);
            lastPositionSent = rectTransform.anchoredPosition;
            Debug.Log($"INICIO (Física): Posición inicial forzada a {lastPositionSent.x:F2}, {lastPositionSent.y:F2}");

        }
    }

    void Update()
    {
        // Leer input horizontal (A/D, flechas izquierda/derecha)
        float inputX = Input.GetAxisRaw("Horizontal");

        // Mover al personaje usando la velocidad del rigidbody
        rb.linearVelocity = new Vector2(inputX * speed, rb.linearVelocity.y);

        // Saltar: solo si est� en el suelo
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // Ponemos la velocidad vertical directamente para un salto "seco"
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    void FixedUpdate()
    {
        // Sincronización de posición con el servidor
        if (ClientBehaviour.Instance != null)
        {
            Vector2 currentPosition = rectTransform.anchoredPosition;

            // Comprobar si la posición ha cambiado lo suficiente
            if ((currentPosition - lastPositionSent).sqrMagnitude > positionUpdateThreshold * positionUpdateThreshold)
            {
                // Enviar la nueva posición al servidor
                ClientBehaviour.Instance.SendMovementUpdate(new Vector3(currentPosition.x, currentPosition.y, 0));

                // Actualizar la última posición enviada
                lastPositionSent = currentPosition;

                // --- DEBUG LOG ---
                //Debug.Log($"CLIENTE ENVÍA [M]: Posición {lastPositionSent.x:F2}, {lastPositionSent.y:F2}");
            }
        }
    }

    public void takeDamage()
    {

    }

    // Detectar cu�ndo toca el suelo
    private void OnCollisionEnter2D(Collision2D collision)
    {
        //print(collision.gameObject.name + " HOLITA");
        // Si chocamos con un objeto con tag "Ground", consideramos que estamos en el suelo
        if (collision.collider.CompareTag("Ground"))
        {
            isGrounded = true;
        }

        // 'collision.gameObject' es el otro objeto con el que colisionaste
        // 'collision.gameObject.tag' obtiene la etiqueta de ese objeto
        if (collision.collider.CompareTag("Player"))
        {
            Debug.Log("¡Colisión con un player! (Etiqueta: " + collision.gameObject.name + ")");
            // Aquí va la lógica para cuando tu personaje choca con un amigo:
            // - Reproducir sonido
            // - Dar un bonus
            // - Cambiar de estado, etc.
            // Ejemplo: Destroy(collision.gameObject); // Si quieres destruir al amigo
        }
        
        if (collision.collider.CompareTag("Enemigo"))
        {
            Debug.Log("¡Colisión con un enemigo!");
            // Lógica para enemigos
        }
    }



    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
}
