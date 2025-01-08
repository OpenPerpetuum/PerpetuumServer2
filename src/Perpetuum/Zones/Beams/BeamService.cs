using Perpetuum.ExportedTypes;
using Perpetuum.Reactive;
using System.Collections.Concurrent;

namespace Perpetuum.Zones.Beams
{
    public class BeamService : IBeamService
    {
        private readonly ConcurrentDictionary<long, Beam> _beams = [];
        private readonly Observable<Beam> _observable;

        public BeamService()
        {
            _observable = new AnonymousObservable<Beam>(OnSubscribe);
        }

        private void OnSubscribe(IObserver<Beam> observer)
        {
            foreach (KeyValuePair<long, Beam> kvp in _beams)
            {
                observer.OnNext(kvp.Value);
            }
        }

        public void Add(Beam beam)
        {
            if (beam.Type == BeamType.undefined)
            {
                return;
            }

            _beams[beam.Id] = beam;

            beam.Expired = b => Remove(beam);
            beam.Start();

            _observable.OnNext(beam);
        }

        public void Clear()
        {
            foreach (Beam beam in _beams.Values)
            {
                Remove(beam);
            }
        }

        public IEnumerable<Beam> All => _beams.Select(kvp => kvp.Value);

        private bool Remove(Beam beam)
        {
            try
            {
                return _beams.Remove(beam.Id);
            }
            finally
            {
                beam.Dispose();
            }
        }

        public IDisposable Subscribe(IObserver<Beam> observer)
        {
            return _observable.Subscribe(observer);
        }
    }
}