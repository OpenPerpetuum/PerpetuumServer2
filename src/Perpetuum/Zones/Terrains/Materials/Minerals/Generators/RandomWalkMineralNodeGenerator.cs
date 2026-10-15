using SkiaSharp;

namespace Perpetuum.Zones.Terrains.Materials.Minerals.Generators
{
    public class RandomWalkMineralNodeGenerator : MineralNodeGeneratorBase
    {
        public RandomWalkMineralNodeGenerator(IZone zone) : base(zone)
        {
            BrushSize = 4;
        }

        private int BrushSize { get; set; }

        protected override Dictionary<SKPointI, double> GenerateNoise(Position startPosition)
        {
            var closed = new HashSet<SKPointI>();
            var tiles = new Dictionary<SKPointI, double>();

            var q = new Queue<SKPointI>();
            q.Enqueue(startPosition);

            var i = 0;

            while (q.TryDequeue(out SKPointI current) && i < 50000)
            {
                i++;

                foreach (var point in current.FloodFill(IsValid).Take(BrushSize * BrushSize))
                {
                    tiles.AddOrUpdate(point, 1, c => c + 1);

                    if (tiles.Count >= MaxTiles)
                        return tiles;
                }

                var r = new List<SKPointI>();

                foreach (var np in current.GetNeighbours())
                {
                    if (closed.Contains(np))
                        continue;

                    if (!IsValid(np))
                    {
                        closed.Add(np);
                        continue;
                    }

                    r.Add(np);
                }

                if (r.Count == 0)
                    q.Enqueue(tiles.Keys.RandomElement());
                else
                    q.Enqueue(r.RandomElement());
            }

            return tiles;
        }
    }
}