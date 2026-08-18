using UnityEngine;

[CreateAssetMenu(
    fileName = "PlantData",
    menuName = "PvZ/Plant Data"
)]
public class PlantData : ScriptableObject
{
    [Header("Identificación")]
    public string nombre = "Planta";
    public PlantType tipo = PlantType.None;

    [TextArea(2, 5)]
    public string descripcion;

    [Header("Prefab")]
    public Plant prefab;

    [Header("Economía")]
    public int costo = 100;
    public float recarga = 7.5f;

    [Header("Vida")]
    public int vida = 300;

    [Header("Combate")]
    public int daño = 20;
    public float intervaloAtaque = 1.5f;
    public float rangoAtaque = 10f;

    [Header("Proyectil")]
    public Projectiles prefabProyectil;
    public float velocidadProyectil = 8f;

    [Header("Producción")]
    public int produccionSol = 25;
    public float intervaloProduccion = 24f;

    [Header("Propiedades PvZ")]
    public bool puedeAtacar = true;
    public bool puedeProducirSol;
    public bool nocturna;
    public bool acuatica;
    public bool voladora;

    [Header("Objetivos")]
    public bool atacarSuelo = true;
    public bool atacarAire;
    public bool atacarAgua;

    [Header("Daño especial")]
    public bool dañoEnArea;
    public float radioDaño = 1f;
    public bool ralentiza;
    public float multiplicadorRalentizacion = 0.4f;

    [Header("Animación")]
    public string reanimNombre;
    public string animacionIdle = "idle";
    public string animacionAtaque = "attack";
    public string animacionMuerte = "death";
    public string animacionEspecial = "special";

    public bool EsAtacante()
    {
        return puedeAtacar &&
               daño > 0;
    }

    public bool EsProductora()
    {
        return puedeProducirSol ||
               produccionSol > 0;
    }
}