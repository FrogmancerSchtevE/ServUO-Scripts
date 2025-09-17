using System;
using System.Linq;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Ultimon.AntiBot
{
    public class AntibotBox : WoodenBox
    {
        public Mobile Owner { get; private set; }
        public int TargetAmount { get; private set; }
        private DateTime _expireTime;

        private int _savedBodyHue;
        private Dictionary<Item, int> _savedItemHues = new Dictionary<Item, int>();

        public static bool LeaveFailuresFrozen = true;

        private static readonly Point3D JailLocation = new Point3D(5158, 1492, 0);
        private static readonly Map JailMap = Map.Trammel;

        [Constructable]
        public AntibotBox(Mobile owner, int targetAmount)
        {
            Owner = owner;
            TargetAmount = targetAmount;

            Name = "Verification Box";
            Hue = 1151;
            Movable = false;

            DropItem(new AntibotCoin(1000));

            _expireTime = DateTime.UtcNow + TimeSpan.FromMinutes(2);

            ApplyBlazeHue(); 

            Timer.DelayCall(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), CheckProgress);
        }

        private void ApplyBlazeHue()
        {
            if (Owner == null) return;

            _savedBodyHue = Owner.Hue;
            _savedItemHues.Clear();

            foreach (var item in Owner.Items)
            {
                _savedItemHues[item] = item.Hue;
                item.Hue = 1161; 
            }

            Owner.Hue = 1161; 
            Owner.CantWalk = true; 
        }

        private void RestoreAppearance()
        {
            if (Owner == null) return;

            Owner.Hue = _savedBodyHue;

            foreach (var kv in _savedItemHues)
            {
                if (kv.Key != null && !kv.Key.Deleted)
                    kv.Key.Hue = kv.Value;
            }

            Owner.CantWalk = false; 
        }

        private void CheckProgress()
        {
            if (Deleted || Owner == null || Owner.Deleted)
                return;

            if (DateTime.UtcNow > _expireTime)
            {
                if (LeaveFailuresFrozen)
                {
                    Owner.CloseAllGumps();
                    Owner.SendGump(new AntibotFailGump());
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"!!! [ANTIBOT FAIL - TIMEOUT] {Owner.Name} ({Owner.Account}) at {Owner.Location} [{Owner.Map}] left Blaze & locked.");
                    Console.ResetColor();
                }
                else
                {
                    RestoreAppearance();
                    Owner.MoveToWorld(JailLocation, JailMap);
                    Owner.SendMessage(38, "You failed the antibot check and have been jailed.");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[ANTIBOT FAIL - AUTO JAIL] {Owner.Name} ({Owner.Account}) jailed at {JailLocation} [{JailMap}]");
                    Console.ResetColor();
                }

                Delete();
                return;
            }

            var coins = Items.OfType<AntibotCoin>().ToList();
            if (coins.Any(c => c.Amount == TargetAmount) &&
                coins.Any(c => c.Amount == 1000 - TargetAmount))
            {
                Owner.CloseAllGumps();
                RestoreAppearance();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[ANTIBOT PASS] {Owner.Name} ({Owner.Account}) at {Owner.Location} [{Owner.Map}]");
                Console.ResetColor();

                Delete();
            }
        }

        private void Cleanup()
        {
            RestoreAppearance();
            Delete();
        }

        public AntibotBox(Serial serial) : base(serial) { }

        public override void OnDelete()
        {
            base.OnDelete();

            foreach (var coin in Items.OfType<AntibotCoin>().ToList())
                coin.Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(Owner);
            writer.Write(TargetAmount);
            writer.Write(_expireTime);
            writer.Write(_savedBodyHue);

            writer.Write(_savedItemHues.Count);
            foreach (var kv in _savedItemHues)
            {
                writer.Write(kv.Key);
                writer.Write(kv.Value);
            }
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            Owner = reader.ReadMobile();
            TargetAmount = reader.ReadInt();
            _expireTime = reader.ReadDateTime();
            _savedBodyHue = reader.ReadInt();

            int count = reader.ReadInt();
            _savedItemHues = new Dictionary<Item, int>(count);
            for (int i = 0; i < count; i++)
            {
                Item it = reader.ReadItem();
                int hue = reader.ReadInt();
                if (it != null)
                    _savedItemHues[it] = hue;
            }

            Timer.DelayCall(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), CheckProgress);
        }
    }
}
