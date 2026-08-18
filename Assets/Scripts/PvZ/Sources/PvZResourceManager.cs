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

    public int CantidadArchivos
    {
        get
        {
            if (!EstaListo || pakReader == null)
                return 0;

            return pakReader.CantidadArchivos;
        }
    }

    private void Awake()
    {
        if (Instancia != null &&
            Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;

        pakReader =
            new PvZPakReader();

        EstaListo = false;
    }

    private void Start()
    {
        Inicializar();
    }

    public void Inicializar()
    {
        EstaListo = false;

        if (pakReader == null)
            pakReader = new PvZPakReader();

        if (string.IsNullOrWhiteSpace(carpetaPvZ))
        {
            Debug.LogError(
                "[PvZ] No se configuró la carpeta de PvZ."
            );

            return;
        }

        string rutaPak =
            Path.Combine(
                carpetaPvZ,
                nombrePak
            );

        Debug.Log(
            "[PvZ] Buscando PAK: " +
            rutaPak
        );

        if (!File.Exists(rutaPak))
        {
            Debug.LogError(
                "[PvZ] No existe el PAK:\n" +
                rutaPak
            );

            return;
        }

        try
        {
            pakReader.Cargar(
                rutaPak
            );

            EstaListo = true;

            Debug.Log(
                "[PvZ] PAK cargado correctamente. " +
                "Archivos: " +
                pakReader.CantidadArchivos
            );
        }
        catch (Exception e)
        {
            EstaListo = false;

            Debug.LogError(
                "[PvZ] No se pudo cargar main.pak:\n" +
                e
            );
        }
    }

    public bool Existe(
        string nombre)
    {
        if (!EstaListo ||
            pakReader == null ||
            string.IsNullOrWhiteSpace(nombre))
        {
            return false;
        }

        return pakReader.Contiene(
            nombre
        );
    }

    public byte[] Leer(
        string nombre)
    {
        if (!EstaListo ||
            pakReader == null)
        {
            Debug.LogError(
                "[PvZ] ResourceManager todavía no está listo."
            );

            return null;
        }

        if (string.IsNullOrWhiteSpace(nombre))
            return null;

        byte[] datos =
            pakReader.LeerArchivo(
                nombre
            );

        if (datos == null)
        {
            Debug.LogWarning(
                "[PvZ] Recurso no encontrado: " +
                nombre
            );
        }

        return datos;
    }

    public Stream Abrir(
        string nombre)
    {
        if (!EstaListo ||
            pakReader == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(nombre))
            return null;

        return pakReader.AbrirStream(
            nombre
        );
    }

    public bool ObtenerArchivo(
        string nombre,
        out PvZPakFile archivo)
    {
        archivo = null;

        if (!EstaListo ||
            pakReader == null ||
            string.IsNullOrWhiteSpace(nombre))
        {
            return false;
        }

        return pakReader.TryGetArchivo(
            nombre,
            out archivo
        );
    }

    public IEnumerable<PvZPakFile>
        ObtenerTodosLosArchivos()
    {
        if (!EstaListo ||
            pakReader == null)
        {
            yield break;
        }

        foreach (
            PvZPakFile archivo
            in pakReader.ObtenerArchivos())
        {
            if (archivo != null)
                yield return archivo;
        }
    }

    public List<PvZPakFile>
        BuscarArchivos(
            string texto)
    {
        List<PvZPakFile> resultado =
            new List<PvZPakFile>();

        if (!EstaListo ||
            pakReader == null ||
            string.IsNullOrWhiteSpace(texto))
        {
            return resultado;
        }

        foreach (
            PvZPakFile archivo
            in pakReader.ObtenerArchivos())
        {
            if (archivo == null)
                continue;

            if (archivo.Name.IndexOf(
                    texto,
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                resultado.Add(
                    archivo
                );
            }
        }

        return resultado;
    }

    public List<string>
        ObtenerNombresArchivos()
    {
        List<string> nombres =
            new List<string>();

        if (!EstaListo ||
            pakReader == null)
        {
            return nombres;
        }

        foreach (
            PvZPakFile archivo
            in pakReader.ObtenerArchivos())
        {
            if (archivo == null)
                continue;

            nombres.Add(
                archivo.Name
            );
        }

        return nombres;
    }

    private void OnDestroy()
    {
        EstaListo = false;

        if (pakReader != null)
        {
            pakReader.Dispose();
            pakReader = null;
        }

        if (Instancia == this)
            Instancia = null;
    }
}