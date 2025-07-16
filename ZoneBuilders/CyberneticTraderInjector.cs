using System;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace CyberneticTraderMod
{
    [Serializable]
    public class CyberneticTraderInjector : ZoneBuilder
    {
        public override void BuildZone(Zone Z)
        {
            if (!Z.ZoneID.Contains("sixdaystilt"))
                return;

            // Avoid duplicate placement
            if (Z.FindObject(o => o.Blueprint == "CyberneticWedgeTraderNPC") != null)
                return;

            GameObject trader = GameObjectFactory.Factory.CreateObject("CyberneticWedgeTraderNPC");
            Cell cell = Z.GetCell(65, 17) ?? Z.GetFirstEmptyCell();
            cell?.AddObject(trader);
        }
    }
}