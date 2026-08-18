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
        if (datos == null ||
            datos.prefab == null)
        {
            Debug.LogError(
                "[PvZ] ZombieData o prefab inválido."
            );

            return null;
        }

        if (!Board.Instancia ||
            !Board.Instancia.EsFilaValida(fila))
        {
            Debug.LogError(
                "[PvZ] Fila inválida: " + fila
            );

            return null;
        }

        Zombie zombie =
            Instantiate(
                datos.prefab
            );

        zombie.Inicializar(
            fila,
            datos
        );

        return zombie;
    }

    public void RegistrarZombie(
        Zombie zombie)
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
        LimpiarReferencias();

        List<Zombie> resultado =
            new List<Zombie>();

        foreach (Zombie zombie in zombies)
        {
            if (zombie == null ||
                zombie.Muerto)
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
        Zombie resultado = null;

        foreach (
            Zombie zombie
            in ObtenerZombiesEnFila(fila))
        {
            if (resultado == null ||
                zombie.transform.position.x <
                resultado.transform.position.x)
            {
                resultado = zombie;
            }
        }

        return resultado;
    }

    public Zombie ObtenerZombieMasCercano(
        Vector3 posicion,
        int fila)
    {
        Zombie resultado = null;
        float distancia = float.MaxValue;

        foreach (
            Zombie zombie
            in ObtenerZombiesEnFila(fila))
        {
            float d =
                Mathf.Abs(
                    zombie.transform.position.x -
                    posicion.x
                );

            if (d < distancia)
            {
                distancia = d;
                resultado = zombie;
            }
        }

        return resultado;
    }

    public int CantidadVivos()
    {
        LimpiarReferencias();

        return zombies.Count;
    }

    public void LimpiarZombies()
    {
        foreach (Zombie zombie in zombies)
        {
            if (zombie != null)
                Destroy(zombie.gameObject);
        }

        zombies.Clear();
    }

    private void LimpiarReferencias()
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
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}