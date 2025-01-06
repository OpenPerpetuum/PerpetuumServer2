namespace Perpetuum.Items
{
    public class SparkTeleportDevice : Item
    {
        public int BaseId
        {
            get
            {
                return ED.Options.GetOption<int>("baseId");
            }
        }
    }
}
