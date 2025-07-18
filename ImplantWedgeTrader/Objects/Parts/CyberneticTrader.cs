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
                E.GetParameter<List<string>>("Options").Add("Debug Inventory"); // Add debug option
            }
            else if (E.ID == "PerformInteraction" && E.GetParameter<string>("Option") == "Trade Implants")
            {
                DoTrade();
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
                
                // Show all tags
                var tags = item.GetTags();
                MessageQueue.AddPlayerMessage($"Tags ({tags.Count}): {string.Join(", ", tags)}");
                
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

        private void DoTrade()
        {
            var playerBody = XRLCore.Core.Game.Player.Body;
            
            // Debug: Check all items in inventory
            var allItems = playerBody.Inventory.GetObjects();
            MessageQueue.AddPlayerMessage($"Debug: Checking {allItems.Count} items in inventory...");
            
            var implants = allItems
                .Where(o => IsCyberneticImplant(o) && !RedeemedImplants.Contains(o.Blueprint))
                .ToList();

            // If no implants found through primary detection, try secondary methods
            if (implants.Count == 0)
            {
                MessageQueue.AddPlayerMessage("Debug: Primary detection failed, trying secondary methods...");
                
                // Look for items that are equippable and might be cybernetics
                var equipableItems = allItems.Where(o => 
                    o.HasPart("Armor") || o.HasPart("MeleeWeapon") || o.HasPart("Shield") || 
                    o.HasPart("ModImplant") || o.HasPart("Equipment") || o.HasPart("Physics")).ToList();
                
                MessageQueue.AddPlayerMessage($"Debug: Found {equipableItems.Count} equipable items to check");
                
                foreach (var item in equipableItems)
                {
                    // Check if it might be cybernetic equipment
                    string itemName = item.DisplayName.ToLower();
                    string itemBlueprint = item.Blueprint.ToLower();
                    
                    if ((itemName.Contains("implant") || itemName.Contains("cybernetic") || 
                         itemBlueprint.Contains("implant") || itemBlueprint.Contains("cybernetic") ||
                         itemName.Contains("bionic") || itemBlueprint.Contains("bionic")) &&
                        !RedeemedImplants.Contains(item.Blueprint))
                    {
                        MessageQueue.AddPlayerMessage($"Debug: Secondary detection found: {item.DisplayName}");
                        implants.Add(item);
                    }
                }
            }

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
                string currentTier = DetermineTier(implants[i]);
                int currentChips = Math.Min(TierValues[currentTier], 3);
                choices.Add($"{implants[i].DisplayName} (worth {currentChips} credit wedge{(currentChips > 1 ? "s" : "")})");
                string implantTier = DetermineTier(implants[i]);
                int implantChips = Math.Min(TierValues[implantTier], 3);
                choices.Add($"{implants[i].DisplayName} (worth {implantChips} credit wedge{(implantChips > 1 ? "s" : "")})");
            }
            choices.Add("Cancel");

            int choice = Popup.PickOption("Choose an implant to trade:", choices.ToArray());
            int choice = Popup.PickOption("Choose an implant to trade:", Options: choices.ToArray());
            
            if (choice < 0 || choice >= implants.Count)
            {
                return; // Cancelled
            }

            var chosen = implants[choice];
            string finalTier = DetermineTier(chosen);
            int finalChips = Math.Min(TierValues[finalTier], 3);
            string chosenTier = DetermineTier(chosen);
            int chosenChips = Math.Min(TierValues[chosenTier], 3);

            AwardChips(finalChips);
            AwardChips(chosenChips);
            RedeemedImplants.Add(chosen.Blueprint);
            chosen.Destroy();
            MessageQueue.AddPlayerMessage($"You receive {finalChips} credit wedge{(finalChips > 1 ? "s" : "")}.");
            MessageQueue.AddPlayerMessage($"You receive {chosenChips} credit wedge{(chosenChips > 1 ? "s" : "")}.");
        }

        private bool IsCyberneticImplant(QudGO obj)
        {
            // Quick check for known cybernetic blueprint patterns
            string blueprint = obj.Blueprint.ToLower();
            string displayName = obj.DisplayName.ToLower();
            
            // Known cybernetic items from research
            var knownCyberneticPatterns = new[]
            {
                "night", "vision", "goggle", "optical", "neural", "muscular", "respiratory",
                "cardiovascular", "dermal", "skeletal", "metabolic", "implant", "cybernetic",
                "bionic", "prosthetic", "anchor", "ontological", "ninefold", "boot",
                "equipment", "rack", "stasis", "entangler", "polyphase", "modulator",
                "precision", "force", "lathe"
            };
            
            // Quick pattern check first
            bool hasPattern = knownCyberneticPatterns.Any(pattern => 
                blueprint.Contains(pattern) || displayName.Contains(pattern));
            
            if (!hasPattern)
            {
                return false; // Skip detailed checks if no patterns match
            }
            
            // Now do detailed checks
            MessageQueue.AddPlayerMessage($"Debug: Detailed analysis of {obj.DisplayName} - passed pattern check");
            
            // Check for common cybernetic implant parts
            var parts = obj.PartsList;
            
            if (obj.HasPart("Cybernetics") || obj.HasPart("CyberneticsBaseItem") || obj.HasPart("ModImplant"))
            {
                MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} detected via part check");
                return true;
            }
            
            // Check blueprint name patterns
            if (blueprint.Contains("implant") || blueprint.Contains("cybernetic") || 
                blueprint.Contains("bionic") || blueprint.Contains("prosthetic"))
            {
                MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} detected via blueprint pattern");
                return true;
            }
            
            // Check tags
            if (obj.HasTag("Cybernetics") || obj.HasTag("Implant") || obj.HasTag("Bionic"))
            {
                MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} detected via tag check");
                return true;
            }
            
            // Check if it's in the "Cybernetics" category
            string category = obj.GetStringProperty("Category");
            
            if (category == "Cybernetics")
            {
                MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} detected via category");
                return true;
            }
            
            // Check display name for cybernetic keywords
            if (displayName.Contains("implant") || displayName.Contains("cybernetic") || 
                displayName.Contains("bionic") || displayName.Contains("prosthetic"))
            {
                MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} detected via display name");
                return true;
            }
            
            // Check for common Caves of Qud cybernetic equipment patterns
            if (blueprint.Contains("night") && blueprint.Contains("vision") ||
                blueprint.Contains("ontological") && blueprint.Contains("anchor") ||
                blueprint.Contains("ninefold") && blueprint.Contains("boot") ||
                blueprint.Contains("equipment") && blueprint.Contains("rack"))
            {
                MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} detected via specific equipment pattern");
            if (obj.GetStringProperty("Category") == "Cybernetics")
                return true;
            }
            
            // Check for common Caves of Qud cybernetic parts by looking at part names
            foreach (var part in parts)
            {
                string partName = part.Name.ToLower();
                if (partName.Contains("cybernetic") || partName.Contains("implant") || 
                    partName.Contains("bionic") || partName.Contains("augment"))
                {
                    MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} detected via part name: {part.Name}");
                    return true;
                }
            }
            
            // Final check: see if the item is equipable in a cybernetic slot
            if (obj.HasProperty("BodyPartType"))
            {
                string bodyPartType = obj.GetStringProperty("BodyPartType");
                if (bodyPartType.ToLower().Contains("cybernetic"))
                {
                    MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} detected via body part type");
                    return true;
                }
            }
            
            // Check if it's wearable equipment that might be cybernetic
            if (obj.HasPart("Armor") && hasPattern)
            {
                MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} detected as cybernetic armor");
                return true;
            }
            
            MessageQueue.AddPlayerMessage($"Debug: {obj.DisplayName} failed all detection checks");
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