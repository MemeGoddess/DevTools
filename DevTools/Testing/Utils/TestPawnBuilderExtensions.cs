using Verse;

namespace DevTools.Testing.Utils;

public static class TestPawnBuilderExtensions
{
    public static TestPawnBuilder AsBuilder(this PawnKindDef pawnKind) => 
        new(pawnKind);
    
    public static TestPawnBuilder AsBuilder(this PawnGenerationRequest request) =>
        new(request);
}