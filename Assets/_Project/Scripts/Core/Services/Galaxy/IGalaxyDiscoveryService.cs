public interface IGalaxyDiscoveryService
{
    bool IsSystemDiscovered(string systemId);
    void DiscoverSystem(string systemId);
    void VisitSystem(string systemId);
}