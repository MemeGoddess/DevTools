using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace DevTools.Testing.Utils;

public enum TestPawnName : int
{
  WorkType = 0
}

public class TestPawnBuilder
{
    private PawnKindDef _pawnKind;
    private PawnGenerationRequest? _request;
    private List<Func<Pawn, bool>> _pawnValidators = [];
    private List<Action<Pawn>> _pawnModifiers = [];
    private List<Action<Pawn>> _postSpawnModifiers = [];
    private List<Action<Pawn>> _preCleanup = [];
    private Faction _faction = Faction.OfPlayer;
    private IntVec3? _location;
    private List<Pawn> _generatedPawns = [];

    [ItemCanBeNull] private string[] _nameGen = new string[typeof(TestPawnName).GetEnumValues().Length];

    [CanBeNull] private Map _map;


    public TestPawnBuilder(PawnKindDef pawnKind)
    {
        _pawnKind = pawnKind;
    }

    public TestPawnBuilder(PawnGenerationRequest request)
    {
        _request = request;
    }

    public TestPawnBuilder WithName(string name)
    {
        _pawnModifiers.Add(pawn =>
        {
            var triple = pawn.Name as NameTriple;
            var first = triple?.First ?? string.Empty;
            var last = triple?.Last ?? string.Empty;
            var newName = name;
            if (_generatedPawns.Count > 0)
                newName += "-" + _generatedPawns.Count;
            pawn.Name = new NameTriple(first, newName, last);

        });
        return this;
    }

    public TestPawnBuilder WithFaction(Faction faction)
    {
        this._faction = faction;
        return this;
    }

    public TestPawnBuilder WithWorkMaxed(WorkTypeDef workType)
    {
        _pawnValidators.Add(pawn => !pawn.WorkTypeIsDisabled(workType));
        _postSpawnModifiers.Add(pawn =>
        {
            foreach (var skill in workType.relevantSkills)
                pawn.skills.Learn(skill, 100000000);

            pawn.workSettings.EnableAndInitialize();
            pawn.workSettings.SetPriority(workType, 1);
        });
        _nameGen[(int)TestPawnName.WorkType] ??= workType.LabelCap;

        return this;
    }

    public TestPawnBuilder WithKind(PawnKindDef kind)
    {
        if (_request != null)
            _request = _request.Value with { KindDef = kind };
        else
            _pawnKind = kind;

        return this;
    }

    public TestPawnBuilder WithGear(params ThingDef[] gear)
    {
        _pawnModifiers.Add(pawn =>
        {
            if (gear.Any(x => x.equipmentType == EquipmentType.Primary))
                pawn.equipment.DestroyAllEquipment();
            if (gear.Any(x => x.IsApparel))
                pawn.apparel.DestroyAll();

            foreach (var def in gear)
            {
                var thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
                thing.TryGetComp<CompQuality>()
                    ?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
                if (def.equipmentType == EquipmentType.Primary)
                {
                    if (pawn.equipment.Primary != null)
                        pawn.equipment.DestroyAllEquipment();
                    pawn.equipment.AddEquipment(thing as ThingWithComps);
                }
                else if (def.IsApparel)
                    pawn.apparel.Wear(thing as Apparel);
                else
                    throw new NotImplementedException(
                        $"Equipping {def.defName} is not implemented");
            }
        });

        return this;
    }

    public TestPawnBuilder WithLocation(IntVec3 location)
    {
        _location = location;
        return this;
    }

    public TestPawnBuilder WithMap(Map map)
    {
        _map = map;
        return this;
    }

    public TestPawnBuilder WithValidator(Func<Pawn, bool> validator)
    {
        _pawnValidators.Add(validator);
        return this;
    }

    public TestPawnBuilder WithModifer(Action<Pawn> modifer)
    {
        _pawnModifiers.Add(modifer);
        return this;
    }

    public TestPawnBuilder WithPostSpawn(Action<Pawn> postSpawn)
    {
        _postSpawnModifiers.Add(postSpawn);
        return this;
    }

    public TestPawnBuilder WithCleanup(Action<Pawn> cleanup)
    {
        _preCleanup.Add(cleanup);
        return this;
    }

    public TestPawnBuilder MakeHostile()
    {
        _pawnModifiers.Add(pawn =>
        {
            if (_faction == Faction.OfPlayer)
                _faction = Faction.OfAncientsHostile;
            else if (!_faction.HostileTo(Faction.OfPlayer))
                _faction.ChangeGoodwill_Debug(Faction.OfPlayer, -100);
        });

        return this;
    }

    public Pawn Spawn()
    {
        var map = _map ?? Find.CurrentMap;
        var location = _location ?? map.Center;

        var pawn = GenPawn();
        pawn.health.Reset();

        var attempts = 1;
        while (attempts < 1000 && _pawnValidators.Any(x => !x(pawn)))
        {
            attempts++;
            pawn = GenPawn();
            pawn.health.Reset();
        }

        if (attempts >= 1000)
            Test.Fail("Could not generate a valid pawn after 1000 attempts");

        if (_nameGen.Any(x => x != null))
        {
            var name = string.Join(" ", _nameGen.Where(x => x != null));
            if (_generatedPawns.Count > 0)
                name += "-" + _generatedPawns.Count;
            var triple = pawn.Name as NameTriple;
            pawn.Name = new NameTriple(triple?.First ?? string.Empty, name,
                triple?.Last ?? string.Empty);
        }

        foreach (var modifer in _pawnModifiers)
            modifer(pawn);

        GenSpawn.Spawn(pawn, location, map);

        foreach (var postSpawnModifier in _postSpawnModifiers)
            postSpawnModifier(pawn);

        _generatedPawns.Add(pawn);
        return pawn;
    }

    public List<Pawn> SpawnMany(int amount) =>
        Enumerable.Range(1, amount).Select(_ => Spawn()).ToList();

    public void Destroy()
    {
        foreach (Pawn pawn in _generatedPawns)
        {
            foreach (Action<Pawn> action in _preCleanup)
                action(pawn);
            pawn.Destroy();
        }
    }

    private Pawn GenPawn()
    {
        if (_pawnKind != null)
            return PawnGenerator.GeneratePawn(_pawnKind, _faction ?? Faction.OfPlayer);
        if (_request != null)
            return PawnGenerator.GeneratePawn(_request.Value);

        throw new InvalidOperationException();
    }

}