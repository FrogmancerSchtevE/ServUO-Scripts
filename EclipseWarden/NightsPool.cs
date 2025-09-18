// NightsPool.cs
// A hazardous lava-like pool themed for Nyx encounter
// Version: 1.0
// Author: [YourName]

using Server;
using Server.Custom; // For InteractiveEffects / AnimEffect
using Server.Items;
using Server.Mobiles;
using Server.Network;
using System;

namespace Server.Items
{
    public class NightsPool : Item
    {
        private Timer _timer;
        private DateTime _expireTime;

        [Constructable]
        public NightsPool() : base(0x122A) 
        {
            Name = "a pool of molten blood";
            Hue = 2301; 
            Movable = false;

            _expireTime = DateTime.UtcNow + TimeSpan.FromSeconds(20); 
            _timer = new InternalTimer(this, TimeSpan.FromSeconds(2.0));
            _timer.Start();
        }

        public NightsPool(Serial serial) : base(serial) { }

        public override bool OnMoveOver(Mobile m)
        {
            if (m.Alive && m is PlayerMobile)
            {
                Damage(m);
            }
            return true;
        }

        private void Damage(Mobile m)
        {
            int dmg = (int)(m.Hits * 0.10); 
            if (dmg < 0) dmg = 0;

            if (dmg > 0)
            {
                m.Damage(dmg);
                m.PublicOverheadMessage(MessageType.Regular, 33, false, "The molten blood burns you!");
            }

            try
            {
                InteractiveEffects.SendEffect(AnimEffect.FlameStrike, 1153, m); 
            }
            catch
            {
                Effects.SendLocationEffect(m.Location, m.Map, 0x3709, 30, 1153, 0);
                Effects.PlaySound(m.Location, m.Map, 0x208);
            }
        }

        public override void OnDelete()
        {
            base.OnDelete();

            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }

        private class InternalTimer : Timer
        {
            private NightsPool _pool;

            public InternalTimer(NightsPool pool, TimeSpan interval) : base(interval, interval)
            {
                _pool = pool;
                Priority = TimerPriority.TwoFiftyMS;
            }

            protected override void OnTick()
            {
                if (_pool == null || _pool.Deleted || DateTime.UtcNow >= _pool._expireTime)
                {
                    _pool?.Delete();
                    Stop();
                    return;
                }

                foreach (Mobile m in _pool.GetMobilesInRange(0)) // same tile only
                {
                    if (m.Alive && m is PlayerMobile)
                        _pool.Damage(m);
                }
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
            writer.WriteDeltaTime(_expireTime);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            _expireTime = reader.ReadDeltaTime();

            _timer = new InternalTimer(this, TimeSpan.FromSeconds(2.0));
            _timer.Start();
        }
    }
}
