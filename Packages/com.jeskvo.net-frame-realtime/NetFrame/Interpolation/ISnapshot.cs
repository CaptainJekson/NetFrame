// ISnapshot interface so we can reuse it for all kinds of systems.
// for example, NetworkTransform, NetworkRigidbody, CharacterController etc.
// NOTE: we use '<T>' and 'where T : Snapshot' to avoid boxing.
// List<Snapshot> would cause allocations through boxing.
namespace NetFrame.Interpolation
{
    public interface ISnapshot
    {
        // the remote timestamp (when it was sent by the remote)
        double RemoteTime { get; set; }

        // the local timestamp (when it was received on our end)
        // technically not needed for basic snapshot interpolation.
        // only for dynamic buffer time adjustment.
        double LocalTime { get; set; }
    }
}
