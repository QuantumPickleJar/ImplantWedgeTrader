using System;
using XRL.World;

namespace CyberneticTraderMod
{
    [Serializable]
    public class CyberneticTraderInjector
    {
        public bool BuildZone(Zone Z)
        {
            if (!Z.ZoneID.Contains("sixdaystilt"))
                return true;

            // Avoid duplicate placement
            if (Z.FindObject(o => o.Blueprint == "CyberneticWedgeTraderNPC") != null)
                return true;

            GameObject trader = GameObjectFactory.Factory.CreateObject("CyberneticWedgeTraderNPC");
            
            // Try to place at specific coordinates, fallback to finding an empty cell
            Cell cell = Z.GetCell(65, 17);
            if (cell == null || !cell.IsEmpty())
            {
                // Find an empty cell in the zone
                for (int x = 0; x < Z.Width; x++)
                {
                    for (int y = 0; y < Z.Height; y++)
                    {
                        Cell testCell = Z.GetCell(x, y);
                        if (testCell != null && testCell.IsEmpty())
                        {
                            cell = testCell;
                            break;
                        }
                    }
                    if (cell != null && cell.IsEmpty()) break;
                }
            }
            
            if (cell != null)
                cell.AddObject(trader);

            return true;
        }
    }
}