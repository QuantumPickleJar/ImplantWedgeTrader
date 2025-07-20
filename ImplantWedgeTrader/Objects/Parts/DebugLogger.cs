using System;
using XRL.World;
using XRL.World.Parts;
using XRL.Core;
using XRL.Messages;
using XRL;

namespace XRL.World.Parts
{
    [Serializable]
    public class DebugLogger : IPart
    {
        public override bool SameAs(IPart p)
        {
            return false;
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return true; // Listen to all events for debugging
        }        public override bool FireEvent(Event E)
        {
            try
            {
                // Log important events
                if (E.ID == "ZoneBuilt" || E.ID == "ObjectCreated" || E.ID == "GetDisplayName" || 
                    E.ID == "CanBeHostileTo" || E.ID == "ThreatCalculation" || E.ID == "GetAttitudeToward")
                {
                    XRL.Core.XRLCore.Log($"[ImplantWedgeTrader DEBUG] {ParentObject.DisplayName}: Event {E.ID}");
                    
                    // Log faction info
                    var faction = ParentObject.GetPart("Faction");
                    if (faction != null)
                    {
                        XRL.Core.XRLCore.Log($"[ImplantWedgeTrader DEBUG] Faction: {ParentObject.GetStringProperty("Faction", "None")}");
                    }
                    
                    // Log brain info
                    var brain = ParentObject.GetPart<Brain>();
                    if (brain != null)
                    {
                        XRL.Core.XRLCore.Log($"[ImplantWedgeTrader DEBUG] Brain Hostile: {brain.Allegiance.Hostile}");
                    }
                    
                    // Log reputation
                    if (The.Player != null)
                    {
                        var rep = ParentObject.GetIntProperty("Reputation_Player", 0);
                        XRL.Core.XRLCore.Log($"[ImplantWedgeTrader DEBUG] Reputation_Player: {rep}");
                    }
                }
            }
            catch (Exception ex)
            {
                XRL.Core.XRLCore.Log($"[ImplantWedgeTrader DEBUG] Logger error: {ex.Message}");
            }
            
            return base.FireEvent(E);
        }
    }
}
