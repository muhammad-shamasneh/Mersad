using Mersad.Api.Models;

namespace Mersad.Api.Detection;

public class DetectionContext
{
    public IReadOnlyCollection<ProcessEvent> ProcessEvents { get; }

    public IReadOnlyCollection<NetworkEvent> NetworkEvents { get; }

    public DetectionContext(
        IReadOnlyCollection<ProcessEvent> processEvents,
        IReadOnlyCollection<NetworkEvent> networkEvents)
    {
        ProcessEvents = processEvents;
        NetworkEvents = networkEvents;
    }
}