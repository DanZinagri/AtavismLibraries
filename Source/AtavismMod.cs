using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AtavismLibraries;

public class AtavismMod : Mod
{
    public static Harmony Harm;
    public static AtavismSettings Settings;

    public AtavismMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<AtavismSettings>();

        // Patches from the Mod constructor rather than a StaticConstructorOnStartup class, so the work
        // happens during mod loading instead of in the UI freeze at the end of startup. The assembly is
        // passed explicitly: on .NET 10 (1.7) the parameterless PatchAll() reads the calling assembly off
        // the stack, which breaks when the JIT inlines the call (Harmony issue #395).
        Harm = new Harmony("DanZinagri.Atavism");
        Harm.PatchAll(Assembly.GetExecutingAssembly());
        EggHatchCompat.Apply(Harm);
    }

    public override string SettingsCategory()
    {
        return "Atavism_SettingsCategory".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        if (BGICompat.Active)
        {
            listing.CheckboxLabeled(
                "Atavism_UseBGI".Translate(),
                ref Settings.useBetterGeneInheritance,
                "Atavism_UseBGIDesc".Translate());
        }
        else
        {
            GUI.color = Color.gray;
            listing.Label("Atavism_BGINotLoaded".Translate());
            GUI.color = Color.white;
        }

        listing.End();
        base.DoSettingsWindowContents(inRect);
    }
}
