using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace DevTools.Testing.Utils;

public class TestPawnBuilder
{
    private readonly PawnKindDef _pawnKind;
    private readonly PawnGenerationRequest? _request;
    private List<Func<Pawn, bool>> _pawnValidators = [];
    private List<Action<Pawn>> _pawnModifiers = [];
    private List<Action<Pawn>> _postSpawnModifiers = [];
    private Faction _faction  = Faction.OfPlayer;
    private IntVec3? _location;
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
        _pawnModifiers.Add(pawn => pawn.Name = new NameSingle(name));
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

    public TestPawnBuilder MakeHostile()
    {
        _pawnModifiers.Add(pawn =>
        {
            if (_faction == Faction.OfPlayer)
                _faction = Faction.OfAncientsHostile;
            else if(!_faction.HostileTo(Faction.OfPlayer))
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
        if(attempts >= 1000)
            Test.Fail("Could not generate a valid pawn after 1000 attempts");

        foreach (var modifer in _pawnModifiers) 
            modifer(pawn);

        GenSpawn.Spawn(pawn, location, map);

        foreach (var postSpawnModifier in _postSpawnModifiers) 
            postSpawnModifier(pawn);
        
        return pawn;
    }

    public List<Pawn> SpawnMany(int amount) => 
        Enumerable.Range(1, amount).Select(_ => Spawn()).ToList();

    private Pawn GenPawn()
    {
        if (_pawnKind != null)
            return PawnGenerator.GeneratePawn(_pawnKind, _faction ?? Faction.OfPlayer);
        if (_request != null)
            return PawnGenerator.GeneratePawn(_request.Value);
        
        throw new InvalidOperationException();
    }
    
}