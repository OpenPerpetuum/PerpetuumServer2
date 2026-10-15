using System;
using System.Diagnostics;
using System.Numerics;
using Perpetuum.ExportedTypes;
using Perpetuum.Units;
using Perpetuum.Zones.Terrains;

namespace Perpetuum.Zones
{
    public class LOSResult
    {
        public static readonly LOSResult None = new LOSResult { hit = false };

        public bool hit;
        public Position position;
        public BlockingFlags blockingFlags;
    }

    public static class LineOfSight
    {
        static LineOfSight()
        {
            Debug = false;
        }

        public static bool Debug { private get; set; }

        private const double MAX_DISTANCE  = 40.0;
        private const double MAX_AMPLITUDE = 16.0;

        [Conditional("DEBUG")]
        private static void OnDebugLOS(IZone zone,BeamType beamType, Position position,bool aligned)
        {
            if (!Debug)
                return;

            if (aligned)
            {
                zone.CreateAlignedDebugBeam(beamType, zone.FixZ(position));
                return;
            }
            
            zone.CreateDebugBeam(beamType, position);
        }

        [NotNull]
        public static LOSResult IsInLineOfSight(this IZone zone, Unit sourceUnit, Unit targetUnit, bool ballistic)
        {
            var source = sourceUnit.PositionWithHeight.ToVector3();
            var target = targetUnit.PositionWithHeight.ToVector3();

            var direction = target - source;
            var len = direction.Length();
            direction = Vector3.Normalize(direction);

            if (targetUnit.HitSize > 0)
            {
                var hitsize = (targetUnit.HitSize * 0.5).Clamp(1, 10);
                len = (float)CylinderIntersection(source, direction, targetUnit.CurrentPosition.ToVector3(), target, hitsize);
            }

            return IsInLineOfSight(zone, source, direction, len, ballistic);
        }

        private static double CylinderIntersection(Vector3 start, Vector3 dir, Vector3 cylinderStart, Vector3 cylinderEnd, double radius)
        {
            var ab = cylinderEnd - cylinderStart;
            var ao = start - cylinderStart;
            var aoxAb = Vector3.Cross(ao,ab);
            var vxAb = Vector3.Cross(dir,ab);
            var ab2 = Vector3.Dot(ab,ab);
            var a = Vector3.Dot(vxAb,vxAb);
            var b = 2 * Vector3.Dot(vxAb,aoxAb);
            var c = Vector3.Dot(aoxAb,aoxAb) - (radius * radius * ab2);
            var d = b * b - 4 * a * c;
            var time = ((-b - Math.Sqrt(d)) / (2 * a));
            return Math.Abs(time);
        }

        [NotNull]
        public static LOSResult IsInLineOfSight(this IZone zone, Unit sourceUnit, Position target, bool ballistic)
        {
            var source = sourceUnit.PositionWithHeight.ToVector3();
            var direction = target.ToVector3() - source;
            var len = direction.Length();
            direction = Vector3.Normalize(direction);
            return IsInLineOfSight(zone, source, direction, len, ballistic);
        }

        [NotNull]
        public static LOSResult IsInLineOfSight(this IZone zone, Position start, Unit target, bool ballistic)
        {
            var s = start.ToVector3();
            var direction = target.PositionWithHeight.ToVector3() - s;
            var len = direction.Length() - 1;
            direction = Vector3.Normalize(direction);
            return IsInLineOfSight(zone, s, direction, len, ballistic);
        }

        [NotNull]
        private static LOSResult IsInLineOfSight(IZone zone, Vector3 origin, Vector3 direction, float distance, bool ballistic)
        {
            if (distance.IsApproximatelyEqual(0.0f))
            {
                var blockingInfo = zone.Terrain.Blocks.GetValue(origin);
                var losResult = new LOSResult
                {
                    hit = true,
                    position = (Position)origin,
                    blockingFlags = blockingInfo.Flags
                };
                return losResult;
            }

            if (RayPassesAboveAllCrossedChunks(zone, origin, direction, distance))
            {
                return LOSResult.None;
            }

            var lastAltitude = zone.Terrain.Altitude.GetAltitudeAsDouble(origin) + 2;

            var lx = (int) origin.X;
            var ly = (int) origin.Y;

            for (var i = 0.0f;i <= distance;i += 0.3f)
            {
                var p = origin + direction * i;

                OnDebugLOS(zone,BeamType.orange_20sec,(Position)p,true);

                var dx = Math.Abs((int)p.X - lx);
                var dy = Math.Abs((int)p.Y - ly);

                if (dx >= 1 || dy >= 1)
                {
                    lx = (int) p.X;
                    ly = (int) p.Y;

                    var blockingInfo = zone.Terrain.Blocks.GetValue(p);
                    var altitude = GetAltitude(zone,p,ref lastAltitude);
                    var blockingHeight = blockingInfo.Height + altitude;

                    if (ballistic)
                    {
                        var x = i / distance;
                        var maxAmp = (Math.Min(distance,MAX_DISTANCE) / MAX_DISTANCE) * MAX_AMPLITUDE;
                        var amp = (-Math.Sin((x * Math.PI) + Math.PI)) * (Math.Pow(x,0.5) * 1.5) * maxAmp;

                        p.Z += (float)amp;
                    }

                    OnDebugLOS(zone,BeamType.red_20sec,(Position)p,false);

                    if (blockingHeight < p.Z)
                        continue;

                    OnDebugLOS(zone,BeamType.blue_20sec,(Position)p,false);

                    var losResult = new LOSResult
                    {
                        hit = true,
                        position = (Position)p,
                        blockingFlags = blockingInfo.Flags
                    };

                    return losResult;
                }
            }

            return LOSResult.None;
        }

        /// <summary>
        /// Coarse pre-check: walks the chunk grid the ray crosses (Amanatides &amp; Woo) and reports
        /// whether every crossed chunk is provably below the ray, so the per-tile loop below can be
        /// skipped entirely. The per-tile check can raise a tile's effective blocking height above
        /// its raw (altitude + block) by at most 0.1 * (highest terrain in the zone + 2) through
        /// neighbor-tile smoothing (see GetAltitude), so a chunk is only trusted when the ray's
        /// lowest point clears its max height by that margin. Ballistic arcs only add height, so
        /// the linear min Z bounds the whole ray. Skipped entirely while any chunk is dirty, when
        /// the bounds may be stale in either direction.
        /// </summary>
        private static bool RayPassesAboveAllCrossedChunks(IZone zone, Vector3 origin, Vector3 direction, float distance)
        {
            var heightfield = zone.Heightfield;
            if (heightfield == null || heightfield.HasDirtyChunks)
            {
                return false;
            }

            float endZ = origin.Z + (float)(direction.Z * distance);
            float minZ = Math.Min(origin.Z, endZ);
            float clearance = minZ - (float)(0.1 * (heightfield.GlobalMaxHeight + 2.0));

            int stepX = direction.X >= 0 ? 1 : -1;
            int stepY = direction.Y >= 0 ? 1 : -1;
            int size = heightfield.ChunkSize;

            int chunkX = (int) (origin.X / size);
            int chunkY = (int) (origin.Y / size);
            float tMaxX = NextChunkBoundary(origin.X, direction.X, chunkX, size, stepX);
            float tMaxY = NextChunkBoundary(origin.Y, direction.Y, chunkY, size, stepY);
            float tDeltaX = direction.X != 0 ? size / Math.Abs(direction.X) : float.PositiveInfinity;
            float tDeltaY = direction.Y != 0 ? size / Math.Abs(direction.Y) : float.PositiveInfinity;

            while (true)
            {
                if (!heightfield.CanRayPassAboveChunk(chunkX, chunkY, clearance))
                {
                    return false;
                }

                if (tMaxX < tMaxY)
                {
                    chunkX += stepX;
                    if (tMaxX > distance)
                    {
                        return true;
                    }

                    tMaxX += tDeltaX;
                }
                else
                {
                    chunkY += stepY;
                    if (tMaxY > distance)
                    {
                        return true;
                    }

                    tMaxY += tDeltaY;
                }
            }
        }

        private static float NextChunkBoundary(float coordinate, float direction, int chunk, int size, int step)
        {
            if (direction == 0)
            {
                return float.PositiveInfinity;
            }

            float boundary = step > 0 ? (chunk + 1) * (float) size - coordinate : chunk * (float) size - coordinate;
            return boundary / direction;
        }

        private static double GetAltitude(IZone zone,Vector3 position,ref double lastAltitude)
        {
            var altitude = zone.Terrain.Altitude.GetAltitudeAsDouble(position);

            try
            {
                if (altitude < lastAltitude)
                    return altitude + (altitude - lastAltitude) * 0.6;

                return altitude + (lastAltitude - altitude) * 0.1;
            }
            finally
            {
                lastAltitude = altitude;
            }
        }
    }
}