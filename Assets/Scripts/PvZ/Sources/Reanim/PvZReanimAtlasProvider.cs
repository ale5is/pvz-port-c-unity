using System;
using UnityEngine;

/// <summary>
/// Conecta PvZReanimAtlas con PvZResourceManager.
/// </summary>
public sealed class PvZReanimAtlasProvider : MonoBehaviour
{
    private PvZReanimRenderer rendererOwner;
    private PvZReanimAtlas atlas;
    private bool construido;

    public void Inicializar(PvZReanimRenderer owner)
    {
        rendererOwner = owner;
        ConstruirSiHaceFalta();
    }

    public Sprite ObtenerSprite(string nombre)
    {
        ConstruirSiHaceFalta();

        if (atlas == null || string.IsNullOrWhiteSpace(nombre))
            return null;

        Sprite sprite = atlas.Get(nombre);

        if (sprite != null)
            return sprite;

        return atlas.GetIndividual(nombre, CargarTextura);
    }

    private void ConstruirSiHaceFalta()
    {
        if (construido ||
            rendererOwner == null ||
            rendererOwner.DatosReanim == null)
            return;

        construido = true;

        atlas = new PvZReanimAtlas();

        atlas.Build(
            rendererOwner.DatosReanim,
            CargarTextura);
    }

    private Texture2D CargarTextura(string nombre)
    {
        if (PvZResourceManager.Instancia == null ||
            string.IsNullOrWhiteSpace(nombre))
            return null;

        string limpio =
            nombre.Trim().Replace("\\", "/");

        const string prefijo = "IMAGE_REANIM_";

        if (limpio.StartsWith(
            prefijo,
            StringComparison.OrdinalIgnoreCase))
        {
            limpio =
                limpio.Substring(prefijo.Length);
        }

        string ruta;

        if (limpio.StartsWith(
            "REANIM/",
            StringComparison.OrdinalIgnoreCase))
        {
            ruta =
                limpio.EndsWith(
                    ".PNG",
                    StringComparison.OrdinalIgnoreCase)
                ? limpio
                : limpio + ".PNG";
        }
        else if (limpio.EndsWith(
            ".PNG",
            StringComparison.OrdinalIgnoreCase))
        {
            ruta =
                "REANIM/" + limpio;
        }
        else
        {
            ruta =
                "REANIM/" +
                limpio +
                ".PNG";
        }

        byte[] datos =
            PvZResourceManager.Instancia.Leer(ruta);

        if (datos == null ||
            datos.Length == 0)
            return null;

        Texture2D textura =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false);

        textura.name = nombre;

        textura.filterMode =
            FilterMode.Point;

        textura.wrapMode =
            TextureWrapMode.Clamp;

        if (!textura.LoadImage(
            datos,
            false))
        {
            Destroy(textura);
            return null;
        }

        return textura;
    }

    private void OnDestroy()
    {
        if (atlas != null)
        {
            atlas.Dispose();
            atlas = null;
        }

        rendererOwner = null;
        construido = false;
    }
}