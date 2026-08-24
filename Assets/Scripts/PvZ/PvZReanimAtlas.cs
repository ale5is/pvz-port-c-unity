using System.Collections.Generic;
using UnityEngine;

namespace PvZReanim
{
    [CreateAssetMenu(
        fileName = "PvZReanimAtlas",
        menuName = "PvZ/Reanim Atlas"
    )]
    public class PvZReanimAtlas : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string name;

            public Sprite sprite;
        }

        [SerializeField]
        private List<Entry> entries =
            new List<Entry>();

        private Dictionary<string, Sprite> spriteCache;

        public int Count =>
            entries != null
                ? entries.Count
                : 0;

        private void OnEnable()
        {
            BuildCache();
        }

        public void BuildCache()
        {
            spriteCache =
                new Dictionary<string, Sprite>(
                    System.StringComparer.OrdinalIgnoreCase
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

                if (string.IsNullOrEmpty(
                    entry.name))
                {
                    continue;
                }

                if (entry.sprite == null)
                    continue;

                spriteCache[
                    entry.name
                ] =
                    entry.sprite;
            }
        }

        public Sprite GetSprite(
            string imageName)
        {
            if (string.IsNullOrEmpty(
                imageName))
            {
                return null;
            }

            if (spriteCache == null)
            {
                BuildCache();
            }

            Sprite sprite;

            if (spriteCache.TryGetValue(
                imageName,
                out sprite))
            {
                return sprite;
            }

            string normalized =
                NormalizeName(
                    imageName
                );

            if (!string.Equals(
                normalized,
                imageName,
                System.StringComparison.OrdinalIgnoreCase))
            {
                if (spriteCache.TryGetValue(
                    normalized,
                    out sprite))
                {
                    return sprite;
                }
            }

            return null;
        }

        public bool Contains(
            string imageName)
        {
            return GetSprite(
                imageName
            ) != null;
        }

        public void SetSprite(
            string imageName,
            Sprite sprite)
        {
            if (string.IsNullOrEmpty(
                imageName))
            {
                return;
            }

            if (sprite == null)
                return;

            if (entries == null)
            {
                entries =
                    new List<Entry>();
            }

            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                Entry entry =
                    entries[i];

                if (entry == null)
                    continue;

                if (!string.Equals(
                    entry.name,
                    imageName,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entry.sprite =
                    sprite;

                BuildCache();

                return;
            }

            Entry newEntry =
                new Entry();

            newEntry.name =
                imageName;

            newEntry.sprite =
                sprite;

            entries.Add(
                newEntry
            );

            BuildCache();
        }

        public bool RemoveSprite(
            string imageName)
        {
            if (entries == null ||
                string.IsNullOrEmpty(
                    imageName))
            {
                return false;
            }

            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                Entry entry =
                    entries[i];

                if (entry == null)
                    continue;

                if (!string.Equals(
                    entry.name,
                    imageName,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entries.RemoveAt(i);

                BuildCache();

                return true;
            }

            return false;
        }

        public void Clear()
        {
            if (entries == null)
                return;

            entries.Clear();

            BuildCache();
        }

        public string GetSpriteName(
            Sprite sprite)
        {
            if (sprite == null ||
                entries == null)
            {
                return null;
            }

            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                Entry entry =
                    entries[i];

                if (entry == null)
                    continue;

                if (entry.sprite == sprite)
                {
                    return entry.name;
                }
            }

            return null;
        }

        private static string NormalizeName(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string result =
                value.Trim();

            int slash =
                Mathf.Max(
                    result.LastIndexOf('/'),
                    result.LastIndexOf('\\')
                );

            if (slash >= 0 &&
                slash + 1 < result.Length)
            {
                result =
                    result.Substring(
                        slash + 1
                    );
            }

            if (result.EndsWith(
                ".png",
                System.StringComparison.OrdinalIgnoreCase))
            {
                result =
                    result.Substring(
                        0,
                        result.Length - 4
                    );
            }

            if (result.EndsWith(
                ".jpg",
                System.StringComparison.OrdinalIgnoreCase))
            {
                result =
                    result.Substring(
                        0,
                        result.Length - 4
                    );
            }

            if (result.EndsWith(
                ".jpeg",
                System.StringComparison.OrdinalIgnoreCase))
            {
                result =
                    result.Substring(
                        0,
                        result.Length - 5
                    );
            }

            return result;
        }
    }
}