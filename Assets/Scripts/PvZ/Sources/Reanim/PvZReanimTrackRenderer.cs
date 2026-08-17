using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Renderiza una pieza individual de una REANIM.
/// 
/// Esta clase recibe un PvZReanimTrack desde PvZReanimRenderer
/// y se encarga de:
/// 
/// - Buscar el frame correspondiente.
/// - Cargar el sprite desde el PAK.
/// - Aplicar posición.
/// - Aplicar escala.
/// - Aplicar rotación.
/// - Activar/desactivar el SpriteRenderer.
/// 
/// Se utiliza reflexión para tolerar pequeñas diferencias entre
/// las estructuras de los frames del parser.
/// </summary>
public class PvZReanimTrackRenderer : MonoBehaviour
{
    // ============================================================
    // DATOS
    // ============================================================

    private PvZReanimRenderer propietario;

    private PvZReanimTrack track;

    private SpriteRenderer spriteRenderer;

    private string ultimaImagen;

    private int indiceTrack;

    private int ultimoFrame = -1;

    private bool inicializado;

    // ============================================================
    // INICIALIZAR
    // ============================================================

    public void Inicializar(
        PvZReanimRenderer propietario,
        PvZReanimTrack track,
        SpriteRenderer spriteRenderer,
        int indiceTrack)
    {
        this.propietario = propietario;
        this.track = track;
        this.spriteRenderer = spriteRenderer;
        this.indiceTrack = indiceTrack;

        inicializado = true;

        if (this.spriteRenderer != null)
        {
            this.spriteRenderer.enabled = false;
            this.spriteRenderer.sortingOrder = indiceTrack;
        }
    }

    // ============================================================
    // APLICAR FRAME
    // ============================================================

    public void AplicarFrame(
        int indiceFrame,
        float escala)
    {
        if (!inicializado)
        {
            return;
        }

        if (track == null ||
            spriteRenderer == null)
        {
            return;
        }

        if (track.frames == null ||
            track.frames.Count == 0)
        {
            spriteRenderer.enabled = false;
            return;
        }

        indiceFrame =
            Mathf.Clamp(
                indiceFrame,
                0,
                track.frames.Count - 1);

        object frame =
            track.frames[indiceFrame];

        if (frame == null)
        {
            spriteRenderer.enabled = false;
            return;
        }

        ultimoFrame = indiceFrame;

        // ========================================================
        // IMAGEN
        // ========================================================

        string nombreImagen =
            ObtenerNombreImagen(frame);

        if (!string.IsNullOrWhiteSpace(nombreImagen))
        {
            if (!string.Equals(
                ultimaImagen,
                nombreImagen,
                StringComparison.OrdinalIgnoreCase))
            {
                Sprite sprite =
                    propietario.ObtenerSprite(
                        nombreImagen);

                spriteRenderer.sprite =
                    sprite;

                ultimaImagen =
                    nombreImagen;
            }
            else if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite =
                    propietario.ObtenerSprite(
                        nombreImagen);
            }
        }
        else
        {
            spriteRenderer.sprite = null;
        }

        // ========================================================
        // VISIBILIDAD
        // ========================================================

        bool visible =
            ObtenerBool(
                frame,
                true,
                "visible",
                "Visible",
                "mVisible",
                "activo",
                "Activo",
                "enabled",
                "Enabled");

        spriteRenderer.enabled =
            visible &&
            spriteRenderer.sprite != null;

        // ========================================================
        // POSICIÓN
        // ========================================================

        float x =
            ObtenerFloat(
                frame,
                0f,
                "x",
                "X",
                "posX",
                "PosX",
                "positionX",
                "PositionX",
                "mX");

        float y =
            ObtenerFloat(
                frame,
                0f,
                "y",
                "Y",
                "posY",
                "PosY",
                "positionY",
                "PositionY",
                "mY");

        // ========================================================
        // ESCALA
        // ========================================================

        float escalaX =
            ObtenerFloat(
                frame,
                1f,
                "scaleX",
                "ScaleX",
                "escalaX",
                "EscalaX",
                "sx",
                "SX",
                "mScaleX");

        float escalaY =
            ObtenerFloat(
                frame,
                1f,
                "scaleY",
                "ScaleY",
                "escalaY",
                "EscalaY",
                "sy",
                "SY",
                "mScaleY");

        // ========================================================
        // ROTACIÓN
        // ========================================================

        float rotacion =
            ObtenerFloat(
                frame,
                0f,
                "rotation",
                "Rotation",
                "rotacion",
                "Rotacion",
                "angle",
                "Angle",
                "mRotation");

        // ========================================================
        // APLICAR TRANSFORMACIÓN
        // ========================================================

        transform.localPosition =
            new Vector3(
                x * escala,
                -y * escala,
                0f);

        transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -rotacion);

        transform.localScale =
            new Vector3(
                escalaX,
                escalaY,
                1f);
    }

    // ============================================================
    // OBTENER NOMBRE DE IMAGEN
    // ============================================================

    private string ObtenerNombreImagen(
        object frame)
    {
        if (frame == null)
        {
            return null;
        }

        // --------------------------------------------------------
        // Strings directos
        // --------------------------------------------------------

        string[] nombres =
        {
            "image",
            "Image",
            "imagen",
            "Imagen",
            "imageName",
            "ImageName",
            "nombreImagen",
            "NombreImagen",
            "sprite",
            "Sprite",
            "spriteName",
            "SpriteName",
            "resource",
            "Resource",
            "resourceName",
            "ResourceName",
            "mImage",
            "mImageName"
        };

        foreach (string nombre in nombres)
        {
            object valor =
                ObtenerValor(
                    frame,
                    nombre);

            if (valor == null)
            {
                continue;
            }

            if (valor is string texto)
            {
                if (!string.IsNullOrWhiteSpace(texto))
                {
                    return texto.Trim();
                }
            }
        }

        // --------------------------------------------------------
        // Si el frame contiene un objeto de imagen
        // --------------------------------------------------------

        string[] objetos =
        {
            "image",
            "Image",
            "imagen",
            "Imagen",
            "sprite",
            "Sprite"
        };

        foreach (string nombre in objetos)
        {
            object objeto =
                ObtenerValor(
                    frame,
                    nombre);

            if (objeto == null ||
                objeto is string)
            {
                continue;
            }

            string resultado =
                BuscarNombreEnObjeto(
                    objeto);

            if (!string.IsNullOrWhiteSpace(
                resultado))
            {
                return resultado;
            }
        }

        return null;
    }

    // ============================================================
    // BUSCAR NOMBRE DENTRO DE OBJETO
    // ============================================================

    private string BuscarNombreEnObjeto(
        object objeto)
    {
        string[] nombres =
        {
            "name",
            "Name",
            "nombre",
            "Nombre",
            "imageName",
            "ImageName",
            "nombreImagen",
            "NombreImagen",
            "resourceName",
            "ResourceName",
            "mName",
            "mImageName"
        };

        foreach (string nombre in nombres)
        {
            object valor =
                ObtenerValor(
                    objeto,
                    nombre);

            if (valor is string texto &&
                !string.IsNullOrWhiteSpace(texto))
            {
                return texto.Trim();
            }
        }

        return null;
    }

    // ============================================================
    // OBTENER VALOR
    // ============================================================

    private object ObtenerValor(
        object objeto,
        string nombre)
    {
        if (objeto == null)
        {
            return null;
        }

        Type tipo =
            objeto.GetType();

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.IgnoreCase;

        FieldInfo campo =
            tipo.GetField(
                nombre,
                flags);

        if (campo != null)
        {
            try
            {
                return campo.GetValue(
                    objeto);
            }
            catch
            {
                return null;
            }
        }

        PropertyInfo propiedad =
            tipo.GetProperty(
                nombre,
                flags);

        if (propiedad != null &&
            propiedad.CanRead)
        {
            try
            {
                return propiedad.GetValue(
                    objeto);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    // ============================================================
    // FLOAT
    // ============================================================

    private float ObtenerFloat(
        object objeto,
        float valorPorDefecto,
        params string[] nombres)
    {
        foreach (string nombre in nombres)
        {
            object valor =
                ObtenerValor(
                    objeto,
                    nombre);

            if (valor == null)
            {
                continue;
            }

            try
            {
                if (valor is float f)
                {
                    return f;
                }

                if (valor is double d)
                {
                    return (float)d;
                }

                if (valor is int i)
                {
                    return i;
                }

                if (valor is long l)
                {
                    return l;
                }

                if (valor is decimal dec)
                {
                    return (float)dec;
                }

                return Convert.ToSingle(
                    valor);
            }
            catch
            {
                // Continuar buscando otro nombre.
            }
        }

        return valorPorDefecto;
    }

    // ============================================================
    // BOOL
    // ============================================================

    private bool ObtenerBool(
        object objeto,
        bool valorPorDefecto,
        params string[] nombres)
    {
        foreach (string nombre in nombres)
        {
            object valor =
                ObtenerValor(
                    objeto,
                    nombre);

            if (valor == null)
            {
                continue;
            }

            try
            {
                if (valor is bool b)
                {
                    return b;
                }

                return Convert.ToBoolean(
                    valor);
            }
            catch
            {
                // Continuar buscando.
            }
        }

        return valorPorDefecto;
    }

    // ============================================================
    // DEBUG
    // ============================================================

    private void Start()
    {
        if (!inicializado)
        {
            Debug.LogWarning(
                "[PvZ Reanim Track] " +
                "TrackRenderer no fue inicializado. " +
                "Track=" +
                indiceTrack);
        }
    }

    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDestroy()
    {
        propietario = null;
        track = null;
        spriteRenderer = null;

        ultimaImagen = null;

        inicializado = false;
        ultimoFrame = -1;
    }
}