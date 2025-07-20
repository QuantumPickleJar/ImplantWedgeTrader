using System;
using System.Collections.Generic;
using System.Linq;
using XRL.World;
using XRL.World.Parts;
using XRL.Messages;
using XRL.UI;

namespace XRL.World.Parts
{
    [Serializable]
    public class CyberneticTrader : IPart
    {
        public override bool SameAs(IPart p)
        {
            return false;
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == GetShortDescriptionEvent.ID
                || ID == GetDisplayNameEvent.ID
                || ID == InventoryActionEvent.ID;
        }

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            E.Postfix.AppendRules("A cybernetic implant trader who deals in credit wedges. You can trade implants for credits with this merchant.");
            return true;
        }

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (E.Understood() && !E.Object.HasProperName)
            {
                E.AddAdjective("&Ccybernetic&y");
            }
            return true;
        }

        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (E.Command == "Trade Implants")
            {
                var player = The.Player;
                if (player != null)
                {
                    ShowTradeMenu(player);
                }
                return true;
            }
            return base.HandleEvent(E);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "CommandTrade")
            {
                var player = The.Player;
                if (player != null)
                {
                    ShowTradeMenu(player);
                }
                return false;
            }
            return base.FireEvent(E);
        }

        private List<GameObject> GetPlayerImplants(GameObject player)
        {
            var implants = new List<GameObject>();
            
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
            
            return implants;
        }

        private bool IsCyberneticImplant(GameObject item)
        {
            // Check if item has Cybernetic part
            if (item.HasPart("Cybernetic"))
                return true;
            
            // Check for cybernetic-related tags
            if (item.HasTag("Cybernetic") || item.HasTag("CyberneticImplant"))
                return true;
            
            // Check blueprint names that contain cybernetic-related terms
            var blueprint = item.Blueprint;
            if (blueprint != null)
            {
                var name = blueprint.ToLower();
                if (name.Contains("cybernetic") || name.Contains("implant") || name.Contains("bionic"))
                    return true;
            }
            
            return false;
        }

        private void ShowTradeMenu(GameObject player)
        {
            var implants = GetPlayerImplants(player);
            
            if (!implants.Any())
            {
                MessageQueue.AddPlayerMessage("You don't have any cybernetic implants to trade.");
                return;
            }

            MessageQueue.AddPlayerMessage("The trader examines your implants with interest...");            // For simplicity, just trade the first implant found
            var implant = implants[0];
            var tier = DetermineImplantTier(implant);
            var wedgeCount = GetCreditWedgeCount(tier);
            
            // Remove the implant and give credit wedges
            player.Inventory.RemoveObject(implant);
            GiveCreditWedges(player, tier);
            
            MessageQueue.AddPlayerMessage($"You trade your {implant.DisplayName} for {wedgeCount} credit wedges.");
        }

        private int DetermineImplantTier(GameObject implant)
        {
            // Simple tier system based on implant properties
            var tier = 1;
            
            // Check for complexity indicators
            if (implant.HasProperty("Tier"))
            {
                tier = Math.Max(tier, implant.GetIntProperty("Tier", 1));
            }
            
            if (implant.HasProperty("LicensePoints"))
            {
                var points = implant.GetIntProperty("LicensePoints", 0);
                if (points >= 3) tier = Math.Max(tier, 3);
                else if (points >= 2) tier = Math.Max(tier, 2);
            }
            
            // Check value
            if (implant.HasPart("Commerce"))
            {
                var value = implant.GetIntProperty("Value", 0);
                if (value >= 500) tier = Math.Max(tier, 3);
                else if (value >= 200) tier = Math.Max(tier, 2);
            }
            
            return Math.Min(tier, 3); // Cap at tier 3
        }

        private int GetCreditWedgeCount(int tier)
        {
            switch (tier)
            {
                case 1: return 1;
                case 2: return 2;
                case 3: return 3;
                default: return 1;
            }
        }        private void GiveCreditWedges(GameObject player, int tier)
        {
            var count = GetCreditWedgeCount(tier);
            var wedgeType = $"CreditWedge{tier}";
            
            try
            {
                // Create multiple single wedges instead of trying to stack
                for (int i = 0; i < count; i++)
                {
                    var wedge = GameObjectFactory.Factory.CreateObject(wedgeType);
                    if (wedge != null)
                    {
                        player.Inventory.AddObject(wedge);
                    }
                    else
                    {
                        // Fallback to basic credit wedge
                        wedge = GameObjectFactory.Factory.CreateObject("CreditWedge1");
                        if (wedge != null)
                        {
                            player.Inventory.AddObject(wedge);
                        }
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