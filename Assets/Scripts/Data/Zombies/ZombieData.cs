using UnityEngine;

public enum ZombieType
{
    Basic,
    Conehead,
    Buckethead,
    Flag,
    PoleVaulting,
    Newspaper,
    ScreenDoor,
    Football,
    Digger,
    Pogo,
    DolphinRider,
    Bungee,
    Bobsled,
    Dancing,
    BackupDancer,
    JackInTheBox,
    Balloon,
    Catapult,
    Gargantuar,
    Imp
}

[CreateAssetMenu(
    fileName = "ZombieData",
    menuName = "PvZ/Zombie"
)]
public class ZombieData : ScriptableObject
{
    [Header("General")]
    public ZombieType tipo = ZombieType.Basic;
    public string nombre = "Zombie";

    [TextArea(2, 5)]
    public string descripcion;

    [Header("Vida")]
    public int vida = 270;
    public int vidaArmadura = 0;
    public int vidaEscudo = 0;

    [Header("Movimiento")]
    public float velocidad = 0.22f;
    public float velocidadAturdido = 0f;

    [Header("Ataque")]
    public int daño = 20;
    public float intervaloAtaque = 1f;
    public float rangoAtaque = 0.65f;

    [Header("Prefab")]
    public Zombie prefab;

    [Header("Propiedades PvZ")]
    public bool puedeEntrarAgua;
    public bool puedeEntrarHighGround;
    public bool puedeSerCongelado = true;
    public bool puedeSerRalentizado = true;
    public bool puedeSerEmpujado = true;

    [Header("Habilidades")]
    public bool puedeSaltar;
    public bool puedeCavar;
    public bool puedeVolar;
    public bool puedeUsarEscudo;
    public bool puedeUsarArmadura;

    [Header("Animación")]
    public string reanimNombre;
    public string animacionIdle = "idle";
    public string animacionCaminar = "walk";
    public string animacionComer = "eat";
    public string animacionAtaque = "attack";
    public string animacionMuerte = "death";
    public string animacionEspecial = "special";

    public int VidaTotal()
    {
        return Mathf.Max(0, vida) +
               Mathf.Max(0, vidaArmadura) +
               Mathf.Max(0, vidaEscudo);
    }

    public bool TieneArmadura()
    {
        return vidaArmadura > 0;
    }

    public bool TieneEscudo()
    {
        return vidaEscudo > 0;
    }

    public bool EsEspecial()
    {
        return tipo != ZombieType.Basic;
    }
}