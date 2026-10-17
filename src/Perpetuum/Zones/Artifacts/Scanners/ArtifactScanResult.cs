using SkiaSharp;

namespace Perpetuum.Zones.Artifacts.Scanners
{
    /// <summary>
    /// Describes one artifact scan operation's result
    /// </summary>
    public class ArtifactScanResult
    {
        public Artifact scannedArtifact;
        public SKPointI estimatedPosition;
        public double radius;
    }
}