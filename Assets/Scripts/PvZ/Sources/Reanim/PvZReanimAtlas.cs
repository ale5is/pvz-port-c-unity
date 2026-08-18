using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Atlas de sprites para una REANIM.
/// Reúne las imágenes utilizadas por los tracks en una sola Texture2D.
/// </summary>
public sealed class PvZReanimAtlas : IDisposable
{
    private const int MaxImages = 64;
    private const int MaxImageSize = 254;
    private const int MaxAtlasSize = 2048;
    private const int Padding = 1;

    private sealed class Entry
    {
        public string key;
        public Texture2D texture;
        public int x;
        public int y;
        public Sprite sprite;
    }

    private readonly Dictionary<string, Entry> entries =
        new Dictionary<string, Entry>(
            StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Sprite> individual =
        new Dictionary<string, Sprite>(
            StringComparer.OrdinalIgnoreCase);

    private Texture2D atlasTexture;

    public Texture2D Texture => atlasTexture;

    public int Count => entries.Count;

    // ============================================================
    // CONSTRUIR ATLAS
    // ============================================================

    public bool Build(
        PvZReanimData data,
        Func<string, Texture2D> loadTexture)
    {
        Dispose();

        if (data == null ||
            data.tracks == null ||
            loadTexture == null)
        {
            return false;
        }

        HashSet<string> names =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (PvZReanimTrack track in data.tracks)
        {
            if (track == null ||
                track.frames == null)
            {
                continue;
            }

            foreach (PvZReanimFrame frame in track.frames)
            {
                if (frame == null ||
                    string.IsNullOrWhiteSpace(frame.image))
                {
                    continue;
                }

                names.Add(
                    frame.image.Trim());
            }
        }

        List<Entry> list =
            new List<Entry>();

        foreach (string name in names)
        {
            if (list.Count >= MaxImages)
                break;

            Texture2D texture =
                loadTexture(name);

            if (texture == null)
                continue;

            if (texture.width > MaxImageSize ||
                texture.height > MaxImageSize)
            {
                continue;
            }

            list.Add(
                new Entry
                {
                    key = name,
                    texture = texture
                });
        }

        if (list.Count == 0)
            return false;

        // Primero las imágenes más altas.
        list.Sort(
            (a, b) =>
            {
                int resultado =
                    b.texture.height.CompareTo(
                        a.texture.height);

                if (resultado != 0)
                    return resultado;

                return
                    b.texture.width.CompareTo(
                        a.texture.width);
            });

        int width =
            NextPowerOfTwo(
                Mathf.CeilToInt(
                    Mathf.Sqrt(
                        TotalArea(list))));

        width =
            Mathf.Clamp(
                Mathf.Max(
                    width,
                    LargestWidth(list)),
                1,
                MaxAtlasSize);

        int height;

        if (!Pack(
            list,
            width,
            out height))
        {
            return false;
        }

        // ========================================================
        // CREAR TEXTURA
        // ========================================================

        atlasTexture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                false);

        atlasTexture.name =
            "PvZ_ReanimAtlas";

        atlasTexture.filterMode =
            FilterMode.Point;

        atlasTexture.wrapMode =
            TextureWrapMode.Clamp;

        atlasTexture.SetPixels32(
            new Color32[
                width * height]);

        // ========================================================
        // COPIAR IMÁGENES
        // ========================================================

        foreach (Entry entry in list)
        {
            Color32[] pixels =
                entry.texture.GetPixels32();

            atlasTexture.SetPixels32(
                entry.x,
                entry.y,
                entry.texture.width,
                entry.texture.height,
                pixels);
        }

        atlasTexture.Apply(
            false,
            false);

        // ========================================================
        // CREAR SPRITES
        // ========================================================

        foreach (Entry entry in list)
        {
            entry.sprite =
                Sprite.Create(
                    atlasTexture,
                    new Rect(
                        entry.x,
                        entry.y,
                        entry.texture.width,
                        entry.texture.height),
                    new Vector2(
                        0.5f,
                        0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);

            entry.sprite.name =
                entry.key;

            entries[
                entry.key] =
                entry;

            UnityEngine.Object.Destroy(entry.texture);

            entry.texture = null;
        }

        Debug.Log(
            "[PvZ Reanim Atlas] " +
            width +
            "x" +
            height +
            " | imágenes=" +
            entries.Count);

        return true;
    }

    // ============================================================
    // OBTENER SPRITE DEL ATLAS
    // ============================================================

    public Sprite Get(
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        Entry entry;

        if (entries.TryGetValue(
            name.Trim(),
            out entry))
        {
            return entry.sprite;
        }

        return null;
    }

    // ============================================================
    // SPRITE INDIVIDUAL
    // ============================================================

    public Sprite GetIndividual(
        string name,
        Func<string, Texture2D> loadTexture)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            loadTexture == null)
        {
            return null;
        }

        name =
            name.Trim();

        Sprite sprite;

        if (individual.TryGetValue(
            name,
            out sprite))
        {
            return sprite;
        }

        Texture2D texture =
            loadTexture(name);

        if (texture == null)
            return null;

        texture.filterMode =
            FilterMode.Point;

        texture.wrapMode =
            TextureWrapMode.Clamp;

        sprite =
            Sprite.Create(
                texture,
                new Rect(
                    0,
                    0,
                    texture.width,
                    texture.height),
                new Vector2(
                    0.5f,
                    0.5f),
                100f);

        sprite.name =
            name;

        individual[name] =
            sprite;

        return sprite;
    }

    // ============================================================
    // UTILIDADES
    // ============================================================

    private static int TotalArea(
        List<Entry> list)
    {
        int area = 0;

        foreach (Entry entry in list)
        {
            area +=
                (entry.texture.width + 2) *
                (entry.texture.height + 2);
        }

        return area;
    }

    private static int LargestWidth(
        List<Entry> list)
    {
        int value = 1;

        foreach (Entry entry in list)
        {
            value =
                Mathf.Max(
                    value,
                    entry.texture.width + 2);
        }

        return value;
    }

    private static bool Pack(
        List<Entry> list,
        int width,
        out int height)
    {
        int x = Padding;
        int y = Padding;

        int rowHeight = 0;

        height = 0;

        foreach (Entry entry in list)
        {
            int w =
                entry.texture.width;

            int h =
                entry.texture.height;

            // Siguiente fila.
            if (x + w + Padding > width)
            {
                x = Padding;

                y +=
                    rowHeight +
                    Padding;

                rowHeight = 0;
            }

            entry.x = x;
            entry.y = y;

            x +=
                w +
                Padding;

            rowHeight =
                Mathf.Max(
                    rowHeight,
                    h);

            height =
                Mathf.Max(
                    height,
                    y +
                    h +
                    Padding);
        }

        height =
            NextPowerOfTwo(
                height);

        return
            width <= MaxAtlasSize &&
            height <= MaxAtlasSize;
    }

    private static int NextPowerOfTwo(
        int value)
    {
        int result = 1;

        while (
            result < value &&
            result < MaxAtlasSize)
        {
            result <<= 1;
        }

        return
            Mathf.Min(
                result,
                MaxAtlasSize);
    }

    // ============================================================
    // LIMPIEZA
    // ============================================================

    public void Dispose()
    {
        foreach (Entry entry in entries.Values)
        {
            if (entry.sprite != null)
            {
                UnityEngine.Object.Destroy(
                    entry.sprite);
            }
        }

        entries.Clear();

        foreach (Sprite sprite in individual.Values)
        {
            if (sprite == null)
                continue;

            Texture2D texture =
                sprite.texture;

            UnityEngine.Object.Destroy(
                sprite);

            if (texture != null)
            {
                UnityEngine.Object.Destroy(
                    texture);
            }
        }

        individual.Clear();

        if (atlasTexture != null)
        {
            UnityEngine.Object.Destroy(
                atlasTexture);
        }

        atlasTexture = null;
    }

    public void OnDestroy()
    {
        Dispose();
    }
}