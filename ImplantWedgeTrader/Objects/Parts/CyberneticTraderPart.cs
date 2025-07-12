using System.Collections.Generic;
using XRL.Core;
using XRL.World.Parts;
using XRL.World.Parts.Mutation; // for cyber implants
using XRL.World;
using XRL.Liquids; // for wedge chips as "liquid currency"

namespace GrantCyberneticWedge
{
    public class CyberneticTrader : IPart // Define your custom Part
    {
        // Parameters:
        //   AcceptedTiers: list of implant rarity tiers (low, mid, high)
        //   MaxChipsPerTier: cap credits per implant (3 max)

        public HashSet<string> RedeemedImplants;
        public static readonly Dictionary<string, int> TierValues = new Dictionary<string,int>()
        {
            { "Low", 1 },
            { "Mid", 2 },
            { "High", 3 }
        };

        public override void Register(GameObject obj)
        {
            base.Register(obj);
            RedeemedImplants = new HashSet<string>();
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "GetInteractionOptions")
            {
                // add "Trade Implants" interaction
                E.GetParameter<List<string>>("Options").Add("Trade Implants");
            }
            else if (E.ID == "PerformInteraction")
            {
                if (E.GetParameter<string>("Option") == "Trade Implants")
                {
                    DoTrade();
                    return true;
                }
            }
            return base.FireEvent(E);
        }

        public void DoTrade()
        {
            // find implants in player inventory
            GameObject player = XRLCore.Core.Game.Player.Body;
            var implants = player.GetObjectsInInventory()
                .FindAll(o => o.HasPart<IPartMutation>() && !RedeemedImplants.Contains(o.BlueprintId));

            if (implants.Count == 0)
            {
                Popup("You have no acceptable implants for trade.");
                return;
            }

            // let player choose one implant to redeem
            GameObject chosen = ChooseImplant(implants);
            if (chosen == null) return;

            // determine tier from mutation
            var part = chosen.GetPart<IPartMutation>();
            string tier = DetermineTier(part);
            int chips = Mathf.Min(TierValues[tier], 3);

            // award wedge chips
            AwardChips(chips);

            // mark redeemed and destroy item
            RedeemedImplants.Add(chosen.BlueprintId);
            chosen.Destroy();

            Popup($"You receive {chips} credit wedge{(chips>1? "s":"")} for trading in the implant.");
        }

        protected virtual GameObject ChooseImplant(List<GameObject> implants)
        {
            // for simplicity: pick first implant
            return implants[0];
        }

        protected virtual string DetermineTier(IPartMutation part)
        {
            // basic algorithm: net stats
            int positive = part.GrantStatChanges().Count;
            if (positive < 2) return "Low";
            if (positive < 4) return "Mid";
            return "High";
        }

        protected virtual void AwardChips(int chips)
        {
            GameObject wedge = GameObject.Create("CreditWedge");
            wedge.SetStackSize(chips);
            XRLCore.Core.Game.Player.Body.Add(wedge);
        }

        protected void Popup(string text)
        {
            XRLCore.Core.Game.ShowPopup(text);
        }
    }
}
