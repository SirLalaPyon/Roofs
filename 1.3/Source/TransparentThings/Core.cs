using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TransparentThings
{
    [StaticConstructorOnStartup]
    public static class Core
    {
        public static bool hasTransparentTrees;
        public static bool hasTransparentRoofs;
        public static List<ThingDef> transparentRoofs;
        static Core()
        {
            hasTransparentTrees = DefDatabase<ThingDef>.AllDefs.Any(x => x.plant != null && HasTransparencyExtension(x));
            transparentRoofs = DefDatabase<ThingDef>.AllDefs.Where(x => typeof(RoofSetter).IsAssignableFrom(x.thingClass) && HasTransparencyExtension(x)).ToList();
            hasTransparentRoofs = transparentRoofs.Any();

            static bool HasTransparencyExtension(ThingDef x)
            {
                var extension = x.GetModExtension<ThingExtension>();
                return extension != null && (extension.transparentWhenPawnIsInsideArea || extension.transparentWhenItemIsInsideArea);
            }

            new Harmony("TransparentThings.Mod").PatchAll();
        }

        public static Dictionary<Map, Dictionary<IntVec3, List<Thing>>> cachedTransparentableCellsByMaps = 
            new Dictionary<Map, Dictionary<IntVec3, List<Thing>>>();
        public static Dictionary<Thing, ThingExtension> cachedTransparentableThingsByExtensions = new Dictionary<Thing, ThingExtension>();
        public static Dictionary<Map, List<ThingWithExtension>> cachedTransparentableThingsByMaps = new Dictionary<Map, List<ThingWithExtension>>();
        public static Dictionary<Thing, HashSet<IntVec3>> cachedCells = new Dictionary<Thing, HashSet<IntVec3>>();
        public static Dictionary<Thing, Shader> lastCachedShaders = new Dictionary<Thing, Shader>();
        public static List<Thing> transparentThings = new List<Thing>();
        public static Dictionary<Thing, bool> matchedItems = new Dictionary<Thing, bool>();
        public static HashSet<IntVec3> GetTransparentCheckArea(this Thing thing, ThingExtension extension)
        {
            if (!cachedCells.TryGetValue(thing, out var cells))
            {
                cachedCells[thing] = cells = GetTransparentCheckAreaInt(thing, extension);
            }
            return cells;
        }
        private static HashSet<IntVec3> GetTransparentCheckAreaInt(Thing thing, ThingExtension extension)
        {
            var anchor = thing.OccupiedRect().BottomLeft;
            var cellRect = new CellRect(anchor.x, anchor.z, extension.firstArea.x, extension.firstArea.z);
            if (extension.firstAreaOffset != IntVec2.Zero)
            {
                cellRect = cellRect.MovedBy(extension.firstAreaOffset);
            }
            var cells = cellRect.Cells.ToList();
            if (extension.secondArea != IntVec2.Zero)
            {
                var cells2 = new CellRect(anchor.x, anchor.z, extension.secondArea.x, extension.secondArea.z);
                if (extension.secondAreaOffset != IntVec2.Zero)
                {
                    cells2 = cells2.MovedBy(extension.secondAreaOffset);
                }
                cells.AddRange(cells2);
            }
            return cells.Where(x => x.InBounds(thing.Map)).ToHashSet();
        }
        public static bool HasItemsInCell(IntVec3 cell, Map map, ThingExtension extension)
        {
            foreach (var thing in cell.GetThingList(map))
            {
                if (ItemMatches(thing, extension))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool BaseItemMatches(Thing thing)
        {
            return thing is Pawn || thing.def.category == ThingCategory.Item;
        }

        public static bool ItemMatches(Thing thing, ThingExtension extension)
        {
            if (!matchedItems.TryGetValue(thing, out var result))
            {
                matchedItems[thing] = result = (thing is Pawn && extension.transparentWhenPawnIsInsideArea
                || thing.def.category == ThingCategory.Item && extension.transparentWhenItemIsInsideArea)
                && (extension.ignoredThings is null || !extension.ignoredThings.Contains(thing.def));
            }
            return result;
        }

        public static void RecheckTransparency(Thing thing, Thing otherThing, ThingExtension extension)
        {
            if (thing != otherThing && thing.Spawned && thing.Map == otherThing.Map)
            {
                if (!lastCachedShaders.TryGetValue(thing, out var shader))
                {
                    lastCachedShaders[thing] = shader = thing.Graphic.Shader;
                }

                bool isTransparent = shader == thing.GetTransparencyShader();
                if (!isTransparent && ItemMatches(otherThing, extension))
                {
                    var cells = GetTransparentCheckArea(thing, extension);
                    if (cells.Contains(otherThing.Position))
                    {
                        thing.Map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlag.Things);
                        if (!transparentThings.Contains(thing))
                        {
                            transparentThings.Add(thing);
                        }
                    }
                }

                if (isTransparent)
                {
                    var cells = GetTransparentCheckArea(thing, extension);
                    if (!cells.Contains(otherThing.Position) && !cells.Any(x => HasItemsInCell(x, thing.Map, extension)))
                    {
                        thing.Map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlag.Things);
                        transparentThings.Remove(thing);
                    }
                }
            }
        }

        public static Shader GetTransparencyShader(this Thing thing)
        {
            if (thing is Plant)
            {
                return TransparentThingsDefOf.TransparentPlant.Shader;
            }
            return TransparentThingsDefOf.TransparentPostLight.Shader;
        }

        public static void FormTransparencyGridIn(Map map)
        {
            if (cachedTransparentableThingsByMaps.TryGetValue(map, out var list))
            {
                if (cachedTransparentableCellsByMaps.TryGetValue(map, out var data))
                {
                    data.Clear();
                }
                else
                {
                    cachedTransparentableCellsByMaps[map] = data = new Dictionary<IntVec3, List<Thing>>();
                }

                foreach (var item in list)
                {
                    var cells = item.thing.GetTransparentCheckArea(item.extension);
                    foreach (var cell in cells)
                    {
                        if (data.ContainsKey(cell))
                        {
                            data[cell].Add(item.thing);
                        }
                        else
                        {
                            data[cell] = new List<Thing> { item.thing };
                        }
                    }
                }
            }
        }

        public static bool TransparencyAllowed(this Thing thing)
        {
            if (thing is Plant)
            {
                return TransparentThingsMod.settings.enableTreeTransparency;
            }
            else if (thing is RoofSetter)
            {
                return TransparentThingsMod.settings.enableRoofTransparency;
            }
            return false;
        }

        [HarmonyPatch(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow")]
        public class SavedGameLoaderNow_LoadGameFromSaveFileNow
        {
            public static void Prefix()
            {
                cachedTransparentableThingsByExtensions.Clear();
                cachedTransparentableThingsByMaps.Clear();
            }
        }

        [HarmonyPatch(typeof(Thing), "SpawnSetup")]
        public class Thing_SpawnSetup_Patch
        {
            private static void Postfix(Thing __instance)
            {
                if (__instance.TransparencyAllowed())
                {
                    var extension = __instance.def.GetModExtension<ThingExtension>();
                    if (extension != null && (extension.transparentWhenItemIsInsideArea || extension.transparentWhenPawnIsInsideArea))
                    {
                        var map = __instance.Map;
                        cachedTransparentableThingsByExtensions[__instance] = extension;
                        if (!cachedTransparentableThingsByMaps.TryGetValue(map, out var list))
                        {
                            cachedTransparentableThingsByMaps[__instance.Map] = list = new List<ThingWithExtension>();
                        }
                        if (!list.Any(x => x.thing == __instance))
                        {
                            list.Add(new ThingWithExtension { thing = __instance, extension = __instance.def.GetModExtension<ThingExtension>()});
                        }
                        FormTransparencyGridIn(map);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Thing), "DeSpawn")]
        public class Thing_DeSpawn_Patch
        {
            private static void Prefix(Thing __instance)
            {
                cachedTransparentableThingsByExtensions.Remove(__instance);
                cachedCells.Remove(__instance);
                var map = __instance.Map;
                if (map != null)
                {
                    if (__instance.TransparencyAllowed() && cachedTransparentableThingsByMaps.TryGetValue(map, out var list))
                    {
                        var count = list.RemoveAll(x => x.thing == __instance);
                        if (count > 0)
                        {
                            FormTransparencyGridIn(map);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Thing), "Position", MethodType.Setter)]
        public class Thing_Position_Patch
        {
            public static void Prefix(Thing __instance, out bool __state, IntVec3 value)
            {
                if (BaseItemMatches(__instance) && __instance.Map != null && __instance.Position != value)
                {
                    __state = true;
                }
                else
                {
                    __state = false;
                }
            }
            public static void Postfix(Thing __instance, bool __state)
            {
                if (__state)
                {
                    if (cachedTransparentableCellsByMaps.TryGetValue(__instance.Map, out var list)
                            && list.TryGetValue(__instance.Position, out var transparentThigns))
                    {
                        foreach (var thing in transparentThigns)
                        {
                            RecheckTransparency(thing, __instance, thing.def.GetModExtension<ThingExtension>());
                        }
                    }
                    for (var i = transparentThings.Count - 1; i >= 0; i--)
                    {
                        var thing = transparentThings[i];
                        if (thing.Map == __instance.Map && thing.Position.DistanceTo(__instance.Position) < 20)
                        {
                            RecheckTransparency(thing, __instance, thing.def.GetModExtension<ThingExtension>());
                        }
                    }
                }
            }
        }


        [HarmonyPatch(typeof(Thing), "Graphic", MethodType.Getter)]
        public class Patch_Graphic_Postfix
        {
            private static void Postfix(Thing __instance, ref Graphic __result)
            {
                if (__instance.TransparencyAllowed() && cachedTransparentableThingsByExtensions.TryGetValue(__instance, out var extension))
                {
                    var cells = GetTransparentCheckArea(__instance, extension);
                    if (cells.Any(x => HasItemsInCell(x, __instance.Map, extension)))
                    {
                        var shader = __instance.GetTransparencyShader();
                        __result = GraphicDatabase.Get(__result.GetType(), "Transparent/" + __result.path, shader, 
                            __instance.def.graphicData.drawSize, __result.color, __result.colorTwo);
                        __instance.Map.mapDrawer.MapMeshDirty(__instance.Position, MapMeshFlag.Things);
                        lastCachedShaders[__instance] = shader;
                        if (!transparentThings.Contains(__instance))
                        {
                            transparentThings.Add(__instance);
                        }
                    }
                    else
                    {
                        lastCachedShaders[__instance] = __instance.def.graphicData.shaderType.Shader;
                        transparentThings.Remove(__instance);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
        public static class PlaySettings_DoPlaySettingsGlobalControls_Patch
        {
            [HarmonyPrepare]
            public static bool Prepare() => hasTransparentRoofs;
            public static void Postfix(WidgetRow row, bool worldView)
            {
                if (worldView)
                {
                    return;
                }
                var oldValue = TransparentThingsMod.settings.makeRoofsSelectable;
                row.ToggleableIcon(ref TransparentThingsMod.settings.makeRoofsSelectable, TexButton.ShowRoofOverlay, "TT.MakeRoofsSelectable".Translate(), SoundDefOf.Mouseover_ButtonToggle);
                if (oldValue != TransparentThingsMod.settings.makeRoofsSelectable)
                {
                    foreach (var roof in transparentRoofs)
                    {
                        roof.selectable = TransparentThingsMod.settings.makeRoofsSelectable;
                    }
                }
            }
        }
    }
}
