using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Items
{
    public class UmbralChain : Item
    {
        private Mobile _bound;
        private int _hitsRemaining;
        private Nyx _master;
        private Timer _failsafeTimer;

        public UmbralChain(Mobile bound, Nyx nyx) : base(5368) 
        {
            Name = "umbral chains";
            Hue = 2301;
            Movable = false;

            _bound = bound;
            _master = nyx;
            _hitsRemaining = 3;

            if (_bound != null)
            {
                _bound.Frozen = true;
                MoveToWorld(_bound.Location, _bound.Map);
            }

            _failsafeTimer = Timer.DelayCall(TimeSpan.FromSeconds(25), AutoBreak);
        }

        public UmbralChain(Serial serial) : base(serial) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (from != _bound)
                return;

            if (from.Weapon is BaseBashing) // Smash instantly with hammer/mace
            {
                BreakChains(from, true);
                return;
            }

            if (_hitsRemaining > 1) // Struggle mechanic
            {
                _hitsRemaining--;
                from.PublicOverheadMessage(MessageType.Regular, 33, false, "*You struggle against the chains...*");
            }
            else
            {
                BreakChains(from, false);
            }
        }

        public override bool OnDragLift(Mobile from) => false;

        private void AutoBreak()
        {
            if (!Deleted)
                BreakChains(_bound, false);
        }

        private void BreakChains(Mobile from, bool smashed)
        {
            if (_bound != null)
            {
                _bound.Frozen = false;

                if (smashed)
                    _bound.PublicOverheadMessage(MessageType.Regular, 33, false, "*You smash the chains apart!*");
                else
                    _bound.PublicOverheadMessage(MessageType.Regular, 33, false, "*The chains shatter!*");

                Effects.SendLocationEffect(_bound.Location, _bound.Map, 0x375A, 15, 2301);
                Effects.PlaySound(_bound.Location, _bound.Map, 0x1F4);
            }

            _master?.NotifyChainBroken(_bound);

            Delete();
        }

        public override void OnDelete()
        {
            if (_bound != null)
                _bound.Frozen = false;

            _failsafeTimer?.Stop();
            _failsafeTimer = null;

            base.OnDelete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version

            writer.Write(_bound);
            writer.Write(_hitsRemaining);
            writer.Write(_master);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            _bound = reader.ReadMobile();
            _hitsRemaining = reader.ReadInt();
            _master = reader.ReadMobile() as Nyx;

            if (_bound != null)
                _bound.Frozen = true;

            _failsafeTimer = Timer.DelayCall(TimeSpan.FromSeconds(25), AutoBreak);
        }
    }
}
