using System.Collections.Generic;

public class MissionGenerationContext
    {
        public string CurrentSystemId;
        public List<string> NeighborSystemIds = new();
        public int DangerLevel;
    }