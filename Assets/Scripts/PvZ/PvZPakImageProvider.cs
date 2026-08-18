using System;
using System.IO;
using UnityEngine;

namespace PvZReanim
{
    public class PvZPakImageProvider : MonoBehaviour
    {
        public static PvZPakImageProvider Instance { get; private set; }

        [Header("PAK")]
        [SerializeField]
        private string pakFileName = "main.pak";

        [Header("Debug")]
        [SerializeField]
        private bool logLoads = true;

        private PvZPakReader pakReader;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);

            LoadPak();
        }

        private void LoadPak()
        {
            string path = Path.Combine(
                Application.streamingAssetsPath,
                "PvZ",
                pakFileName
            );

            pakReader = new PvZPakReader();

            if (!pakReader.Load(path))
            {
                Debug.LogError(
                    "[PvZPakImageProvider] " +
                    "No se pudo cargar: " +
                    path,
                    this
                );

                pakReader = null;
                return;
            }

            Debug.Log(
                "[PvZPakImageProvider] PAK listo | " +
                "Archivos: " +
                pakReader.FileCount,
                this
            );
        }

        public bool IsReady
        {
            get
            {
                return pakReader != null &&
                       pakReader.IsLoaded;
            }
        }

        public Texture2D LoadTexture(
            string imageName)
        {
            if (!IsReady)
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "El PAK todavía no está disponible.",
                    this
                );

                return null;
            }

            string path =
                ResolveImagePath(imageName);

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (!pakReader.TryGetFile(
                    path,
                    out byte[] data))
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "No se encontró: " +
                    path,
                    this
                );

                return null;
            }

            Texture2D texture =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false
                );

            texture.name =
                Path.GetFileNameWithoutExtension(
                    path
                );

            if (!texture.LoadImage(
                    data,
                    false))
            {
                Destroy(texture);

                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "El recurso no es una imagen válida: " +
                    path,
                    this
                );

                return null;
            }

            if (logLoads)
            {
                Debug.Log(
                    "[PvZPakImageProvider] " +
                    "Imagen cargada: " +
                    path +
                    " | " +
                    texture.width +
                    "x" +
                    texture.height,
                    this
                );
            }

            return texture;
        }

        public Sprite LoadSprite(
            string imageName)
        {
            Texture2D texture =
                LoadTexture(imageName);

            if (texture == null)
                return null;

            Sprite sprite =
                Sprite.Create(
                    texture,
                    new Rect(
                        0,
                        0,
                        texture.width,
                        texture.height
                    ),
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    100f
                );

            sprite.name =
                texture.name;

            return sprite;
        }

        private string ResolveImagePath(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return null;
            }

            string name =
                imageName.Trim();

            name =
                name.Replace(
                    '\\',
                    '/'
                );

            // Ya viene con ruta completa.
            if (name.StartsWith(
                    "reanim/",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (name.EndsWith(
                        ".png",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }

                return name + ".png";
            }

            // Quitar extensión si el Reanim
            // solamente proporciona el nombre.
            if (name.EndsWith(
                    ".png",
                    StringComparison.OrdinalIgnoreCase))
            {
                name =
                    name.Substring(
                        0,
                        name.Length - 4
                    );
            }

            return
                "reanim/" +
                name +
                ".png";
        }
    }
}