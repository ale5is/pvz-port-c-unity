using System.Collections.Generic;
using UnityEngine;

public class ZombieManager : MonoBehaviour
{
    public static ZombieManager Instancia { get; private set; }

    private readonly List<Zombie> zombies = new();

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
                "[PvZ] ZombieData '" +
                datos.nombre +
                "' no tiene prefab."
            );

            return null;
        }

        if (fila < 0 ||
            fila >= Board.FILAS)
        {
            Debug.LogError(
                "[PvZ] Fila de zombie inválida: " +
                fila
            );

            return null;
        }

        Zombie zombie =
            Instantiate(datos.prefab);

        zombie.Inicializar(
            fila,
            datos
        );

        RegistrarZombie(zombie);

        return zombie;
    }

    public void RegistrarZombie(Zombie zombie)
    {
        if (zombie == null)
            return;

        if (!zombies.Contains(zombie))
            zombies.Add(zombie);
    }

    public void NotificarMuerte(
        Zombie zombie)
    {
        if (zombie == null)
            return;

        zombies.Remove(zombie);
    }

    public List<Zombie> ObtenerZombiesEnFila(
        int fila)
    {
        List<Zombie> resultado = new();

        for (int i = zombies.Count - 1;
             i >= 0;
             i--)
        {
            Zombie zombie = zombies[i];

            if (zombie == null)
            {
                zombies.RemoveAt(i);
                continue;
            }

            if (zombie.Muerto)
                continue;

            if (zombie.fila == fila)
                resultado.Add(zombie);
        }

        resultado.Sort(
            (a, b) =>
                a.transform.position.x.CompareTo(
                    b.transform.position.x
                )
        );

        return resultado;
    }

    public Zombie ObtenerPrimerZombieEnFila(
        int fila)
    {
        Zombie objetivo = null;

        foreach (
            Zombie zombie
            in ObtenerZombiesEnFila(fila))
        {
            if (objetivo == null ||
                zombie.transform.position.x <
                objetivo.transform.position.x)
            {
                objetivo = zombie;
            }
        }

        return objetivo;
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

    public void LimpiarZombies()
    {
        for (int i = zombies.Count - 1;
             i >= 0;
             i--)
        {
            if (zombies[i] != null)
                Destroy(
                    zombies[i].gameObject
                );
        }

        zombies.Clear();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}