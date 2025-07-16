using System;
using System.Collections.Generic;
using System.Linq;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;
using XRL.UI;
using XRL.Messages;
using QudGO = XRL.World.GameObject;
using QudEvent = XRL.World.Event;

namespace CyberneticTraderMod
{
    [Serializable]
    public class CyberneticTrader : IPart
    {
        public HashSet<string> RedeemedImplants = new HashSet<string>();

        public static readonly Dictionary<string, int> TierValues = new()
        {
            {"Low", 1},
            {"Mid", 2},
            {"High", 3}
        };

        public override bool FireEvent(QudEvent E)
        {
            if (E.ID == "GetInteractionOptions")
            {
                E.GetParameter<List<string>>("Options").Add("Trade Implants");
            }
            else if (E.ID == "PerformInteraction" && E.GetParameter<string>("Option") == "Trade Implants")
            {
                DoTrade();
                return true;
            }
            else if (E.ID == "ConversationAction" && E.GetStringParameter("Action") == "DoTrade")
            {
                DoTrade();
                return true;
            }
            return base.FireEvent(E);
        }

        private void DoTrade()
        {
            var playerBody = XRLCore.Core.Game.Player.Body;
            var implants = playerBody.Inventory.GetObjects()
                .Where(o => o.HasPart("Cybernetic") && !RedeemedImplants.Contains(o.Blueprint))
                .ToList();

            if (implants.Count == 0)
            {
                MessageQueue.AddPlayerMessage("You have no acceptable implants for trade.");
                return;
            }

            var chosen = implants.First();
            string tier = DetermineTier(chosen);
            int chips = Math.Min(TierValues[tier], 3);

            AwardChips(chips);
            RedeemedImplants.Add(chosen.Blueprint);
            chosen.Destroy();
            MessageQueue.AddPlayerMessage($"You receive {chips} credit wedge{(chips > 1 ? "s" : "")}.");
        }

        private string DetermineTier(QudGO implant)
        {
            if (!implant.HasPart("Cybernetic"))
                return "Low";

            int complexity = 0;
            if (implant.HasProperty("Tier"))
            {
                complexity += implant.GetIntProperty("Tier", 1);
            }
            if (implant.HasProperty("LicensePoints"))
            {
                complexity += implant.GetIntProperty("LicensePoints", 0) / 2;
            }
            if (implant.HasProperty("Value"))
            {
                int value = implant.GetIntProperty("Value", 0);
                if (value > 1000) complexity += 2;
                else if (value > 500) complexity += 1;
            }
            if (implant.HasTag("Rare") || implant.HasTag("Unique"))
                complexity += 2;

            string blueprint = implant.Blueprint;
            if (blueprint.Contains("High") || blueprint.Contains("Advanced") || blueprint.Contains("Superior"))
                complexity += 2;
            else if (blueprint.Contains("Med") || blueprint.Contains("Standard"))
                complexity += 1;

            if (complexity >= 6) return "High";
            if (complexity >= 3) return "Mid";
            return "Low";
        }

        private void AwardChips(int chips)
        {
            var wedge = GameObjectFactory.Factory.CreateObject("CreditWedge1");
            if (chips > 1)
            {
                wedge.SetIntProperty("StackSize", chips);
            }
            XRLCore.Core.Game.Player.Body.TakeObject(wedge);
        }
    }
}