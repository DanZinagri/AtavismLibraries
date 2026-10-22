using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace AtavismLibraries
{
    // Atavism for babies hatched from eggs. Egg layers never go through
    // PregnancyUtility.ApplyBirthOutcome (their live pregnancy is cancelled), so
    // the two egg hatchers are patched instead, each only when its mod is loaded.
    // Both are found by name: a compile-time reference to an optional mod's types
    // fails to load when that mod is missing.
    //
    //  VEF (VEF.Genes.CompHumanHatcher, used by Alpha Genes' egg laying and others):
    //    Hatch() generates the baby from the mother's kind with no gene list, then
    //    AddGene()s a mother/father mix on top. The prefix opens the birth context
    //    for the mother and empties VEF's stored mother/father gene lists, so the
    //    atavism gene set is all the baby gets.
    //
    //  VRE Saurid (VRESaurids.Comp_HumanHatcher):
    //    GenerateChild() builds the baby when the egg is laid, then applies Saurid's
    //    own inherited xenotype or a "Hybrid" label. The prefix opens the context and
    //    the postfix re-asserts the target xenotype afterwards.
    //
    // The generation itself is handled by the existing PawnGenerator.GeneratePawn
    // prefix, exactly as for a live birth. Cost: a few checks per hatched egg.
    public static class EggHatchCompat
    {
        public const string VEFPackageId = "OskarPotocki.VanillaFactionsExpanded.Core";

        public const string SauridPackageId = "vanillaracesexpanded.saurid";

        public static void Apply(Harmony harmony)
        {
            if (ModsConfig.IsActive(VEFPackageId))
            {
                TryPatch(harmony, "VEF.Genes.CompHumanHatcher:Hatch",
                    nameof(VEFHatchPrefix), nameof(VEFHatchPostfix));
            }
            if (ModsConfig.IsActive(SauridPackageId))
            {
                TryPatch(harmony, "VRESaurids.Comp_HumanHatcher:GenerateChild",
                    nameof(SauridGenerateChildPrefix), nameof(SauridGenerateChildPostfix));
            }
        }

        private static void TryPatch(Harmony harmony, string target, string prefix, string postfix)
        {
            MethodInfo original = AccessTools.Method(target);
            if (original == null)
            {
                Log.Warning($"[Atavism] {target} was not found, so eggs from that mod hatch without atavism.");
                return;
            }
            harmony.Patch(original,
                prefix: new HarmonyMethod(typeof(EggHatchCompat), prefix),
                postfix: new HarmonyMethod(typeof(EggHatchCompat), postfix),
                finalizer: new HarmonyMethod(typeof(EggHatchCompat), nameof(CloseContext)));
        }

        // Only the modes that stack a parent mix on the target need one, and
        // excess does not, so most lineage defs skip the work entirely.
        private static List<GeneDef> ParentGenesIfNeeded(GeneticAtavismDef def, Pawn father, Pawn mother)
        {
            if (def.InheritanceMode == AtavismInheritanceMode.excess)
                return null;
            return PregnancyUtility.GetInheritedGenes(father, mother);
        }

        public static void VEFHatchPrefix(Pawn ___hatcheeParent, Pawn ___otherParent,
            ref List<GeneDef> ___motherGenes, ref List<GeneDef> ___fatherGenes)
        {
            AtavismContext.Close();
            Pawn mother = ___hatcheeParent;
            if (mother?.genes == null)
                return;

            GeneticAtavismDef def = AtavismUtils.TryPickAtavismDef(mother);
            if (def == null)
                return;

            AtavismContext.Open(mother, ___otherParent, def, ParentGenesIfNeeded(def, ___otherParent, mother));

            // VEF adds these to the hatched baby after generating it. The egg is
            // destroyed at the end of Hatch, so nothing else reads them.
            ___motherGenes = new List<GeneDef>();
            ___fatherGenes = new List<GeneDef>();
        }

        public static void VEFHatchPostfix()
        {
            if (AtavismContext.Applied)
            {
                AtavismUtils.TrySendAtavismLetter(AtavismContext.Baby, AtavismContext.Mother, AtavismContext.TargetXenotype);
            }
        }

        public static void SauridGenerateChildPrefix(Pawn ___mother, Pawn ___father)
        {
            AtavismContext.Close();
            if (___mother?.genes == null)
                return;

            GeneticAtavismDef def = AtavismUtils.TryPickAtavismDef(___mother);
            if (def == null)
                return;

            // Saurid puts its own gene list in the generation request, which the
            // GeneratePawn prefix uses as the parent mix, so none is needed here.
            AtavismContext.Open(___mother, ___father, def);
        }

        // No letter here: Saurid builds the baby when the egg is laid, long before
        // it hatches, and the egg's genes tab already shows what it will be.
        public static void SauridGenerateChildPostfix(Pawn ___hatchee)
        {
            XenotypeDef target = AtavismContext.TargetXenotype;
            if (!AtavismContext.Applied || target == null || ___hatchee?.genes == null)
                return;

            ___hatchee.genes.SetXenotypeDirect(target);
            ___hatchee.genes.xenotypeName = null;
            ___hatchee.genes.iconDef = null;
            ___hatchee.genes.hybrid = false;
        }

        // A finalizer runs even when the hatch throws, so the mother can never stay
        // parked in the context for the next pawn generated anywhere.
        public static void CloseContext()
        {
            AtavismContext.Close();
        }
    }
}
