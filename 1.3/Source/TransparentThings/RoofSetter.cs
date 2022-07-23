using RimWorld;
using UnityEngine;
using Verse;

namespace TransparentThings
{
    public class RoofSetter : Building
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                foreach (var cell in this.OccupiedRect())
                {
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                }
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            foreach (var cell in this.OccupiedRect())
            {
                this.Map.roofGrid.SetRoof(cell, null);
            }
            base.Destroy(mode);
        }
    }
}
