using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public sealed class PvZPakReader : IDisposable
{
    private const uint PAK_MAGIC = 0xBAC04AC0;
    private const uint PAK_VERSION = 0;
    private const byte FILEFLAGS_END = 0x80;

    private readonly Dictionary<string, PvZPakFile> archivos =
        new Dictionary<string, PvZPakFile>(StringComparer.OrdinalIgnoreCase);

    private byte[] datosPak;

    public bool EstaCargado => datosPak != null;

    public int CantidadArchivos => archivos.Count;

    public bool Cargar(string ruta)
    {
        Limpiar();

        if (!File.Exists(ruta))
            throw new FileNotFoundException(
                "No se encontró el archivo main.pak.",
                ruta);

        datosPak = File.ReadAllBytes(ruta);

        if (datosPak.Length < 8)
            throw new InvalidDataException(
                "El archivo PAK es demasiado pequeño.");

        uint magic = LeerUInt32(0);

        if (magic != PAK_MAGIC)
        {
            DesencriptarPak();

            magic = LeerUInt32(0);

            if (magic != PAK_MAGIC)
            {
                Limpiar();

                throw new InvalidDataException(
                    "El archivo no tiene un encabezado PAK válido.");
            }
        }

        uint version = LeerUInt32(4);

        if (version != PAK_VERSION)
        {
            Limpiar();

            throw new InvalidDataException(
                $"Versión PAK no soportada: {version}");
        }

        LeerIndice();

        return true;
    }

    private void LeerIndice()
    {
        int posicion = 8;
        int posicionDatos = 0;

        while (posicion < datosPak.Length)
        {
            byte flags = datosPak[posicion++];

            if ((flags & FILEFLAGS_END) != 0)
                break;

            if (posicion >= datosPak.Length)
                throw new InvalidDataException(
                    "PAK corrupto: falta el tamaño del nombre.");

            int nombreLength = datosPak[posicion++];

            if (nombreLength <= 0 || nombreLength > 255)
                throw new InvalidDataException(
                    $"Nombre de archivo PAK inválido: {nombreLength}");

            if (posicion + nombreLength > datosPak.Length)
                throw new InvalidDataException(
                    "PAK corrupto: nombre fuera de rango.");

            string nombre = Encoding.ASCII.GetString(
                datosPak,
                posicion,
                nombreLength);

            posicion += nombreLength;

            nombre = NormalizarNombre(nombre);

            if (posicion + 4 + 8 > datosPak.Length)
                throw new InvalidDataException(
                    "PAK corrupto: falta información del archivo.");

            int tamaño = LeerInt32(posicion);
            posicion += 4;

            long fecha = LeerInt64(posicion);
            posicion += 8;

            if (tamaño < 0)
                throw new InvalidDataException(
                    $"Tamaño inválido para {nombre}");

            var archivo = new PvZPakFile(
                nombre,
                posicionDatos,
                tamaño,
                fecha);

            archivos[nombre] = archivo;

            posicionDatos += tamaño;
        }

        // En el formato de PvZ los datos de los archivos comienzan
        // después de toda la tabla de índice.
        long inicioDatos = posicion;

        var claves = new List<string>(archivos.Keys);

        foreach (string clave in claves)
        {
            PvZPakFile viejo = archivos[clave];

            archivos[clave] = new PvZPakFile(
                viejo.Name,
                inicioDatos + viejo.Offset,
                viejo.Size,
                viejo.Timestamp);
        }

        foreach (PvZPakFile archivo in archivos.Values)
        {
            if (archivo.Offset < 0 ||
                archivo.Offset + archivo.Size > datosPak.Length)
            {
                throw new InvalidDataException(
                    $"El archivo '{archivo.Name}' apunta fuera del PAK.");
            }
        }
    }

    private void DesencriptarPak()
    {
        // Resodded utiliza "\xF7" como contraseña.
        // Como es un único byte, equivale a XOR 0xF7.
        for (int i = 0; i < datosPak.Length; i++)
            datosPak[i] ^= 0xF7;
    }

    public bool Contiene(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return false;

        return archivos.ContainsKey(NormalizarNombre(nombre));
    }

    public byte[] LeerArchivo(string nombre)
    {
        if (!TryGetArchivo(nombre, out PvZPakFile archivo))
            return null;

        byte[] resultado = new byte[archivo.Size];

        Buffer.BlockCopy(
            datosPak,
            checked((int)archivo.Offset),
            resultado,
            0,
            archivo.Size);

        return resultado;
    }

    public Stream AbrirStream(string nombre)
    {
        byte[] datos = LeerArchivo(nombre);

        if (datos == null)
            return null;

        return new MemoryStream(
            datos,
            writable: false);
    }

    public bool TryGetArchivo(
        string nombre,
        out PvZPakFile archivo)
    {
        archivo = null;

        if (string.IsNullOrWhiteSpace(nombre))
            return false;

        return archivos.TryGetValue(
            NormalizarNombre(nombre),
            out archivo);
    }

    public IEnumerable<PvZPakFile> ObtenerArchivos()
    {
        return archivos.Values;
    }

    public void Limpiar()
    {
        archivos.Clear();
        datosPak = null;
    }

    private static string NormalizarNombre(string nombre)
    {
        nombre = nombre.Replace('\\', '/');

        while (nombre.Contains("//"))
            nombre = nombre.Replace("//", "/");

        return nombre.ToUpperInvariant();
    }

    private uint LeerUInt32(int posicion)
    {
        return BitConverter.ToUInt32(
            datosPak,
            posicion);
    }

    private int LeerInt32(int posicion)
    {
        return BitConverter.ToInt32(
            datosPak,
            posicion);
    }

    private long LeerInt64(int posicion)
    {
        return BitConverter.ToInt64(
            datosPak,
            posicion);
    }

    public void Dispose()
    {
        Limpiar();
    }
}