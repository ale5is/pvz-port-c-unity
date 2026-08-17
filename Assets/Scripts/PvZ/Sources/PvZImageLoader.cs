using System;
using UnityEngine;

public static class PvZImageLoader
{
    public static Texture2D CargarTexture(string nombreArchivo)
    {
        if (PvZResourceManager.Instancia == null)
        {
            Debug.LogError("[PvZ ImageLib] No existe PvZResourceManager.");
            return null;
        }

        byte[] datos = PvZResourceManager.Instancia.Leer(nombreArchivo);

        if (datos == null || datos.Length == 0)
        {
            Debug.LogError(
                $"[PvZ ImageLib] No se pudo leer: {nombreArchivo}");

            return null;
        }

        Texture2D textura = new Texture2D(
            2,
            2,
            TextureFormat.RGBA32,
            false);

        textura.name = nombreArchivo;

        bool cargada = textura.LoadImage(datos, false);

        if (!cargada)
        {
            UnityEngine.Object.Destroy(textura);

            Debug.LogError(
                $"[PvZ ImageLib] Unity no pudo decodificar: {nombreArchivo}");

            return null;
        }

        textura.wrapMode = TextureWrapMode.Clamp;
        textura.filterMode = FilterMode.Point;

        Debug.Log(
            $"[PvZ ImageLib] Imagen cargada: {nombreArchivo} " +
            $"({textura.width}x{textura.height})");

        return textura;
    }

    public static Sprite CargarSprite(
        string nombreArchivo,
        float pixelsPerUnit = 100f)
    {
        Texture2D textura = CargarTexture(nombreArchivo);

        if (textura == null)
            return null;

        return Sprite.Create(
            textura,
            new Rect(
                0,
                0,
                textura.width,
                textura.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
    }
}