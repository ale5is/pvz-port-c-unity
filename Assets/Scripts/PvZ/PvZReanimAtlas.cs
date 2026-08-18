using System;
using System.Collections.Generic;
using UnityEngine;

namespace PvZReanim
{
    /// <summary>
    /// Tabla de imágenes utilizadas por una reanimación.
    ///
    /// El .reanim guarda el nombre/referencia de la imagen.
    /// Este objeto se encarga de resolver esa referencia
    /// a un Sprite de Unity.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PvZReanimAtlas",
        menuName = "PvZ/Reanim Atlas"
    )]
    public class PvZReanimAtlas : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public Sprite sprite;
        }

        [SerializeField]
        private List<Entry> entries =
            new List<Entry>();

        private Dictionary<string, Sprite> lookup;

        private void OnEnable()
        {
            BuildLookup();
        }

        /// <summary>
        /// Reconstruye la tabla interna de búsqueda.
        /// </summary>
        public void BuildLookup()
        {
            lookup =
                new Dictionary<string, Sprite>(
                    StringComparer.OrdinalIgnoreCase
                );

            if (entries == null)
                return;

            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                Entry entry =
                    entries[i];

                if (entry == null)
                    continue;

                if (string.IsNullOrEmpty(entry.name))
                    continue;

                if (entry.sprite == null)
                    continue;

                lookup[NormalizeName(entry.name)] =
                    entry.sprite;
            }
        }

        /// <summary>
        /// Busca un Sprite por nombre.
        /// </summary>
        public Sprite GetSprite(
            string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return null;

            if (lookup == null)
                BuildLookup();

            string normalized =
                NormalizeName(
                    imageName
                );

            if (lookup.TryGetValue(
                    normalized,
                    out Sprite sprite))
            {
                return sprite;
            }

            return null;
        }

        /// <summary>
        /// Comprueba si existe una imagen.
        /// </summary>
        public bool Contains(
            string imageName)
        {
            return GetSprite(
                imageName
            ) != null;
        }

        /// <summary>
        /// Asigna un Sprite a un nombre.
        /// </summary>
        public void SetSprite(
            string imageName,
            Sprite sprite)
        {
            if (string.IsNullOrEmpty(imageName))
                return;

            if (entries == null)
            {
                entries =
                    new List<Entry>();
            }

            string normalized =
                NormalizeName(
                    imageName
                );

            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                Entry entry =
                    entries[i];

                if (entry == null)
                    continue;

                if (NormalizeName(entry.name) !=
                    normalized)
                {
                    continue;
                }

                entry.sprite =
                    sprite;

                BuildLookup();

                return;
            }

            entries.Add(
                new Entry
                {
                    name = imageName,
                    sprite = sprite
                }
            );

            BuildLookup();
        }

        /// <summary>
        /// Elimina una entrada.
        /// </summary>
        public bool Remove(
            string imageName)
        {
            if (entries == null ||
                string.IsNullOrEmpty(imageName))
            {
                return false;
            }

            string normalized =
                NormalizeName(
                    imageName
                );

            for (int i = entries.Count - 1;
                 i >= 0;
                 i--)
            {
                Entry entry =
                    entries[i];

                if (entry == null)
                    continue;

                if (NormalizeName(entry.name) !=
                    normalized)
                {
                    continue;
                }

                entries.RemoveAt(i);

                BuildLookup();

                return true;
            }

            return false;
        }

        public int Count
        {
            get
            {
                return entries != null
                    ? entries.Count
                    : 0;
            }
        }

        public Entry GetEntry(
            int index)
        {
            if (entries == null ||
                index < 0 ||
                index >= entries.Count)
            {
                return null;
            }

            return entries[index];
        }

        private static string NormalizeName(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value =
                value.Trim();

            value =
                value.Replace(
                    '\\',
                    '/'
                );

            int slash =
                value.LastIndexOf('/');

            if (slash >= 0 &&
                slash + 1 < value.Length)
            {
                value =
                    value.Substring(
                        slash + 1
                    );
            }

            int extension =
                value.LastIndexOf('.');

            if (extension > 0)
            {
                string ext =
                    value.Substring(
                        extension
                    );

                if (ext.Equals(
                        ".png",
                        StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(
                        ".jpg",
                        StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(
                        ".jpeg",
                        StringComparison.OrdinalIgnoreCase))
                {
                    value =
                        value.Substring(
                            0,
                            extension
                        );
                }
            }

            return value.ToLowerInvariant();
        }
    }
}