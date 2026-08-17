using System.Collections;
using UnityEngine;

public class PvZImageTest : MonoBehaviour
{
    [Header("Imagen dentro de main.pak")]
    public string nombreImagen = "DATA/PEASHOOTER.PNG";

    [Header("Escala")]
    public float pixelsPerUnit = 100f;

    private SpriteRenderer spriteRenderer;

    private IEnumerator Start()
    {
        // Esperar a que exista el ResourceManager
        while (PvZResourceManager.Instancia == null)
            yield return null;

        // Esperar a que termine de cargar main.pak
        while (!PvZResourceManager.Instancia.EstaListo)
            yield return null;

        Debug.Log(
            "[PvZ ImageTest] ResourceManager listo. Cargando imagen..."
        );

        CargarImagen();
    }

    [ContextMenu("Cargar imagen")]
    public void CargarImagen()
    {
        if (PvZResourceManager.Instancia == null)
        {
            Debug.LogError(
                "[PvZ ImageTest] No existe PvZResourceManager."
            );

            return;
        }

        if (!PvZResourceManager.Instancia.EstaListo)
        {
            Debug.LogError(
                "[PvZ ImageTest] PvZResourceManager todavía no está listo."
            );

            return;
        }

        Debug.Log(
            $"[PvZ ImageTest] Cargando: {nombreImagen}"
        );

        Sprite sprite = PvZImageLoader.CargarSprite(
            nombreImagen,
            pixelsPerUnit
        );

        if (sprite == null)
        {
            Debug.LogError(
                $"[PvZ ImageTest] No se pudo cargar: {nombreImagen}"
            );

            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = sprite;

        Debug.Log(
            $"[PvZ ImageTest] ¡Imagen mostrada correctamente! " +
            $"{sprite.texture.width}x{sprite.texture.height}"
        );
    }
}