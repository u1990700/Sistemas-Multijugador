using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    public string characterName;    // "Perro", "Creeper", etc.
    public int maxHealth = 3;
    public int currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage()
    {
        currentHealth -= 1;
        Debug.Log($"{characterName} recibe 1 de daño. Vida actual: {currentHealth}");

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{characterName} ha muerto.");
        // Aquí haces lo que toque:
        // - Desactivar el objeto
        // - Enviar mensaje al servidor
        // - Cambiar de escena, etc.
        gameObject.SetActive(false);
    }
}
