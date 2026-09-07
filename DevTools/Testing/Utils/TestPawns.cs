using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DevTools.Testing.Utils;

public static class TestPawns
{
  public static TestPawnBuilder Worker =>
    Blank
      .WithName("Worker")
      .WithModifer(x => x.skills.skills.ForEach(y =>
      {
        y.levelInt = 20;
        y.xpSinceLastLevel = 10000;
      }));

  public static TestPawnBuilder Solder =>
    Blank
      .WithName("Solder")
      .WithWorkMaxed(WorkTypeDefOf.Hunting)
      .WithGear(
        ThingDef.Named("Apparel_ArmorHelmetRecon"),
        ThingDef.Named("Apparel_ArmorRecon"),
        ThingDef.Named("Gun_AssaultRifle"));

  public static TestPawnBuilder Blank =>
    PawnKindDefOf
      .Colonist
      .AsBuilder()
      .WithAllWorkAvailable()
      .WithModifer(x => x.story.traits.GainTrait(new Trait(new TraitDef()
      {
        defName =  "Needless",
        generated = true,
        degreeDatas = [new TraitDegreeData()
        {

          disablesNeeds = new List<NeedDef>()
          {
            NeedDefOf.Food,
            NeedDefOf.Rest,
          }
        }],
      })));


  private static TestPawnBuilder WithAllWorkAvailable(this TestPawnBuilder builder) =>
    builder.WithModifer(pawn => pawn.health.Reset())
    .WithValidator(pawn =>
      pawn.story.AllBackstories
        .All(backstory => backstory.DisabledWorkTypes.Count == 0));
}