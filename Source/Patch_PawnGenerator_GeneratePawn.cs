using HarmonyLib;
using System;
using Verse;

namespace AtavismLibraries
{
    //Applies the atavism decided in Patch_PregnancyUtility_ApplyBirthOutcome.
    //No rolling and no def lookups happen here - this method runs for every pawn
    //the game ever generates, so it does nothing at all unless a birth is open.
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnGenerationRequest) })]
    public static class Patch_PawnGenerator_GeneratePawn
    {
        [HarmonyPrefix]
        public static void Prefix(ref PawnGenerationRequest request, out bool __state)
        {
            __state = false;
            if (!AtavismContext.TryBeginApply(out GeneticAtavismDef def, out Pawn mother, out Pawn father))
                return;

            // Marks this call as the one generating the baby, so the postfix records
            // this pawn and not one generated in a nested call (a relative, say).
            __state = true;

            // ForcedEndogenes is the gene list ApplyBirthOutcome settled on for
            // this birth either the one it was handed or the one it worked out
            // itself so it is what the baby would otherwise have been born with.
            // With Better Gene Inheritance installed it is already that mod's mix.
            AtavismUtils.AtavismGeneInheritanceResult inherited =
                AtavismUtils.BuildInheritedGenes(request.ForcedEndogenes ?? AtavismContext.ParentGenes, mother, father, def);

            // Assign fresh lists rather than clearing in place. The incoming
            // ForcedEndogenes belongs to the caller, and emptying it would mutate
            // state we do not own.
            request.ForcedEndogenes = inherited.endogenes;
            request.ForcedXenogenes = inherited.xenogenes;
            request.ForcedXenotype = def.targetXenotype;
        }

        // Hands the generated baby to the context, for births whose caller never
        // sees it (VEF's egg hatcher generates and spawns it inside one method).
        [HarmonyPostfix]
        public static void Postfix(Pawn __result, bool __state)
        {
            if (__state)
            {
                AtavismContext.RecordBaby(__result);
            }
        }
    }
}
