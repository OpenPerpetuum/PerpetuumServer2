using Perpetuum.Zones;

namespace Perpetuum.Players
{
    public class PlayerMovement
    {
        private static readonly TimeSpan minStepTime = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan maxElapsedTime = TimeSpan.FromSeconds(1);
        private readonly Player player;

        public PlayerMovement(Player player)
        {
            this.player = player;
        }

        public void Update(TimeSpan elapsed)
        {
            double speed = player.Speed;

            if (speed <= 0.0)
            {
                return;
            }

            elapsed = elapsed.Min(maxElapsedTime);

            double angle = player.Direction * MathHelper.PI2;
            double vx = Math.Sin(angle) * speed;
            double vy = Math.Cos(angle) * speed;
            double px = player.CurrentPosition.X;
            double py = player.CurrentPosition.Y;

            while (elapsed > TimeSpan.Zero)
            {
                TimeSpan time = minStepTime;

                if (elapsed < minStepTime)
                {
                    time = elapsed;
                }

                elapsed -= minStepTime;

                double nx = px + (vx * time.TotalSeconds);
                double ny = py - (vy * time.TotalSeconds);

                int dx = (int)px - (int)nx;
                int dy = (int)py - (int)ny;

                if ((dx != 0 || dy != 0) &&
                    !player.IsWalkable((int)nx, (int)ny))
                {
                    break;
                }

                px = nx;
                py = ny;
            }

            player.TryMove(new Position(px, py));
        }
    }
}
