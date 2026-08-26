using UnityEngine;

namespace PvZReanim
{
    // Sólo resuelve sprites contra el PAK (PvZPakImageProvider).
    // Antes también podía tirar de un PvZReanimAtlas (sprites
    // sueltos de Unity) y de Resources.Load, pero ya no se van a
    // usar texturas/animaciones fuera del .pak, así que esas rutas
    // se sacaron (junto con el registro manual de sprites, que
    // sólo usaba el PvZReanimSpriteLoader que también se borró).
    public class PvZReanimImageProvider : MonoBehaviour
    {
        [Header("PAK")]
        [SerializeField]
        private bool searchPak = true;

        [SerializeField]
        private PvZPakImageProvider pakProvider;

        public bool SearchPak
        {
            get
            {
                return searchPak;
            }

            set
            {
                searchPak = value;
            }
        }

        public PvZPakImageProvider PakProvider
        {
            get
            {
                return pakProvider;
            }

            set
            {
                pakProvider = value;
            }
        }

        private void Awake()
        {
            FindPakProvider();
        }

        private void FindPakProvider()
        {
            if (pakProvider != null)
                return;

            pakProvider =
                PvZPakImageProvider.Instance;

            if (pakProvider != null)
                return;

            pakProvider =
                FindFirstObjectByType<
                    PvZPakImageProvider
                >();
        }

        public virtual Sprite Resolve(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return null;
            }

            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(
                    normalized))
            {
                return null;
            }

            if (!searchPak)
                return null;

            FindPakProvider();

            if (pakProvider == null ||
                !pakProvider.IsReady)
            {
                return null;
            }

            Sprite sprite =
                pakProvider.LoadSprite(
                    imageName
                );

            if (sprite != null)
            {
                Debug.Log(
                    "[PvZReanimImageProvider] " +
                    "Sprite resuelto desde PAK: " +
                    imageName,
                    this
                );
            }

            return sprite;
        }

        public virtual bool TryResolve(
            string imageName,
            out Sprite sprite)
        {
            sprite =
                Resolve(
                    imageName
                );

            return sprite != null;
        }

        public virtual bool Contains(
            string imageName)
        {
            return Resolve(
                imageName
            ) != null;
        }

        public void SetPakProvider(
            PvZPakImageProvider newProvider)
        {
            pakProvider =
                newProvider;
        }

        public PvZPakImageProvider GetPakProvider()
        {
            FindPakProvider();

            return pakProvider;
        }
    }
}
