using System;
using XRL.World;

namespace CyberneticTraderMod
{
    [Serializable]
    public class CyberneticTraderInjector
    {
        public bool BuildZone(Zone Z)
        {
            // This zone builder is used to inject the Cybernetic Trader into the Six Day Stilt
            // The actual placement is handled by the SDS_Trader.rpm file
            return true;
        }
    }
}
