using System.Collections.Generic;

namespace PvZReanim
{
    /*
     * Tabla sacada 1:1 de gLawnReanimationArray en el
     * Reanimator.cpp original (ResoddedFramework).
     *
     * OJO con los nombres: no son intuitivos.
     * El Lanzaguisantes usa el archivo "Single" (un guisante).
     * El Repetidor usa "PeaShooter.reanim" (dos guisantes),
     * a pesar de que el nombre del archivo sugiere lo contrario.
     *
     * Usar esta tabla en vez de un path fijo por prefab evita
     * que dos tipos de planta terminen compartiendo por error
     * el mismo .reanim (que fue justo el bug: el Lanzaguisantes
     * estaba cargando "PeaShooter.reanim", el archivo del
     * Repetidor).
     */
    public static class PvZPlantReanimTable
    {
        public static readonly Dictionary<string, string> PathBySeedName =
            new Dictionary<string, string>
            {
                { "PEASHOOTER",   "reanim/PeaShooterSingle.reanim" },
                { "REPEATER",     "reanim/PeaShooter.reanim" },
                { "SNOWPEA",      "reanim/SnowPea.reanim" },
                { "THREEPEATER",  "reanim/ThreePeater.reanim" },
                { "GATLINGPEA",   "reanim/GatlingPea.reanim" },
                { "SPLITPEA",     "reanim/SplitPea.reanim" },
                { "FIREPEA",      "reanim/FirePea.reanim" },
                { "CATTAIL",      "reanim/Cattail.reanim" },
                { "WALLNUT",      "reanim/Wallnut.reanim" },
                { "TALLNUT",      "reanim/Tallnut.reanim" },
                { "SUNFLOWER",    "reanim/SunFlower.reanim" },
                { "TWINSUNFLOWER","reanim/TwinSunflower.reanim" },
                { "SUNSHROOM",    "reanim/SunShroom.reanim" },
                { "CHERRYBOMB",   "reanim/CherryBomb.reanim" },
                { "SQUASH",       "reanim/Squash.reanim" },
                { "DOOMSHROOM",   "reanim/DoomShroom.reanim" },
                { "FUMESHROOM",   "reanim/Fumeshroom.reanim" },
                { "PUFFSHROOM",   "reanim/Puffshroom.reanim" },
                { "HYPNOSHROOM",  "reanim/Hypnoshroom.reanim" },
                { "SCAREDYSHROOM","reanim/ScaredyShroom.reanim" },
                { "ICESHROOM",    "reanim/IceShroom.reanim" },
                { "GLOOMSHROOM",  "reanim/GloomShroom.reanim" },
                { "MAGNETSHROOM", "reanim/Magnetshroom.reanim" },
                { "CHOMPER",      "reanim/Chomper.reanim" },
                { "POTATOMINE",   "reanim/PotatoMine.reanim" },
                { "SPIKEWEED",    "reanim/Caltrop.reanim" },
                { "SPIKEROCK",    "reanim/SpikeRock.reanim" },
                { "MARIGOLD",     "reanim/Marigold.reanim" },
                { "JALAPENO",     "reanim/Jalapeno.reanim" },
                { "CACTUS",       "reanim/Cactus.reanim" },
                { "TANGLEKELP",   "reanim/Tanglekelp.reanim" },
                { "STARFRUIT",    "reanim/Starfruit.reanim" },
                { "CABBAGEPULT",  "reanim/Cabbagepult.reanim" },
                { "KERNELPULT",   "reanim/Cornpult.reanim" },
                { "MELONPULT",    "reanim/Melonpult.reanim" },
                { "WINTERMELON",  "reanim/WinterMelon.reanim" },
                { "UMBRELLALEAF", "reanim/Umbrellaleaf.reanim" },
                { "GARLIC",       "reanim/Garlic.reanim" },
                { "GOLDMAGNET",   "reanim/GoldMagnet.reanim" },
                { "LILYPAD",      "reanim/Lilypad.reanim" },
                { "FLOWERPOT",    "reanim/Pot.reanim" },
                { "COBCANNON",    "reanim/CobCannon.reanim" },
                { "IMITATER",     "reanim/Imitater.reanim" },
                { "COFFEEBEAN",   "reanim/Coffeebean.reanim" },
                { "GRAVEBUSTER",  "reanim/Gravebuster.reanim" },
                { "BLOVER",       "reanim/Blover.reanim" },
                { "SEASHROOM",    "reanim/SeaShroom.reanim" },
                { "PLANTERN",     "reanim/Plantern.reanim" },
                { "TORCHWOOD",    "reanim/Torchwood.reanim" },
                { "POOLCLEANER",  "reanim/PoolCleaner.reanim" },
                { "ROOFCLEANER",  "reanim/RoofCleaner.reanim" },
                { "LAWNMOWER",    "reanim/LawnMower.reanim" },
                { "SODROLL",      "reanim/SodRoll.reanim" },
            };

        public static string GetReanimPath(string seedName)
        {
            if (string.IsNullOrEmpty(seedName))
                return null;

            string key = seedName.Trim().ToUpperInvariant();

            return PathBySeedName.TryGetValue(key, out string path)
                ? path
                : null;
        }
    }
}