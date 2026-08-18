using UnityEngine;
using UnityEngine.UI;

public class SeedPacket : MonoBehaviour
{
    [Header("Planta")]
    public PlantData datos;

    [Header("UI")]
    public Image imagen;
    public Image recargaImagen;
    public UnityEngine.GameObject seleccionVisual;

    [Header("Estado")]
    public bool seleccionada;
    public bool desbloqueada = true;

    private float temporizadorRecarga;

    public bool Lista =>
        desbloqueada &&
        datos != null &&
        temporizadorRecarga <= 0f;

    private void Awake()
    {
        if (SeedBank.Instancia != null)
        {
            SeedBank.Instancia.RegistrarCarta(this);
        }

        ActualizarVisual();
    }

    private void Update()
    {
        ActualizarRecarga();
        ActualizarVisual();
    }

    public void Inicializar(
        PlantData plantData)
    {
        datos = plantData;

        temporizadorRecarga = 0f;
        seleccionada = false;

        ActualizarVisual();
    }

    public void Seleccionar()
    {
        if (!Lista)
            return;

        if (SeedBank.Instancia == null)
            return;

        if (!SeedBank.Instancia.TieneSoles(
                datos.costo))
        {
            return;
        }

        seleccionada = true;

        if (CursorManager.Instancia != null)
        {
            CursorManager.Instancia.SeleccionarPlanta(
                datos.tipo
            );
        }

        ActualizarVisual();
    }

    public void Deseleccionar()
    {
        seleccionada = false;

        if (CursorManager.Instancia != null)
        {
            CursorManager.Instancia.CancelarSeleccion();
        }

        ActualizarVisual();
    }

    public bool IntentarPlantar(
        int fila,
        int columna)
    {
        if (!Lista)
            return false;

        if (datos == null)
            return false;

        if (Board.Instancia == null)
            return false;

        if (!Board.Instancia.PuedePlantar(
                fila,
                columna))
        {
            return false;
        }

        if (!SeedBank.Instancia.TieneSoles(
                datos.costo))
        {
            return false;
        }

        Plant planta =
            PlantFactory.Instancia != null
                ? PlantFactory.Instancia.CrearPlanta(
                    datos,
                    fila,
                    columna
                )
                : null;

        if (planta == null)
            return false;

        if (!SeedBank.Instancia.GastarSol(
                datos.costo))
        {
            Destroy(planta.gameObject);
            return false;
        }

        IniciarRecarga();

        Deseleccionar();

        return true;
    }

    public void IniciarRecarga()
    {
        if (datos == null)
            return;

        temporizadorRecarga =
            Mathf.Max(
                0f,
                datos.recarga
            );

        seleccionada = false;

        ActualizarVisual();
    }

    private void ActualizarRecarga()
    {
        if (temporizadorRecarga <= 0f)
            return;

        temporizadorRecarga -=
            Time.deltaTime;

        if (temporizadorRecarga < 0f)
            temporizadorRecarga = 0f;
    }

    public float ObtenerPorcentajeRecarga()
    {
        if (datos == null ||
            datos.recarga <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(
            temporizadorRecarga /
            datos.recarga
        );
    }

    private void ActualizarVisual()
    {
        if (recargaImagen != null)
        {
            recargaImagen.fillAmount =
                ObtenerPorcentajeRecarga();
        }

        if (seleccionVisual != null)
        {
            seleccionVisual.SetActive(
                seleccionada
            );
        }

        if (imagen != null)
        {
            bool disponible =
                Lista &&
                SeedBank.Instancia != null &&
                SeedBank.Instancia.TieneSoles(
                    datos != null
                        ? datos.costo
                        : 0
                );

            imagen.color =
                disponible
                    ? Color.white
                    : new Color(
                        0.5f,
                        0.5f,
                        0.5f,
                        1f
                    );
        }
    }

    public int ObtenerCosto()
    {
        return datos != null
            ? datos.costo
            : 0;
    }

    public float ObtenerRecarga()
    {
        return datos != null
            ? datos.recarga
            : 0f;
    }

    private void OnDestroy()
    {
        if (SeedBank.Instancia != null)
        {
            SeedBank.Instancia.QuitarCarta(this);
        }
    }
}