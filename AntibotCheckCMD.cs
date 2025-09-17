using System;
using Server;
using Server.Commands;
using Server.Targeting;

namespace Ultimon.AntiBot
{
    public class AntibotCheckCommand
    {
        public static void Initialize()
        {
            CommandSystem.Register("AntibotCheck", AccessLevel.GameMaster, OnCommand);
        }

        private static void OnCommand(CommandEventArgs e)
        {
            e.Mobile.SendMessage("Target a player to run the antibot coin challenge.");
            e.Mobile.Target = new AntibotTarget();
        }

        private class AntibotTarget : Target
        {
            public AntibotTarget() : base(12, false, TargetFlags.None) { }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is Mobile m))
                {
                    from.SendMessage("That is not a valid target.");
                    return;
                }

                if (m.AccessLevel > AccessLevel.Player)
                {
                    from.SendMessage("You cannot run antibot checks on staff.");
                    return;
                }

                AntibotSystem.Trigger(m);
                from.SendMessage($"Antibot coin challenge started for {m.Name}.");
            }
        }
    }
}
