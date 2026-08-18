using UnityEngine;

public enum ZombieType
{
    Basic,
    Conehead,
    Buckethead
}

[CreateAssetMenu(fileName = "ZombieData", menuName = "PvZ/Zombie")]
public class ZombieData : ScriptableObject
{
    [Header("General")]
    public ZombieType tipo = ZombieType.Basic;
    public string nombre = "Zombie";

    [Header("Vida")]
    public int vida = 270;
    public int vidaArmadura = 0;

    [Header("Movimiento")]
    public float velocidad = 0.22f;

    [Header("Ataque")]
    public int daño = 20;
    public float intervaloAtaque = 1f;
    public float rangoAtaque = 0.65f;

    [Header("Prefab")]
    public Zombie prefab;
}