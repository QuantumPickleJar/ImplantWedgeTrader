using System;
using System.Collections.Generic;
using System.Linq;
using XRL.World;
using XRL.World.Conversations.Parts;
using XRL.Messages;
using XRL.UI;

namespace XRL.World.Conversations.Parts
{
    [Serializable]
    public class CyberneticTradeHandler : IConversationPart
    {
        private static readonly Dictionary<string, int> ImplantValues = new Dictionary<string, int>
        {
            // Basic implants (1 credit wedge)
            { "Bionic Heart", 1 },
            { "Optical Technician", 1 },
            { "Optical Multiscanner", 1 },
            { "Nocturnal Apex", 1 },
            { "Hyper-Elastic Ankle", 1 },
            { "Parabolic Muscular Subroutine", 1 },
            { "Pentaceps", 1 },
            { "Carbide Chef", 1 },
            { "Inflatable Axons", 1 },
            
            // Advanced implants (2 credit wedges)
            { "Bionic Liver", 2 },
            { "Bionic Lungs", 2 },
            { "Translucent Skin", 2 },
            { "Dermal Insulation", 2 },
            { "Metamorphic Polygel", 2 },
            { "Artificial Heart", 2 },
            { "Stabilizer Arm Locks", 2 },
            { "Motorized Treads", 2 },
            { "Spring-Loaded Feet", 2 },
            
            // Sophisticated implants (3 credit wedges)
            { "Bionic Limbs", 3 },
            { "Cybernetic Cranium", 3 },
            { "Night Vision", 3 },
            { "Heightened Hearing", 3 },
            { "Heightened Smell", 3 },
            { "Heightened Taste", 3 },
            { "Heightened Touch", 3 },
            { "Electromagnetic Pulse", 3 },
            { "Electrical Generation", 3 },
            { "Magnetic Pulse", 3 }
        };

        public override bool WantEvent(int ID, int propagation)
        {
            return ID == EnterElementEvent.ID || base.WantEvent(ID, propagation);
        }

        public override bool HandleEvent(EnterElementEvent E)
        {
            GameObject player = The.Player;
            if (player == null) return base.HandleEvent(E);

            // Find all cybernetic implants on the player
            List<GameObject> implants = new List<GameObject>();
            
            // Check body parts for cybernetic implants
            if (player.Body != null)
            {
                foreach (var bodyPart in player.Body.GetParts())
                {
                    if (bodyPart.Equipped != null && IsCyberneticImplant(bodyPart.Equipped))
                    {
                        implants.Add(bodyPart.Equipped);
                    }
                }
            }

            // Check inventory for cybernetic implants
            if (player.Inventory != null)
            {
                foreach (var item in player.Inventory.GetObjects())
                {
                    if (IsCyberneticImplant(item))
                    {
                        implants.Add(item);
                    }
                }
            }

            if (implants.Count == 0)
            {
                MessageQueue.AddPlayerMessage("The trader examines you carefully. \"I'm afraid I don't see any cybernetic implants that I can work with.\"");
                return base.HandleEvent(E);
            }

            // Let player choose which implant to trade
            List<string> options = new List<string>();
            List<string> descriptions = new List<string>();
            
            foreach (var implant in implants)
            {
                string name = implant.DisplayName;
                int value = GetImplantValue(implant);
                options.Add($"{name} (worth {value} credit wedge{(value > 1 ? "s" : "")})");
                descriptions.Add(implant.GetDisplayName());
            }

            options.Add("Cancel");
            descriptions.Add("Don't trade anything.");

            // Build choice string with hotkeys
            string choiceString = "";
            for (int i = 0; i < options.Count; i++)
            {
                choiceString += $"&{i + 1}){options[i]}\n";
            }

            int choice = Popup.PickOption(
                "Select implant to trade:",
                choiceString
            );

            if (choice == -1 || choice == options.Count - 1)
            {
                MessageQueue.AddPlayerMessage("You decide not to trade anything.");
                return base.HandleEvent(E);
            }

            // Process the trade
            GameObject selectedImplant = implants[choice];
            int creditWedges = GetImplantValue(selectedImplant);

            // Remove the implant from player
            if (selectedImplant.Equipped != null)
            {
                // It's equipped, need to unequip it first
                selectedImplant.Equipped.ForceUnequip();
            }
            
            selectedImplant.RemoveFromContext();

            // Give credit wedges
            for (int i = 0; i < creditWedges; i++)
            {
                GameObject wedge = GameObjectFactory.Factory.CreateObject("CreditWedge1");
                if (wedge != null)
                {
                    player.TakeObject(wedge);
                }
            }

            MessageQueue.AddPlayerMessage($"The trader carefully removes your {selectedImplant.DisplayName} and hands you {creditWedges} credit wedge{(creditWedges > 1 ? "s" : "")}.");
            MessageQueue.AddPlayerMessage("\"A fair trade. Your cybernetic asset has been properly liquidated.\"");

            return base.HandleEvent(E);
        }

        private bool IsCyberneticImplant(GameObject obj)
        {
            if (obj == null) return false;
            
            // Check for cybernetic tags or parts
            if (obj.HasTag("Cybernetic") || obj.HasTag("Implant") || obj.HasTag("Biotech"))
                return true;
                
            // Check if it's a known cybernetic implant
            string name = obj.DisplayName;
            if (ImplantValues.ContainsKey(name))
                return true;

            // Check for cybernetic-related parts
            if (obj.HasPart("Cybernetics") || obj.HasPart("Implant") || obj.HasPart("Biotech"))
                return true;

            return false;
        }

        private int GetImplantValue(GameObject implant)
        {
            if (implant == null) return 0;
            
            string name = implant.DisplayName;
            if (ImplantValues.ContainsKey(name))
                return ImplantValues[name];

            // Default value based on complexity or rarity
            if (implant.HasTag("Rare") || implant.HasTag("Complex"))
                return 3;
            else if (implant.HasTag("Advanced"))
                return 2;
            else
                return 1;
        }
    }
}
