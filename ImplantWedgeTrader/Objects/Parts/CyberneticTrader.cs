using System;
using System.Collections.Generic;
using System.Linq;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;
using XRL.Messages;
using QudGO = XRL.World.GameObject;
using QudEvent = XRL.World.Event;

namespace CyberneticTraderMod
{
    [Serializable]
    public class CyberneticTrader : IPart
    {
        public HashSet<string> RedeemedImplants = new HashSet<string>();

        public override bool FireEvent(QudEvent E)
        {
            // Handle conversation events
            if (E.ID == "ConversationInit")
            {
                // Check what cybernetic implants the player has when conversation starts
                CheckPlayerImplants();
                return true;
            }

            return base.FireEvent(E);
        }

        private void CheckPlayerImplants()
        {
            var playerBody = XRLCore.Core.Game.Player.Body;
            var allItems = playerBody.Inventory.GetObjects();
            
            MessageQueue.AddPlayerMessage("=== CYBERNETIC ANALYSIS ===");
            
            var valuableItems = allItems.Where(item => CalculateItemValue(item) > 0).ToList();
            
            if (valuableItems.Count == 0)
            {
                MessageQueue.AddPlayerMessage("I don't see any cybernetic implants I'd be interested in trading for.");
                return;
            }
            
            MessageQueue.AddPlayerMessage($"I can see {valuableItems.Count} items worth trading:");
            
            foreach (var item in valuableItems.Take(5))
            {
                int value = CalculateItemValue(item);
                string valueText = value > 1 ? $"{value} credit wedges" : "1 credit wedge";
                MessageQueue.AddPlayerMessage($"• {item.DisplayName} - worth {valueText}");
            }
            
            if (valuableItems.Count > 5)
            {
                MessageQueue.AddPlayerMessage($"... and {valuableItems.Count - 5} more items.");
            }
        }

        public void DoTrade()
        {
            var playerBody = XRLCore.Core.Game.Player.Body;
            var allItems = playerBody.Inventory.GetObjects();
            
            MessageQueue.AddPlayerMessage("=== PROCESSING TRADE ===");
            
            // Filter to items we're interested in
            var tradeableItems = allItems.Where(item => 
                CalculateItemValue(item) > 0 && 
                !RedeemedImplants.Contains(item.Blueprint)).ToList();
            
            if (tradeableItems.Count == 0)
            {
                MessageQueue.AddPlayerMessage("You have no items I'm interested in, or you've already traded them all.");
                return;
            }
            
            // Simple trade - just trade the first available item for now
            var selectedItem = tradeableItems[0];
            int tradeValue = CalculateItemValue(selectedItem);
            
            // Process the trade
            AwardChips(tradeValue);
            RedeemedImplants.Add(selectedItem.Blueprint);
            selectedItem.Destroy();
            MessageQueue.AddPlayerMessage($"You receive {tradeValue} credit wedge{(tradeValue > 1 ? "s" : "")} for {selectedItem.DisplayName}.");
        }
        
        private int CalculateItemValue(QudGO item)
        {
            // Check if it's a cybernetic item first
            if (IsCyberneticImplant(item))
            {
                return CalculateCyberneticValue(item);
            }
            
            // Check if it's valuable equipment that might be worth trading
            if (IsValuableEquipment(item))
            {
                return CalculateEquipmentValue(item);
            }
            
            // Check if it's a rare/unique item
            if (item.HasTag("Rare") || item.HasTag("Unique") || item.HasTag("Artifact"))
            {
                return 1; // Base value for rare items
            }
            
            return 0; // Not interested in this item
        }
        
        private int CalculateCyberneticValue(QudGO item)
        {
            int complexity = 0;
            
            // Check tier property
            if (item.HasProperty("Tier"))
            {
                complexity += item.GetIntProperty("Tier", 1);
            }
            
            // Check value property
            if (item.HasProperty("Value"))
            {
                int value = item.GetIntProperty("Value", 0);
                if (value > 1000) complexity += 2;
                else if (value > 500) complexity += 1;
            }
            
            // Check rarity tags
            if (item.HasTag("Rare") || item.HasTag("Unique") || item.HasTag("Artifact"))
                complexity += 2;
            
            // Check blueprint name for quality indicators
            string blueprint = item.Blueprint.ToLower();
            if (blueprint.Contains("high") || blueprint.Contains("advanced") || 
                blueprint.Contains("superior") || blueprint.Contains("master") ||
                blueprint.Contains("legendary"))
                complexity += 2;
            else if (blueprint.Contains("med") || blueprint.Contains("standard") || 
                     blueprint.Contains("improved"))
                complexity += 1;
            
            // Return 1-3 credit wedges based on complexity
            if (complexity >= 6) return 3;
            if (complexity >= 3) return 2;
            return 1;
        }
        
        private int CalculateEquipmentValue(QudGO item)
        {
            int value = 0;
            
            // Check if it's high-tier equipment
            if (item.HasProperty("Tier"))
            {
                int tier = item.GetIntProperty("Tier", 1);
                if (tier >= 6) value = 2;
                else if (tier >= 3) value = 1;
            }
            
            // Check if it's rare equipment
            if (item.HasTag("Rare") || item.HasTag("Unique"))
                value = Math.Max(value, 1);
            
            return value;
        }
        
        private bool IsValuableEquipment(QudGO item)
        {
            // Check if it's equipment that might be valuable
            if (item.HasPart("Armor") || item.HasPart("MeleeWeapon") || 
                item.HasPart("RangedWeapon") || item.HasPart("Shield"))
            {
                // Only if it's high tier or rare
                int tier = item.GetIntProperty("Tier", 1);
                return tier >= 3 || item.HasTag("Rare") || item.HasTag("Unique");
            }
            
            return false;
        }

        private bool IsCyberneticImplant(QudGO obj)
        {
            string blueprint = obj.Blueprint.ToLower();
            string displayName = obj.DisplayName.ToLower();
            
            // Check for cybernetic-related parts
            if (obj.HasPart("Cybernetics") || obj.HasPart("CyberneticsBaseItem") || 
                obj.HasPart("ModImplant") || obj.HasPart("ImplantBed") || 
                obj.HasPart("CyberneticsPart"))
                return true;
            
            // Check for cybernetic-related tags
            if (obj.HasTag("Cybernetics") || obj.HasTag("Implant") || obj.HasTag("Bionic") ||
                obj.HasTag("Prosthetic") || obj.HasTag("Augment") || obj.HasTag("Cyborg"))
                return true;
            
            // Check if it's in the "Cybernetics" category
            var category = obj.GetStringProperty("Category");
            if (category == "Cybernetics" || category == "Implants" || category == "Bionics")
                return true;
            
            // Check blueprint and display name for cybernetic keywords
            var cyberneticKeywords = new[] { 
                "implant", "cybernetic", "bionic", "prosthetic", "augment", "cyborg",
                "neural", "cortex", "optic", "servo", "actuator", "interface",
                "enhancement", "modification", "upgrade", "stimulator"
            };
            
            if (cyberneticKeywords.Any(keyword => blueprint.Contains(keyword) || displayName.Contains(keyword)))
                return true;
            
            // Check for specific cybernetic equipment patterns
            var cyberneticPatterns = new[] {
                "night vision", "thermal vision", "telescopic", "penetrating",
                "ontological anchor", "ninefold boot", "equipment rack",
                "cybernetic heart", "cybernetic spine", "cybernetic brain",
                "dermal insulation", "artificial muscle", "mechanical wing",
                "bionic limb", "neural interface", "cranial", "spinal",
                "optical", "auditory", "sensory", "motor", "reflex"
            };
            
            foreach (var pattern in cyberneticPatterns)
            {
                if (blueprint.Contains(pattern) || displayName.Contains(pattern))
                    return true;
            }
            
            // Check part names for cybernetic-related terms
            foreach (var part in obj.PartsList)
            {
                string partName = part.Name.ToLower();
                if (cyberneticKeywords.Any(keyword => partName.Contains(keyword)))
                    return true;
            }
            
            // Check if it's wearable equipment that might be cybernetic
            string wornOn = obj.GetStringProperty("WornOn");
            if (!string.IsNullOrEmpty(wornOn))
            {
                var wornOnLower = wornOn.ToLower();
                if (wornOnLower.Contains("cybernetic") || wornOnLower.Contains("implant") ||
                    wornOnLower.Contains("bionic") || wornOnLower.Contains("prosthetic"))
                    return true;
            }
            
            // Check if it has license points (usually indicates cybernetic)
            if (obj.HasProperty("LicensePoints") && obj.GetIntProperty("LicensePoints") > 0)
                return true;
            
            return false;
        }

        private void AwardChips(int chips)
        {
            try
            {
                for (int i = 0; i < chips; i++)
                {
                    var wedge = GameObjectFactory.Factory.CreateObject("CreditWedge1");
                    if (wedge != null)
                    {
                        XRLCore.Core.Game.Player.Body.TakeObject(wedge);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageQueue.AddPlayerMessage($"Error creating credit wedges: {ex.Message}");
            }
        }
    }
}