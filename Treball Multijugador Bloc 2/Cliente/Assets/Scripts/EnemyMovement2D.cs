using UnityEngine;

public class EnemyMovement2D : MonoBehaviour
{
    public float speed = 100f;      // Velocidad horizontal
    public float jumpForce = 100f;  // Fuerza del salto
    Rigidbody2D rb;
    //bool isGrounded = false;      // Para saber si est� tocando el suelo


    // Variables de sincronización de red
    public RectTransform rectTransform; // Para leer/escribir la posición UI
    private Vector2 lastPositionSent;
    public float positionUpdateThreshold = 0.5f; // Umbral más alto para física

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); // Obtenemos el Rigidbody2D del personaje


        // Obtenemos el RectTransform para la sincronización de UI
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("EnemyMovement2D requiere un RectTransform para sincronización de red.");
            enabled = false;
        }

    }

    private void Start()
    {
        string nombreObjeto = gameObject.name;

         // Inicializar la última posición enviada con la posición anclada inicial
         rectTransform.anchoredPosition = new Vector2(-25f, -40f);
         lastPositionSent = rectTransform.anchoredPosition;
         Debug.Log($"INICIO ENEMIGO (Física): Posición inicial forzada a {lastPositionSent.x:F2}, {lastPositionSent.y:F2}");

        
    }

    void Update()
    {
    }

    void FixedUpdate()
    {
        // Sincronización de posición con el servidor
    }

    // Detectar cu�ndo toca el suelo
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Si chocamos con un objeto con tag "Ground", consideramos que estamos en el suelo
        //if (collision.collider.CompareTag("Ground"))
        //{
        //    isGrounded = true;
        //}
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        //if (collision.collider.CompareTag("Ground"))
        //{
        //    isGrounded = false;
        //}
    }
}
