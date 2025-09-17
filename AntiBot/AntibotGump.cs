using Server;
using Server.Gumps;

namespace Ultimon.AntiBot
{
    public class AntibotGump : Gump
    {
        private readonly int _targetAmount;

        public AntibotGump(int targetAmount) : base(50, 50)
        {
            _targetAmount = targetAmount;

            Closable = true;
            Dragable = true;
            Resizable = false;

            AddBackground(0, 0, 300, 150, 9270);
            AddLabel(30, 20, 1152, "Antibot Verification");
            AddHtml(30, 50, 240, 60,
                $"<BASEFONT COLOR=#FFFFFF>You must split the stack of 1000 Antibot Coins " +
                $"so that one pile equals <B>{_targetAmount}</B>.<br><br>" +
                $"Drag-split the coins in the verification box. " +
                $"You have 2 minutes.</BASEFONT>", true, true);
        }
    }
}
