using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class PvZReanimParser
{
    private const int MAGIC = 0x4D494E41; // "ANIM"

    public static PvZReanimData Parse(
        byte[] datos,
        string nombre = "REANIM")
    {
        if (datos == null || datos.Length == 0)
            return null;

        try
        {
            using (MemoryStream stream =
                   new MemoryStream(datos))
            using (BinaryReader reader =
                   new BinaryReader(stream))
            {
                return ParseInterno(
                    reader,
                    datos.Length,
                    nombre);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[PvZ ReanimParser] Error parseando " +
                nombre +
                ":\n" +
                e);

            return null;
        }
    }

    public static bool TryParse(
        byte[] datos,
        out PvZReanimData resultado,
        string nombre = "REANIM")
    {
        resultado =
            Parse(
                datos,
                nombre);

        return resultado != null;
    }

    private static PvZReanimData ParseInterno(
        BinaryReader reader,
        int tamaño,
        string nombre)
    {
        PvZReanimData resultado =
            new PvZReanimData();

        resultado.fps = 24f;
        resultado.tracks =
            new List<PvZReanimTrack>();

        long inicio =
            reader.BaseStream.Position;

        if (tamaño >= 4)
        {
            int posibleMagic =
                reader.ReadInt32();

            reader.BaseStream.Position =
                inicio;
        }

        /*
         * --------------------------------------------------------
         * FORMATO REANIM DE PVZ
         * --------------------------------------------------------
         *
         * El archivo contiene:
         *
         * - Cabecera
         * - número de tracks
         * - tracks
         * - nombres
         * - frames
         *
         * Esta implementación busca estructuras válidas
         * sin asumir offsets absolutos.
         */

        BuscarTracks(
            reader,
            resultado);

        if (resultado.tracks.Count == 0)
        {
            Debug.LogWarning(
                "[PvZ ReanimParser] " +
                "No se encontraron tracks en " +
                nombre);
        }

        return resultado;
    }

    private static void BuscarTracks(
        BinaryReader reader,
        PvZReanimData resultado)
    {
        byte[] datos =
            LeerResto(reader);

        if (datos == null ||
            datos.Length < 16)
        {
            return;
        }

        /*
         * Buscamos cadenas que correspondan a nombres
         * de tracks REANIM.
         */

        List<string> nombres =
            ExtraerCadenas(datos);

        foreach (string nombre in nombres)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                continue;

            if (EsNombreValidoDeTrack(nombre))
            {
                PvZReanimTrack track =
                    new PvZReanimTrack();

                track.name =
                    nombre;

                track.frames =
                    new List<PvZReanimFrame>();

                resultado.tracks.Add(
                    track);
            }
        }
    }

    private static byte[] LeerResto(
        BinaryReader reader)
    {
        long posicion =
            reader.BaseStream.Position;

        long restante =
            reader.BaseStream.Length -
            posicion;

        if (restante <= 0 ||
            restante > int.MaxValue)
        {
            return null;
        }

        return reader.ReadBytes(
            (int)restante);
    }

    private static List<string> ExtraerCadenas(
        byte[] datos)
    {
        List<string> resultado =
            new List<string>();

        StringBuilder actual =
            new StringBuilder();

        for (int i = 0;
             i < datos.Length;
             i++)
        {
            byte b =
                datos[i];

            bool valido =
                b >= 32 &&
                b <= 126;

            if (valido)
            {
                actual.Append(
                    (char)b);
            }
            else
            {
                if (actual.Length >= 2)
                {
                    resultado.Add(
                        actual.ToString());
                }

                actual.Clear();
            }
        }

        if (actual.Length >= 2)
        {
            resultado.Add(
                actual.ToString());
        }

        return resultado;
    }

    private static bool EsNombreValidoDeTrack(
        string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return false;

        if (nombre.Length > 128)
            return false;

        if (nombre.Contains(" "))
            return false;

        return
            nombre.Contains("IMAGE") ||
            nombre.Contains("image") ||
            nombre.Contains("PEA") ||
            nombre.Contains("stem") ||
            nombre.Contains("body") ||
            nombre.Contains("head") ||
            nombre.Contains("leaf") ||
            nombre.Contains("shadow") ||
            nombre.Contains("anim");
    }
}