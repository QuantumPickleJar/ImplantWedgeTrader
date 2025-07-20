using System;
using XRL.World;
using XRL.World.Parts;

namespace CyberneticTraderMod
{
    [Serializable]
    public class CyberneticTraderInjector
    {
        public bool BuildZone(Zone Z)
        {
            // Try to place the cybernetic trader in the Six Day Stilt
            try
            {
                // Find a suitable cell to place the NPC
                Cell targetCell = null;

                // Look for an empty cell that's passable
                for (int x = 0; x < 80; x++)
                {
                    for (int y = 0; y < 25; y++)
                    {
                        Cell cell = Z.GetCell(x, y);
                        if (cell != null && cell.IsEmpty() && cell.IsPassable())
                        {
                            targetCell = cell;
                            break;
                        }
                    }
                    if (targetCell != null) break;
                }

                if (targetCell != null)
                {
                    // Create and place the NPC
                    GameObject npc = GameObject.Create("CyberneticWedgeTraderNPC");
                    if (npc != null)
                    {
                        targetCell.AddObject(npc);
                        return true;
                    }
                }
            }            catch (Exception ex)
            {
                // Log the error but don't crash the game
                Console.WriteLine($"CyberneticTraderInjector error: {ex.Message}");
            }

            return true; // Return true to prevent zone generation failure
        }
    }
}
