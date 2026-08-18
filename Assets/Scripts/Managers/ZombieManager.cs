using System.Collections.Generic;
using UnityEngine;

public class ZombieManager : MonoBehaviour
{
    public static ZombieManager Instancia { get; private set; }

    private readonly List<Zombie> zombies =
        new List<Zombie>();

    public IReadOnlyList<Zombie> ZombiesActivos =>
        zombies;

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

    public Zombie CrearZombie(
        ZombieData datos,
        int fila)
    {
        if (datos == null)
        {
            Debug.LogError(
                "[PvZ] ZombieData es null."
            );

            return null;
        }

        if (datos.prefab == null)
        {
            Debug.LogError(
                "[PvZ] ZombieData no tiene prefab."
            );

            return null;
        }

        Zombie zombie =
            Instantiate(datos.prefab);

        zombie.Inicializar(
            fila,
            datos
        );

        zombies.Add(zombie);

        return zombie;
    }

    public void NotificarMuerte(
        Zombie zombie)
    {
        zombies.Remove(zombie);
    }

    public int CantidadVivos()
    {
        for (int i = zombies.Count - 1;
             i >= 0;
             i--)
        {
            if (zombies[i] == null ||
                zombies[i].Muerto)
            {
                zombies.RemoveAt(i);
            }
        }

        return zombies.Count;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}