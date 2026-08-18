using System.Collections.Generic;
using UnityEngine;

public class SeedBank : MonoBehaviour
{
    public static SeedBank Instancia { get; private set; }

    [Header("Soles")]
    [SerializeField]
    private int soles = 50;

    [Header("Cartas")]
    [SerializeField]
    private List<SeedPacket> cartas =
        new List<SeedPacket>();

    public int Soles => soles;

    public IReadOnlyList<SeedPacket> Cartas =>
        cartas;

    private void Awake()
    {
        if (Instancia != null &&
            Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    public bool TieneSoles(int cantidad)
    {
        return cantidad >= 0 &&
               soles >= cantidad;
    }

    public bool GastarSol(int cantidad)
    {
        if (cantidad <= 0)
            return true;

        if (soles < cantidad)
            return false;

        soles -= cantidad;

        ActualizarUI();

        return true;
    }

    public void AgregarSol(int cantidad)
    {
        if (cantidad <= 0)
            return;

        soles += cantidad;

        ActualizarUI();
    }

    public void EstablecerSoles(int cantidad)
    {
        soles = Mathf.Max(0, cantidad);

        ActualizarUI();
    }

    public bool PuedePlantar(
        PlantData datos)
    {
        if (datos == null)
            return false;

        return TieneSoles(datos.costo);
    }

    public bool ComprarPlanta(
        PlantData datos)
    {
        if (datos == null)
            return false;

        return GastarSol(datos.costo);
    }

    public void RegistrarCarta(
        SeedPacket carta)
    {
        if (carta == null)
            return;

        if (!cartas.Contains(carta))
            cartas.Add(carta);
    }

    public void QuitarCarta(
        SeedPacket carta)
    {
        if (carta == null)
            return;

        cartas.Remove(carta);
    }

    public void ActualizarUI()
    {
        // Se conectará con la UI del Seed Bank.
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}