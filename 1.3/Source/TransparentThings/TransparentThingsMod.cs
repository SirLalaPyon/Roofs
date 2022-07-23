using UnityEngine;
using Verse;

namespace TransparentThings
{
    public class TransparentThingsMod : Mod
    {
        public static TransparentThingsSettings settings;
        public TransparentThingsMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<TransparentThingsSettings>();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return (Core.hasTransparentTrees || Core.hasTransparentRoofs) ? "TT.TransparentThings".Translate() : "";
        }
    }
    public class TransparentThingsSettings : ModSettings
    {
        public bool enableTreeTransparency = true;
        public bool enableRoofTransparency = true;
        public bool makeRoofsSelectable = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableTreeTransparency, "enableTreeTransparency", true);
            Scribe_Values.Look(ref enableRoofTransparency, "enableRoofTransparency", true);
            Scribe_Values.Look(ref makeRoofsSelectable, "makeRoofsSelectable", true);
        }
        public void DoSettingsWindowContents(Rect inRect)
        {
            Rect rect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(rect);
            if (Core.hasTransparentTrees)
            {
                listingStandard.CheckboxLabeled("TT.EnableTreeTransparency".Translate(), ref enableTreeTransparency);
            }
            if (Core.hasTransparentRoofs)
            {
                listingStandard.CheckboxLabeled("TT.EnableRoofTransparency".Translate(), ref enableRoofTransparency);
                listingStandard.CheckboxLabeled("TT.MakeRoofsSelectable".Translate(), ref makeRoofsSelectable);
            }
            listingStandard.End();
        }
    }
}
