using Server;
using Server.Items;
using System.Xml.Linq;
using Ultima;

namespace Ultimon.AntiBot
{
    public class AntibotCoin : Gold
    {
        [Constructable]
        public AntibotCoin() : base(1) 
        {
            Name = "Antibot Coin";
            Hue = 1151;
            Movable = true;
            LootType = LootType.Regular;
            Weight = 0.0; 
        }

        [Constructable]
        public AntibotCoin(int amount) : base(amount)
        {
            Name = "Antibot Coin";
            Hue = 1151;
            Movable = true;
            LootType = LootType.Regular;
            Weight = 0.0;
        }

        public AntibotCoin(Serial serial) : base(serial) { }

        public override bool IsVirtualItem => true; 

        public override void OnDoubleClick(Mobile from)
        {
            from.SendMessage(38, "This is not real currency. It can only be split for verification.");
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
        }
    }
}
