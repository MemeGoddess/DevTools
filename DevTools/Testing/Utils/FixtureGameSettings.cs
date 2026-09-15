using RimWorld;

namespace DevTools.Testing;

public readonly struct FixtureGameSettings
{
    public readonly WorldGenerationSettings worldGen;
    public readonly Scenario scenario;
    public readonly Storyteller storyteller;
    public readonly MapGenerationSettings mapGen;

    public FixtureGameSettings(object instance)
    {
        worldGen = (instance as IWorldGeneration)?.WorldGenerationSettings;
        scenario = (instance as IScenario)?.Scenario;
        storyteller = (instance as IStoryteller)?.Storyteller;
        mapGen = (instance as IMapGeneration)?.MapGenerationSettings;
    }

    public bool NeedsReload => worldGen != null || scenario != null || storyteller != null || mapGen != null;
}