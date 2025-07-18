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
            
            // Debug: Check all items in inventory
            var allItems = playerBody.Inventory.GetObjects();
            MessageQueue.AddPlayerMessage($"Debug: Checking {allItems.Count} items in inventory...");
            
            var implants = allItems
                .Where(o => IsCyberneticImplant(o) && !RedeemedImplants.Contains(o.Blueprint))
                .ToList();

            // Debug: Show what was found
            MessageQueue.AddPlayerMessage($"Debug: Found {implants.Count} tradeable implants.");
            foreach (var implant in implants.Take(3)) // Show first 3 for debugging
            {
                MessageQueue.AddPlayerMessage($"Debug: Found {implant.DisplayName} ({implant.Blueprint})");
            }

            if (implants.Count == 0)
            {
                // Try to find any item that might be an implant for debugging
                var possibleImplants = allItems.Where(o => 
                    o.Blueprint.ToLower().Contains("implant") || 
                    o.DisplayName.ToLower().Contains("implant") ||
                    o.Blueprint.ToLower().Contains("cybernetic") ||
                    o.DisplayName.ToLower().Contains("cybernetic")).ToList();
                
                if (possibleImplants.Count > 0)
                {
                    MessageQueue.AddPlayerMessage($"Debug: Found {possibleImplants.Count} items with 'implant' or 'cybernetic' in name, but they didn't pass detection.");
                    foreach (var item in possibleImplants.Take(3))
                    {
                        MessageQueue.AddPlayerMessage($"Debug: Item {item.DisplayName} ({item.Blueprint}) - checking why it failed...");
                    }
                }
                
                MessageQueue.AddPlayerMessage("You have no acceptable implants for trade.");
                return;
            }

            // Let player choose which implant to trade
            List<string> choices = new List<string>();
            for (int i = 0; i < implants.Count; i++)
            {
                string implantTier = DetermineTier(implants[i]);
                int implantChips = Math.Min(TierValues[implantTier], 3);
                choices.Add($"{implants[i].DisplayName} (worth {implantChips} credit wedge{(implantChips > 1 ? "s" : "")})");
            }
            choices.Add("Cancel");

            int choice = Popup.PickOption("Choose an implant to trade:", Options: choices.ToArray());
            
            if (choice < 0 || choice >= implants.Count)
            {
                return; // Cancelled
            }

            var chosen = implants[choice];
            string chosenTier = DetermineTier(chosen);
            int chosenChips = Math.Min(TierValues[chosenTier], 3);

            AwardChips(chosenChips);
            RedeemedImplants.Add(chosen.Blueprint);
            chosen.Destroy();
            MessageQueue.AddPlayerMessage($"You receive {chosenChips} credit wedge{(chosenChips > 1 ? "s" : "")}.");
        }

        private bool IsCyberneticImplant(QudGO obj)
        {
            // Check for common cybernetic implant characteristics
            if (obj.HasPart("Cybernetics") || obj.HasPart("CyberneticsBaseItem") || obj.HasPart("ModImplant"))
                return true;
            
            // Check blueprint name patterns
            string blueprint = obj.Blueprint.ToLower();
            if (blueprint.Contains("implant") || blueprint.Contains("cybernetic") || 
                blueprint.Contains("bionic") || blueprint.Contains("prosthetic"))
                return true;
            
            // Check tags
            if (obj.HasTag("Cybernetics") || obj.HasTag("Implant") || obj.HasTag("Bionic"))
                return true;
            
            // Check if it's in the "Cybernetics" category
            if (obj.GetStringProperty("Category") == "Cybernetics")
                return true;
            
            // Additional checks for common implant names
            if (blueprint.Contains("optical") || blueprint.Contains("neural") ||
                blueprint.Contains("muscular") || blueprint.Contains("respiratory") ||
                blueprint.Contains("cardiovascular") || blueprint.Contains("dermal") ||
                blueprint.Contains("skeletal") || blueprint.Contains("metabolic"))
                return true;
            
            return false;
        }

        private string DetermineTier(QudGO implant)
        {
            if (!IsCyberneticImplant(implant))
                return "Low";

            int complexity = 0;
            
            // Check tier property
            if (implant.HasProperty("Tier"))
            {
                complexity += implant.GetIntProperty("Tier", 1);
            }
            
            // Check license points
            if (implant.HasProperty("LicensePoints"))
            {
                complexity += implant.GetIntProperty("LicensePoints", 0) / 2;
            }
            
            // Check value
            if (implant.HasProperty("Value"))
            {
                int value = implant.GetIntProperty("Value", 0);
                if (value > 1000) complexity += 2;
                else if (value > 500) complexity += 1;
            }
            
            // Check rarity tags
            if (implant.HasTag("Rare") || implant.HasTag("Unique") || implant.HasTag("Artifact"))
                complexity += 2;
            
            // Check blueprint name for quality indicators
            string blueprint = implant.Blueprint.ToLower();
            if (blueprint.Contains("high") || blueprint.Contains("advanced") || 
                blueprint.Contains("superior") || blueprint.Contains("master") ||
                blueprint.Contains("legendary"))
                complexity += 2;
            else if (blueprint.Contains("med") || blueprint.Contains("standard") || 
                     blueprint.Contains("improved"))
                complexity += 1;

            // Check for special implant types that should be higher tier
            if (blueprint.Contains("night") || blueprint.Contains("thermal") ||
                blueprint.Contains("telescopic") || blueprint.Contains("penetrating"))
                complexity += 1;

            if (complexity >= 6) return "High";
            if (complexity >= 3) return "Mid";
            return "Low";
        }

        private void AwardChips(int chips)
        {
            for (int i = 0; i < chips; i++)
            {
                var wedge = GameObjectFactory.Factory.CreateObject("CreditWedge1");
                XRLCore.Core.Game.Player.Body.TakeObject(wedge);
            }
        }
    }
}