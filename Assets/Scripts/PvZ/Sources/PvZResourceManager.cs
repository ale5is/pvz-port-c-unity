using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PvZResourceManager : MonoBehaviour
{
    public static PvZResourceManager Instancia { get; private set; }

    [Header("Ruta de PvZ")]
    [Tooltip("Carpeta donde se encuentra main.pak")]
    public string carpetaPvZ;

    [Tooltip("Nombre del archivo PAK principal")]
    public string nombrePak = "main.pak";

    private PvZPakReader pakReader;

    public bool EstaListo { get; private set; }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;

        pakReader = new PvZPakReader();
    }

    private void Start()
    {
        Inicializar();
    }

    public void Inicializar()
    {
        EstaListo = false;

        if (string.IsNullOrWhiteSpace(carpetaPvZ))
        {
            Debug.LogError(
                "[PvZ] No se configuró la carpeta de PvZ.");

            return;
        }

        string rutaPak = Path.Combine(
            carpetaPvZ,
            nombrePak);

        Debug.Log(
            $"[PvZ] Buscando PAK: {rutaPak}");

        try
        {
            pakReader.Cargar(rutaPak);

            EstaListo = true;

            Debug.Log(
                $"[PvZ] PAK cargado correctamente. " +
                $"Archivos: {pakReader.CantidadArchivos}");
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[PvZ] No se pudo cargar main.pak:\n{e}");
        }
    }

    public bool Existe(string nombre)
    {
        if (!EstaListo)
            return false;

        return pakReader.Contiene(nombre);
    }

    public byte[] Leer(string nombre)
    {
        if (!EstaListo)
        {
            Debug.LogError(
                "[PvZ] ResourceManager todavía no está listo.");

            return null;
        }

        byte[] datos = pakReader.LeerArchivo(nombre);

        if (datos == null)
        {
            Debug.LogWarning(
                $"[PvZ] Recurso no encontrado: {nombre}");
        }

        return datos;
    }

    public Stream Abrir(string nombre)
    {
        if (!EstaListo)
            return null;

        return pakReader.AbrirStream(nombre);
    }

    public bool ObtenerArchivo(
        string nombre,
        out PvZPakFile archivo)
    {
        archivo = null;

        if (!EstaListo)
            return false;

        return pakReader.TryGetArchivo(
            nombre,
            out archivo);
    }

    public IEnumerable<PvZPakFile> ObtenerTodosLosArchivos()
    {
        if (!EstaListo)
            yield break;

        foreach (PvZPakFile archivo in pakReader.ObtenerArchivos())
            yield return archivo;
    }

    private void OnDestroy()
    {
        if (pakReader != null)
        {
            pakReader.Dispose();
            pakReader = null;
        }

        if (Instancia == this)
            Instancia = null;
    }
}