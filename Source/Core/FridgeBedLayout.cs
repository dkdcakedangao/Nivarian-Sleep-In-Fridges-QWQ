using Verse;

// 这边真的是石山了
// 短边计算+长边计算
// 容量计算
// 话说，我测试这个的时候，完全没试那些雷霆模组的占地超大的冰箱，应该不会有问题吧？
// 待优化
namespace NivarianSleepInFridges
{
    internal sealed class FridgeBedLayout
    {
        internal ThingDef BedDef { get; private set; }
        internal IntVec3 Position { get; private set; }
        internal Rot4 Rotation { get; private set; }
        internal int Capacity { get; private set; }

        private FridgeBedLayout(ThingDef bedDef, IntVec3 position, Rot4 rotation, int capacity)
        {
            BedDef = bedDef;
            Position = position;
            Rotation = rotation;
            Capacity = capacity;
        }

        internal static FridgeBedLayout For(ThingWithComps fridge)
        {
            CellRect fridgeRect = fridge.OccupiedRect();
            int shortSide = fridgeRect.Width < fridgeRect.Height ? fridgeRect.Width : fridgeRect.Height;
            int capacity = shortSide < 2 ? 1 : 2;

            ThingDef bedDef;
            if (fridgeRect.Width == 1 && fridgeRect.Height == 1)
            {
                bedDef = NSIF_DefOf.NSIF_FridgeBedProxySingleCell;
            }
            else if (capacity == 1)
            {
                bedDef = NSIF_DefOf.NSIF_FridgeBedProxySingle;
            }
            else
            {
                bedDef = NSIF_DefOf.NSIF_FridgeBedProxyDouble;
            }

            Rot4 rotation = ChooseRotation(fridge, fridgeRect);
            IntVec3 position = ChoosePosition(fridge, fridgeRect, bedDef, rotation);
            return new FridgeBedLayout(bedDef, position, rotation, capacity);
        }

        internal bool Matches(Building_FridgeBedProxy proxy)
        {
            return proxy != null
                && proxy.Spawned
                && proxy.def == BedDef
                && proxy.Position == Position
                && proxy.Rotation == Rotation;
        }

        // 朝向
        private static Rot4 ChooseRotation(ThingWithComps fridge, CellRect rect)
        {
            Rot4 rotation;
            if (rect.Width == rect.Height)
            {
                rotation = fridge.Rotation;
                return rotation.IsVertical ? rotation.Opposite : rotation;
            }

            if (rect.Height > rect.Width)
            {
                return fridge.Rotation.IsVertical ? fridge.Rotation.Opposite : Rot4.South;
            }

            return fridge.Rotation.IsHorizontal ? fridge.Rotation : Rot4.East;
        }

        private static IntVec3 ChoosePosition(ThingWithComps fridge, CellRect fridgeRect, ThingDef bedDef, Rot4 rotation)
        {
            IntVec3 best = fridge.Position;
            int bestDistance = int.MaxValue;
            int bestForward = int.MinValue;
            bool found = false;

            foreach (IntVec3 candidate in fridgeRect.Cells)
            {
                CellRect bedRect = GenAdj.OccupiedRect(candidate, rotation, bedDef.Size);
                if (!Contains(fridgeRect, bedRect))
                {
                    continue;
                }

                int deltaX = (bedRect.minX + bedRect.maxX) - (fridgeRect.minX + fridgeRect.maxX);
                int deltaZ = (bedRect.minZ + bedRect.maxZ) - (fridgeRect.minZ + fridgeRect.maxZ);
                int distance = deltaX * deltaX + deltaZ * deltaZ;
                IntVec3 facing = fridge.Rotation.FacingCell;
                int forward = deltaX * facing.x + deltaZ * facing.z;

                if (!found
                    || distance < bestDistance
                    || (distance == bestDistance && forward > bestForward)
                    || (distance == bestDistance && forward == bestForward && IsEarlier(candidate, best)))
                {
                    best = candidate;
                    bestDistance = distance;
                    bestForward = forward;
                    found = true;
                }
            }

            if (!found)
            {
                Log.Error("[Nivarian Sleep In Fridges] Could not fit bed proxy inside " + fridge.def.defName + ".");
            }

            return best;
        }

        private static bool Contains(CellRect outer, CellRect inner)
        {
            return inner.minX >= outer.minX
                && inner.maxX <= outer.maxX
                && inner.minZ >= outer.minZ
                && inner.maxZ <= outer.maxZ;
        }

        private static bool IsEarlier(IntVec3 left, IntVec3 right)
        {
            return left.z < right.z || (left.z == right.z && left.x < right.x);
        }
    }
}
