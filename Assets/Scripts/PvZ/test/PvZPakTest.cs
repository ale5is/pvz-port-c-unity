using UnityEngine;

namespace PvZReanim
{
    public class PvZPakTest : MonoBehaviour
    {
        private void Start()
        {
            if (PvZPakImageProvider.Instance == null)
            {
                Debug.LogError(
                    "[PvZPakTest] " +
                    "No existe PvZPakImageProvider."
                );

                return;
            }

            Sprite sprite =
                PvZPakImageProvider.Instance
                    .LoadSprite(
                        "PeaShooter_Head"
                    );

            if (sprite == null)
            {
                Debug.LogError(
                    "[PvZPakTest] " +
                    "NO se pudo cargar " +
                    "PeaShooter_Head"
                );

                return;
            }

            GameObject obj =
                new GameObject(
                    "PAK_PeaShooter_Head"
                );

            SpriteRenderer renderer =
                obj.AddComponent<SpriteRenderer>();

            renderer.sprite =
                sprite;

            obj.transform.position =
                Vector3.zero;

            Debug.Log(
                "[PvZPakTest] OK: " +
                sprite.name +
                " | " +
                sprite.texture.width +
                "x" +
                sprite.texture.height
            );
        }
    }
}