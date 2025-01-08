namespace Perpetuum.Items.Templates
{
    public class CachedRobotTemplateReader(IRobotTemplateReader reader) : IRobotTemplateReader
    {
        private readonly IRobotTemplateReader _reader = reader;
        private Dictionary<int, RobotTemplate> _templates;

        public void Init()
        {
            _templates = _reader.GetAll().ToDictionary(t => t.ID);
        }

        public RobotTemplate? Get(int templateID)
        {
            return _templates.GetOrDefault(templateID);
        }

        public IEnumerable<RobotTemplate> GetAll()
        {
            return _templates.Values;
        }
    }
}