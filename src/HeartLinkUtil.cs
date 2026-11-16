using System.Collections.Generic;
using UnityEngine;

namespace ONIHeart
{
    /// <summary>
    /// Shared helpers for linking interface buildings and terminals to element hearts.
    /// Links are identified by world + bottom-left cell so hearts on different planets
    /// can coexist at the same coordinates without colliding.
    /// </summary>
    public static class HeartLink
    {
        public static bool IsAlive(HeartOfElements heart)
        {
            return heart != null && heart.gameObject != null && heart.Storage != null;
        }

        public static bool Matches(HeartOfElements heart, int worldId, int cell)
        {
            return IsAlive(heart) && worldId >= 0 && cell >= 0 &&
                   heart.GetMyWorldId() == worldId &&
                   heart.GetBottomLeftCell() == cell;
        }

        public static HeartOfElements Find(int worldId, int cell)
        {
            if (worldId < 0 || cell < 0)
            {
                return null;
            }

            for (int i = 0; i < HeartOfElements.All.Count; i++)
            {
                HeartOfElements candidate = HeartOfElements.All[i];
                if (Matches(candidate, worldId, cell))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Nearest heart in the device's own world; used for automatic connection.</summary>
        public static HeartOfElements NearestInWorld(int worldId, Vector3 position)
        {
            if (worldId < 0)
            {
                return null;
            }

            HeartOfElements best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < HeartOfElements.All.Count; i++)
            {
                HeartOfElements candidate = HeartOfElements.All[i];
                if (!IsAlive(candidate) || candidate.GetMyWorldId() != worldId)
                {
                    continue;
                }

                float distance = (candidate.transform.GetPosition() - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>All hearts, with the device's own world first, then other planets.</summary>
        public static List<HeartOfElements> AllInCycleOrder(int localWorldId)
        {
            List<HeartOfElements> hearts = new List<HeartOfElements>();
            for (int i = 0; i < HeartOfElements.All.Count; i++)
            {
                HeartOfElements candidate = HeartOfElements.All[i];
                if (IsAlive(candidate))
                {
                    hearts.Add(candidate);
                }
            }

            hearts.Sort((a, b) =>
            {
                int worldA = a.GetMyWorldId();
                int worldB = b.GetMyWorldId();
                bool localA = worldA == localWorldId;
                bool localB = worldB == localWorldId;
                if (localA != localB)
                {
                    return localA ? -1 : 1;
                }

                if (worldA != worldB)
                {
                    return worldA.CompareTo(worldB);
                }

                return a.GetBottomLeftCell().CompareTo(b.GetBottomLeftCell());
            });
            return hearts;
        }

        public static HeartOfElements NextInCycle(int localWorldId, int currentWorldId, int currentCell)
        {
            List<HeartOfElements> hearts = AllInCycleOrder(localWorldId);
            if (hearts.Count == 0)
            {
                return null;
            }

            int index = hearts.FindIndex(h => Matches(h, currentWorldId, currentCell));
            return hearts[(index + 1) % hearts.Count];
        }

        /// <summary>Name shown on the target button, with the planet appended for remote hearts.</summary>
        public static string DisplayName(HeartOfElements heart, int viewerWorldId)
        {
            if (!IsAlive(heart))
            {
                return STRINGS.UI.ONIHEART.NO_TARGET;
            }

            string name = heart.gameObject.GetProperName();
            int heartWorldId = heart.GetMyWorldId();
            if (heartWorldId == viewerWorldId)
            {
                return name;
            }

            string worldName = GetWorldName(heartWorldId);
            return string.IsNullOrEmpty(worldName)
                ? name
                : string.Format(STRINGS.UI.ONIHEART.TARGET_ON_WORLD, name, worldName);
        }

        public static string GetWorldName(int worldId)
        {
            if (worldId < 0 || ClusterManager.Instance == null)
            {
                return null;
            }

            WorldContainer world = ClusterManager.Instance.GetWorld(worldId);
            if (world == null)
            {
                return null;
            }

            ClusterGridEntity entity = world.GetComponent<ClusterGridEntity>();
            if (entity != null && !string.IsNullOrEmpty(entity.Name))
            {
                return entity.Name;
            }

            return world.worldName;
        }

        public static Tag StoredTag(int worldId, int cell)
        {
            return TagManager.Create("ONIHeartStored_" + worldId + "_" + cell);
        }

        public static Tag BottleSourceTag(int worldId, int cell)
        {
            return TagManager.Create("ONIHeartBottleSource_" + worldId + "_" + cell);
        }
    }
}
