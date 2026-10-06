namespace Mersad.Api.Detection.Policies;

public static class SuspiciousNetworkPorts
{
    public static readonly HashSet<int> All =
    [
        4444,
        1337,
        31337
    ];

    public static bool Contains(int port)
    {
        return All.Contains(port);
    }
}