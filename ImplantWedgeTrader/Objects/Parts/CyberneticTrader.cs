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
                E.GetParameter<List<string>>("Options").Add("Appraise Items");
                E.GetParameter<List<string>>("Options").Add("Debug Inventory"); // Add debug option
            }
            else if (E.ID == "PerformInteraction" && E.GetParameter<string>("Option") == "Trade Implants")
            {
                DoTrade();
                return true;
            }
            else if (E.ID == "PerformInteraction" && E.GetParameter<string>("Option") == "Appraise Items")
            {
                AppraiseItems();
                return true;
            }
            else if (E.ID == "PerformInteraction" && E.GetParameter<string>("Option") == "Debug Inventory")
            {
                DebugInventory();
                return true;
            }
            else if (E.ID == "ConversationAction" && E.GetStringParameter("Action") == "DoTrade")
            {
                DoTrade();
                return true;
            }
            else if (E.ID == "ConversationAction" && E.GetStringParameter("Action") == "AppraiseItems")
            {
                AppraiseItems();
                return true;
            }
            return base.FireEvent(E);
        }

        private void DebugInventory()
        {
            var playerBody = XRLCore.Core.Game.Player.Body;
            var allItems = playerBody.Inventory.GetObjects();
            
            MessageQueue.AddPlayerMessage("=== FULL INVENTORY DEBUG ===");
            MessageQueue.AddPlayerMessage($"Total items: {allItems.Count}");
            
            foreach (var item in allItems.Take(10)) // Show first 10 items
            {
                MessageQueue.AddPlayerMessage($"--- ITEM: {item.DisplayName} ---");
                MessageQueue.AddPlayerMessage($"Blueprint: {item.Blueprint}");
                
                // Show all parts
                var parts = item.PartsList;
                MessageQueue.AddPlayerMessage($"Parts ({parts.Count}): {string.Join(", ", parts.Select(p => p.Name))}");
                
                // Show all tags - using HasTag method since GetTags doesn't exist
                var commonTags = new[] { "Cybernetics", "Implant", "Bionic", "Artifact", "Rare", "Unique" };
                var itemTags = commonTags.Where(tag => item.HasTag(tag)).ToList();
                MessageQueue.AddPlayerMessage($"Tags: {string.Join(", ", itemTags)}");
                
                // Show key properties
                var category = item.GetStringProperty("Category");
                var bodyPartType = item.GetStringProperty("BodyPartType");
                var tier = item.GetIntProperty("Tier", -1);
                
                MessageQueue.AddPlayerMessage($"Category: {category}, BodyPartType: {bodyPartType}, Tier: {tier}");
                
                // Test detection
                bool detected = IsCyberneticImplant(item);
                MessageQueue.AddPlayerMessage($"Cybernetic Detection: {detected}");
            }
            
            MessageQueue.AddPlayerMessage("=== END DEBUG ===");
        }

        private void AppraiseItems()
        {
            var playerBody = XRLCore.Core.Game.Player.Body;
            var allItems = playerBody.Inventory.GetObjects();
            
            MessageQueue.AddPlayerMessage("=== ITEM APPRAISAL ===");
            
            var valuableItems = allItems.Where(item => CalculateItemValue(item) > 0).ToList();
            
            if (valuableItems.Count == 0)
            {
                MessageQueue.AddPlayerMessage("I don't see any items I'd be interested in trading for.");
                return;
            }
            
            MessageQueue.AddPlayerMessage($"I can see {valuableItems.Count} items worth trading:");
            
            foreach (var item in valuableItems.Take(10))
            {
                int value = CalculateItemValue(item);
                string valueText = value > 1 ? $"{value} credit wedges" : "1 credit wedge";
                MessageQueue.AddPlayerMessage($"• {item.DisplayName} - worth {valueText}");
            }
            
            if (valuableItems.Count > 10)
            {
                MessageQueue.AddPlayerMessage($"... and {valuableItems.Count - 10} more items.");
            }
        }

        private void DoTrade()
        {
            var playerBody = XRLCore.Core.Game.Player.Body;
            var allItems = playerBody.Inventory.GetObjects();
            
            MessageQueue.AddPlayerMessage("=== CYBERNETIC TRADER ===");
            
            // Filter to items we're interested in
            var tradeableItems = allItems.Where(item => 
                CalculateItemValue(item) > 0 && 
                !RedeemedImplants.Contains(item.Blueprint)).ToList();
            
            if (tradeableItems.Count == 0)
            {
                MessageQueue.AddPlayerMessage("You have no items I'm interested in, or you've already traded them all.");
                return;
            }
            
            // Create a list of choices for the player
            var choices = new List<string>();
            for (int i = 0; i < tradeableItems.Count; i++)
            {
                var item = tradeableItems[i];
                int value = CalculateItemValue(item);
                string valueText = value > 1 ? $"{value} wedges" : "1 wedge";
                choices.Add($"{item.DisplayName} ({valueText})");
            }
            choices.Add("Cancel");
            
            // Let player choose (building the choice string manually)
            string choiceString = "";
            for (int i = 0; i < choices.Count; i++)
            {
                choiceString += $"&{i + 1}){choices[i]}\n";
            }
            
            int choice = Popup.PickOption("Choose an item to trade:", choiceString);
            
            if (choice < 0 || choice >= tradeableItems.Count)
            {
                MessageQueue.AddPlayerMessage("Trade cancelled.");
                return;
            }
            
            var selectedItem = tradeableItems[choice];
            
            // Skip items that were already traded
            if (RedeemedImplants.Contains(selectedItem.Blueprint))
            {
                MessageQueue.AddPlayerMessage($"You have already traded {selectedItem.DisplayName}.");
                return;
            }
            
            // Calculate potential value for the selected item
            int tradeValue = CalculateItemValue(selectedItem);
            
            if (tradeValue <= 0)
            {
                MessageQueue.AddPlayerMessage($"I'm not interested in {selectedItem.DisplayName}. I only trade for cybernetic implants and valuable equipment.");
                return;
            }
            
            // Confirm the trade using XRL.UI.Popup
            if (Popup.ShowYesNo($"Trade {selectedItem.DisplayName} for {tradeValue} credit wedge{(tradeValue > 1 ? "s" : "")}?") == DialogResult.Yes)
            {
                AwardChips(tradeValue);
                RedeemedImplants.Add(selectedItem.Blueprint);
                selectedItem.Destroy();
                MessageQueue.AddPlayerMessage($"You receive {tradeValue} credit wedge{(tradeValue > 1 ? "s" : "")} for {selectedItem.DisplayName}.");
            }
            else
            {
                MessageQueue.AddPlayerMessage("Trade cancelled.");
            }
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