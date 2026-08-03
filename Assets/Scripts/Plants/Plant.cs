using UnityEngine;

public class Plant : GameObject
{
    public PlantData datos;

    protected int vida;

    protected override void Start()
    {
        base.Start();

        if (datos != null)
            vida = datos.vida;
    }

    public virtual void RecibirDaño(int daño)
    {
        vida -= daño;

        if (vida <= 0)
            Destroy(gameObject);
    }
}