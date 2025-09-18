using System;
using Server;
using Server.Mobiles;
using Server.Items;
using Server.Network;
using Server.Custom; 

namespace Server.Mobiles
{
    [CorpseName("the shattered remains of an eclipse orb")]
    public class EclipseOrb : BaseCreature
    {
        private int _countdown = 10;

        [Constructable]
        public EclipseOrb()
            : base(AIType.AI_Melee, FightMode.None, 10, 1, 0.2, 0.4)
        {
            Name = "an eclipse orb";
            Body = 0x3A; 
            Hue = 2301;   
            CantWalk = true;

            SetStr(100);
            SetDex(50);
            SetInt(100);

            SetHits(200);
            SetDamage(0);

            SetResistance(ResistanceType.Physical, 0, 10);
            SetResistance(ResistanceType.Fire, 70, 80);
            SetResistance(ResistanceType.Cold, 70, 80);
            SetResistance(ResistanceType.Poison, 70, 80);
            SetResistance(ResistanceType.Energy, 70, 80);

            Fame = 0;
            Karma = 0;
            VirtualArmor = 50;

            try
            {
                InteractiveEffects.SendEffect(AnimEffect.Smoke, 1109, this);
            }
            catch
            {
                Effects.SendLocationEffect(Location, Map, 0x3728, 30, 1109, 0);
                Effects.PlaySound(Location, Map, 0x228);
            }

            BeginPulse();
        }      

        public override bool IsEnemy(Mobile m) => false; 
        public override bool CanBeParagon => false;
        public override Poison PoisonImmune => Poison.Lethal;
        public override bool BardImmune => true;

        private void BeginPulse()
        {
            _countdown = 10;
            Timer.DelayCall(TimeSpan.FromSeconds(1), PulseTick);
        }


        private void PulseTick()
        {
            if (Deleted || !Alive)
                return;

            if (_countdown > 0)
            {
                PublicOverheadMessage(MessageType.Regular, 33, false, $"*{_countdown}*");
                _countdown--;
                Timer.DelayCall(TimeSpan.FromSeconds(1), PulseTick);
            }
            else
            {
                DoExplosion();
                BeginPulse(); 
            }
        }

        private void DoExplosion()
        {
            PublicOverheadMessage(MessageType.Regular, 33, false, "*BOOM!*");

            try
            {
                InteractiveEffects.SendEffect(AnimEffect.ExplosionMulti, 0, this);
            }
            catch
            {
                Effects.SendLocationEffect(Location, Map, 0x36BD, 30, 0, 0);
                Effects.PlaySound(Location, Map, 0x307);
            }

            foreach (Mobile m in GetMobilesInRange(4))
            {
                if (m is PlayerMobile pm && pm.Alive)
                {
                    int dmg = Utility.RandomMinMax(25, 55);
                    pm.Damage(dmg, this);
                }
            }
        }

        private Nyx _master;

        public void SetMaster(Nyx master)
        {
            _master = master;
        }

        public override bool OnBeforeDeath()
        {
            PublicOverheadMessage(MessageType.Regular, 33, false, "The eclipse orb shatters!");

            try
            {
                InteractiveEffects.SendEffect(AnimEffect.Smoke, 1109, this);
            }
            catch
            {
                Effects.SendLocationEffect(Location, Map, 0x3728, 30, 1109, 0);
                Effects.PlaySound(Location, Map, 0x228);
            }

            if (_master != null && !_master.Deleted)
            {
                _master.NotifyOrbDestroyed(this);
            }

            Delete();
            return false;
        }


        public EclipseOrb(Serial serial) : base(serial) { }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); 
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            BeginPulse();
        }
    }
}
