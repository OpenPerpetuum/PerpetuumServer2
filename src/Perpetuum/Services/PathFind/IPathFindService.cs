namespace Perpetuum.Services.PathFind
{
    public interface IPathFindService
    {
        void EnqueuePathFinding(IPathFindInfo pathFindInfo);
    }
}
