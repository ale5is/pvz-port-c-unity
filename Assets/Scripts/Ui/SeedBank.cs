using UnityEngine;

public class SeedBank : MonoBehaviour
{
    public static SeedBank Instancia { get; private set; }

    [Header("Sol")]
    [SerializeField]
    private int soles = 50;

    public int Soles => soles;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    public bool PuedeComprar(PlantData planta)
    {
        if (planta == null)
            return false;

        return soles >= planta.costo;
    }

    public bool Comprar(PlantData planta)
    {
        if (!PuedeComprar(planta))
            return false;

        soles -= planta.costo;
        return true;
    }

    public void AgregarSoles(int cantidad)
    {
        if (cantidad <= 0)
            return;

        soles += cantidad;
    }

    public bool QuitarSoles(int cantidad)
    {
        if (cantidad <= 0 || soles < cantidad)
            return false;

        soles -= cantidad;
        return true;
    }

    public void EstablecerSoles(int cantidad)
    {
        soles = Mathf.Max(0, cantidad);
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}