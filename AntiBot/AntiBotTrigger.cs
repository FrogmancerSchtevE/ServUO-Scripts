using Server;

namespace Ultimon.AntiBot
{
    public static class AntibotSystem
    {
        public static void Trigger(Mobile m)
        {
            if (m == null || m.Deleted) return;

            int target = Utility.RandomMinMax(2, 999);

            m.CantWalk = true;
            m.Hue = 1151; 
            m.SendGump(new AntibotGump(target));

            var box = new AntibotBox(m, target);

            if (m.Backpack != null)
                m.Backpack.DropItem(box); 
            else
                box.MoveToWorld(m.Location, m.Map);
        }
    }
}
