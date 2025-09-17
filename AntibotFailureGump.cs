using Server;
using Server.Gumps;

namespace Ultimon.AntiBot
{
    public class AntibotFailGump : Gump
    {
        public AntibotFailGump() : base(50, 50)
        {
            Closable = true;
            Dragable = true;
            Resizable = false;

            AddBackground(0, 0, 350, 150, 9270);
            AddLabel(30, 20, 38, "Antibot Verification Failed");
            AddHtml(30, 50, 280, 70,
                "<BASEFONT COLOR=#FF0000>You have failed the antibot challenge.<br><br>" +
                "You are frozen and marked in blaze until a staff member reviews your case.<br><br>" +
                "Please page staff in-game or on Discord to be released.</BASEFONT>",
                true, true);
        }
    }
}
