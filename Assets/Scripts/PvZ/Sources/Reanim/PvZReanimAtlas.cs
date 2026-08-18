using System;
using System.Collections.Generic;
using UnityEngine;

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

        public int width;
        public int height;
    }

    private readonly Dictionary<string, Entry> entries =
        new Dictionary<string, Entry>(
            StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Texture2D> individual =
        new Dictionary<string, Texture2D>(
            StringComparer.OrdinalIgnoreCase);

    private Texture2D atlasTexture;

    public Texture2D Texture
    {
        get
        {
            return atlasTexture;
        }
    }

    public int Count
    {
        get
        {
            return entries.Count;
        }
    }

    // ============================================================
    // BUILD
    // ============================================================

    public bool Build(
        PvZReanimData data,
        Func<string, Texture2D> loadTexture)
    {
        Dispose();

        if (
            data == null ||
            data.tracks == null ||
            loadTexture == null)
        {
            return false;
        }

        HashSet<string> nombres =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        // ========================================================
        // Encontrar imágenes
        // ========================================================

        foreach (
            PvZReanimTrack track
            in data.tracks)
        {
            if (
                track == null ||
                track.frames == null)
            {
                continue;
            }

            foreach (
                PvZReanimFrame frame
                in track.frames)
            {
                if (
                    frame == null ||
                    string.IsNullOrWhiteSpace(
                        frame.image))
                {
                    continue;
                }

                // Resodded no mete al atlas imágenes
                // que realmente sean spritesheets.
                if (frame.imageFrame > 0)
                {
                    continue;
                }

                nombres.Add(
                    frame.image.Trim());
            }
        }

        List<Entry> lista =
            new List<Entry>();

        foreach (string nombre in nombres)
        {
            if (
                lista.Count >=
                MaxImages)
            {
                break;
            }

            Texture2D textura =
                loadTexture(nombre);

            if (textura == null)
            {
                continue;
            }

            if (
                textura.width >
                MaxImageSize ||
                textura.height >
                MaxImageSize)
            {
                // No se mete al atlas.
                continue;
            }

            lista.Add(
                new Entry
                {
                    key = nombre,
                    texture = textura,
                    width = textura.width,
                    height = textura.height
                });
        }

        if (lista.Count == 0)
        {
            return false;
        }

        // ========================================================
        // Orden igual que Resodded:
        // altura descendente
        // luego ancho descendente.
        // ========================================================

        lista.Sort(
            delegate (
                Entry a,
                Entry b)
            {
                int resultado =
                    b.height.CompareTo(
                        a.height);

                if (resultado != 0)
                {
                    return resultado;
                }

                return
                    b.width.CompareTo(
                        a.width);
            });

        // ========================================================
        // WIDTH
        // ========================================================

        int totalArea = 0;
        int mayorAncho = 1;

        foreach (Entry entry in lista)
        {
            totalArea +=
                (entry.width + 2) *
                (entry.height + 2);

            mayorAncho =
                Mathf.Max(
                    mayorAncho,
                    entry.width + 2);
        }

        int width =
            NextPowerOfTwo(
                Mathf.CeilToInt(
                    Mathf.Sqrt(
                        totalArea)));

        width =
            Mathf.Clamp(
                Mathf.Max(
                    width,
                    mayorAncho),
                1,
                MaxAtlasSize);

        // ========================================================
        // PACK
        // ========================================================

        int height;

        if (!Pack(
            lista,
            width,
            out height))
        {
            return false;
        }

        // ========================================================
        // TEXTURA
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

        Color32[] pixels =
            new Color32[
                width * height];

        atlasTexture.SetPixels32(
            pixels);

        // ========================================================
        // COPIAR
        // ========================================================

        foreach (Entry entry in lista)
        {
            Color32[] source =
                entry.texture.GetPixels32();

            atlasTexture.SetPixels32(
                entry.x,
                entry.y,
                entry.width,
                entry.height,
                source);
        }

        atlasTexture.Apply(
            false,
            false);

        // ========================================================
        // SPRITES
        // ========================================================

        foreach (Entry entry in lista)
        {
            entry.sprite =
                Sprite.Create(
                    atlasTexture,
                    new Rect(
                        entry.x,
                        entry.y,
                        entry.width,
                        entry.height),
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

            Destroy(
                entry.texture);

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
    // GET SPRITE
    // ============================================================

    public Sprite Get(
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Entry entry;

        if (
            entries.TryGetValue(
                name.Trim(),
                out entry))
        {
            return entry.sprite;
        }

        return null;
    }

    // ============================================================
    // INFORMACIÓN DEL ATLAS
    // ============================================================

    public bool TryGet(
        string name,
        out Texture2D texture,
        out Rect rect,
        out int width,
        out int height)
    {
        texture = null;
        rect = new Rect();
        width = 0;
        height = 0;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        Entry entry;

        if (
            !entries.TryGetValue(
                name.Trim(),
                out entry))
        {
            return false;
        }

        texture =
            atlasTexture;

        rect =
            new Rect(
                entry.x,
                entry.y,
                entry.width,
                entry.height);

        width =
            entry.width;

        height =
            entry.height;

        return true;
    }

    // ============================================================
    // INDIVIDUAL
    // ============================================================

    public Sprite GetIndividual(
        string name,
        Func<string, Texture2D> loadTexture)
    {
        if (
            string.IsNullOrWhiteSpace(name) ||
            loadTexture == null)
        {
            return null;
        }

        name =
            name.Trim();

        Texture2D textura;

        if (
            !individual.TryGetValue(
                name,
                out textura))
        {
            textura =
                loadTexture(name);

            if (textura == null)
            {
                return null;
            }

            textura.filterMode =
                FilterMode.Point;

            textura.wrapMode =
                TextureWrapMode.Clamp;

            individual[
                name] =
                textura;
        }

        return
            Sprite.Create(
                textura,
                new Rect(
                    0,
                    0,
                    textura.width,
                    textura.height),
                new Vector2(
                    0.5f,
                    0.5f),
                100f);
    }

    // ============================================================
    // INFORMACIÓN INDIVIDUAL
    // ============================================================

    public bool TryGetIndividual(
        string name,
        Func<string, Texture2D> loadTexture,
        out Texture2D texture,
        out Rect rect,
        out int width,
        out int height)
    {
        texture = null;
        rect = new Rect();
        width = 0;
        height = 0;

        if (
            string.IsNullOrWhiteSpace(name) ||
            loadTexture == null)
        {
            return false;
        }

        name =
            name.Trim();

        if (
            !individual.TryGetValue(
                name,
                out texture))
        {
            texture =
                loadTexture(name);

            if (texture == null)
            {
                return false;
            }

            texture.filterMode =
                FilterMode.Point;

            texture.wrapMode =
                TextureWrapMode.Clamp;

            individual[
                name] =
                texture;
        }

        width =
            texture.width;

        height =
            texture.height;

        rect =
            new Rect(
                0,
                0,
                width,
                height);

        return true;
    }

    // ============================================================
    // PACK
    // ============================================================

    private static bool Pack(
        List<Entry> lista,
        int width,
        out int height)
    {
        int x = Padding;
        int y = Padding;

        int rowHeight = 0;

        height = 0;

        foreach (Entry entry in lista)
        {
            if (
                x +
                entry.width +
                Padding >
                width)
            {
                x = Padding;

                y +=
                    rowHeight +
                    Padding;

                rowHeight = 0;
            }

            entry.x =
                x;

            entry.y =
                y;

            x +=
                entry.width +
                Padding;

            rowHeight =
                Mathf.Max(
                    rowHeight,
                    entry.height);

            height =
                Mathf.Max(
                    height,
                    y +
                    entry.height +
                    Padding);
        }

        height =
            NextPowerOfTwo(
                height);

        return
            width <= MaxAtlasSize &&
            height <= MaxAtlasSize;
    }

    // ============================================================
    // POWER OF TWO
    // ============================================================

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

        return Mathf.Min(
            result,
            MaxAtlasSize);
    }

    // ============================================================
    // LIMPIAR
    // ============================================================

    public void Dispose()
    {
        foreach (
            Entry entry
            in entries.Values)
        {
            if (entry.sprite != null)
            {
                Destroy(
                    entry.sprite);
            }
        }

        entries.Clear();

        foreach (
            Texture2D texture
            in individual.Values)
        {
            if (texture != null)
            {
                Destroy(
                    texture);
            }
        }

        individual.Clear();

        if (atlasTexture != null)
        {
            Destroy(
                atlasTexture);
        }

        atlasTexture = null;
    }

    public void OnDestroy()
    {
        Dispose();
    }

    private static void Destroy(
        UnityEngine.Object objeto)
    {
        if (objeto == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(
                objeto);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(
                objeto);
        }
    }
}