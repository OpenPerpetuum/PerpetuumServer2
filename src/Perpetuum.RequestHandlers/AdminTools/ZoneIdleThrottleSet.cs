using Perpetuum.Host.Requests;
using Perpetuum.Zones;

namespace Perpetuum.RequestHandlers.AdminTools
{
    /// <summary>
    /// GameAdmin channel command: switches the zone idle throttle on (state 1) or off
    /// (state 0) on the running server, so its CPU savings can be measured without a restart.
    /// </summary>
    public class ZoneIdleThrottleSet : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            int state = request.Data.GetOrDefault<int>(k.state, 1);
            ZoneIdleThrottling.Enabled = state != 0;

            Message.Builder.FromRequest(request)
                .SetData(k.state, ZoneIdleThrottling.Enabled ? 1 : 0)
                .Send();
        }
    }
}
