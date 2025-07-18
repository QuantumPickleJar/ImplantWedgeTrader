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
            
            // Debug: List all items in inventory
            var allItems = playerBody.Inventory.GetObjects();
            MessageQueue.AddPlayerMessage($"Debug: You have {allItems.Count} total items in inventory.");
            
            foreach (var item in allItems)
            {
                bool hasCyberneticsBase = item.HasPart("CyberneticsBaseItem");
                bool hasCyberneticsTag = item.HasTag("CyberneticImplant");
                bool hasBodyPart = item.HasPart("BodyPart");
                
                if (hasCyberneticsBase || hasCyberneticsTag || hasBodyPart || item.Blueprint.ToLower().Contains("cyber"))
                {
                    MessageQueue.AddPlayerMessage($"Debug: {item.DisplayName} (blueprint: {item.Blueprint}) - CyberneticsBase: {hasCyberneticsBase}, CyberneticTag: {hasCyberneticsTag}, BodyPart: {hasBodyPart}");
                }
            }
            
            var implants = playerBody.Inventory.GetObjects()
                .Where(o => (o.HasPart("CyberneticsBaseItem") || o.HasTag("CyberneticImplant") || o.HasPart("BodyPart")) && !RedeemedImplants.Contains(o.Blueprint))
                .ToList();

            if (implants.Count == 0)
            {
                MessageQueue.AddPlayerMessage("You have no cybernetic implants available for trade. I need cybernetic implants or body parts to convert.");
                return;
            }

            // Show selection UI if there are multiple implants
            QudGO chosen;
            if (implants.Count == 1)
            {
                chosen = implants[0];
            }
            else
            {
                // Use the picker to let player choose which implant to trade
                List<QudGO> options = new List<QudGO>(implants);
                int choice = Popup.ShowOptionList("Choose an implant to trade:", 
                    options.Select(o => o.DisplayName + " (" + DetermineTier(o) + " tier)").ToArray());
                
                if (choice < 0 || choice >= options.Count)
                {
                    MessageQueue.AddPlayerMessage("Trade cancelled.");
                    return;
                }
                chosen = options[choice];
            }

            string tier = DetermineTier(chosen);
            int chips = TierValues[tier];

            AwardChips(chips);
            RedeemedImplants.Add(chosen.Blueprint);
            chosen.Destroy();
            MessageQueue.AddPlayerMessage($"You trade your {chosen.DisplayName} for {chips} credit wedge{(chips > 1 ? "s" : "")}.");
        }

        private string DetermineTier(QudGO implant)
        {
            if (!implant.HasPart("CyberneticsBaseItem") && !implant.HasTag("CyberneticImplant") && !implant.HasPart("BodyPart"))
                return "Low";

            int complexity = 0;
            
            // Check license point cost (primary indicator)
            if (implant.HasProperty("LicensePoints"))
            {
                int licensePoints = implant.GetIntProperty("LicensePoints", 0);
                complexity += licensePoints;
            }
            
            // Check tier property
            if (implant.HasProperty("Tier"))
            {
                complexity += implant.GetIntProperty("Tier", 1) * 2;
            }
            
            // Check value as secondary indicator
            if (implant.HasProperty("Value"))
            {
                int value = implant.GetIntProperty("Value", 0);
                if (value > 2000) complexity += 3;
                else if (value > 1000) complexity += 2;
                else if (value > 500) complexity += 1;
            }
            
            // Check for special tags
            if (implant.HasTag("Rare") || implant.HasTag("Unique") || implant.HasTag("Artifact"))
                complexity += 3;
            
            // Blueprint-based complexity assessment
            string blueprint = implant.Blueprint.ToLower();
            if (blueprint.Contains("high") || blueprint.Contains("advanced") || blueprint.Contains("superior") || 
                blueprint.Contains("mk iii") || blueprint.Contains("mkiii"))
                complexity += 2;
            else if (blueprint.Contains("med") || blueprint.Contains("standard") || 
                     blueprint.Contains("mk ii") || blueprint.Contains("mkii"))
                complexity += 1;

            // Determine tier based on total complexity
            if (complexity >= 8) return "High";
            if (complexity >= 4) return "Mid";
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