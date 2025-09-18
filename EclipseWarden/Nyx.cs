using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Items;
using Server.Network;

namespace Server.Mobiles
{
    [CorpseName("the corpse of Nyx")]
    public class Nyx : BaseCreature
    {
        private DateTime _nextChainsAllowed;
        private bool _orbs50Triggered;
        private bool _orbs5Triggered;
        private bool _immune;
        private List<EclipseOrb> _activeOrbs;

        private DateTime _nextPoolTime;
        private DateTime _nextReactivePoolAllowed;

        private Mobile _focusTarget;
        private Dictionary<Mobile, UmbralChain> _activeChains;
        private bool _chainsActive;


        [Constructable]
        public Nyx()
            : base(AIType.AI_Mage, FightMode.Closest, 12, 1, 0.2, 0.4)
        {
            Name = "Nyx, the Eclipse Warden";
            Body = 258; // Lady Melisande body
            Hue = 1109;   // charcoal/purple

            SetStr(1200, 1500);
            SetDex(150, 200);
            SetInt(1200, 1500);

            SetHits(30000);

            SetDamage(20, 30);
            SetDamageType(ResistanceType.Physical, 50);
            SetDamageType(ResistanceType.Energy, 50);

            SetResistance(ResistanceType.Physical, 40, 55);
            SetResistance(ResistanceType.Fire, 60, 70);
            SetResistance(ResistanceType.Cold, 60, 70);
            SetResistance(ResistanceType.Poison, 100);
            SetResistance(ResistanceType.Energy, 60, 70);

            Fame = 22500;
            Karma = -22500;

            VirtualArmor = 60;

            _nextChainsAllowed = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            _activeOrbs = new List<EclipseOrb>();

            _nextPoolTime = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            _nextReactivePoolAllowed = DateTime.UtcNow;

            _focusTarget = null;
            _activeChains = new Dictionary<Mobile, UmbralChain>();
            _chainsActive = false;
        }

        public override Poison PoisonImmune => Poison.Lethal;
        public override bool BardImmune => true;
        public override bool CanBeParagon => false;

        public override void OnThink()
        {
            base.OnThink();

            if (!Alive || Map == null || Map == Map.Internal)
                return;

            CheckArenaBounds();

            // Orb Phases
            if (!_orbs50Triggered && Hits < (HitsMax / 2))
            {
                _orbs50Triggered = true;
                DoOrbPhase(50);
            }

            if (!_orbs5Triggered && Hits < (HitsMax / 20))
            {
                _orbs5Triggered = true;
                DoOrbPhase(5);
            }

            // Nights Pool 
            if (DateTime.UtcNow >= _nextPoolTime)
            {
                SpawnNightsPool();
                _nextPoolTime = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            }

            // Umbral Chains chance
            if (DateTime.UtcNow >= _nextChainsAllowed && !_chainsActive)
            {
                if (Utility.RandomDouble() < 0.05) // 5% per think
                {
                    DoUmbralChains();
                    _nextChainsAllowed = DateTime.UtcNow + TimeSpan.FromSeconds(45);
                }
            }
        }

        private void CheckArenaBounds()
        {
            bool playersAlive = false;

            foreach (Mobile m in GetMobilesInRange(30))
            {
                if (m is PlayerMobile && m.Alive)
                {
                    playersAlive = true;
                    break;
                }
            }

            if (!playersAlive)
            {
                ResetFight();
            }
        }

        private void ResetFight()
        {
            Hits = HitsMax;
            _immune = false;
            _orbs50Triggered = false;
            _orbs5Triggered = false;

            foreach (var orb in _activeOrbs)
            {
                if (orb != null && !orb.Deleted)
                    orb.Delete();
            }
            _activeOrbs.Clear();

            foreach (var chain in _activeChains.Values)
            {
                if (chain != null && !chain.Deleted)
                    chain.Delete();
            }
            _activeChains.Clear();
            _chainsActive = false;
            _focusTarget = null;

            PublicOverheadMessage(MessageType.Emote, 1175, false, "*Nyx regains her strength as the arena grows silent...*");
        }

        private void DoOrbPhase(int threshold)
        {
            PublicOverheadMessage(MessageType.Emote, 1175, false, $"*Nyx calls upon the eclipse*");
            _immune = true;

            int orbCount = Utility.RandomMinMax(3, 4);

            for (int i = 0; i < orbCount; i++)
            {
                Point3D loc = new Point3D(X + Utility.RandomMinMax(-2, 2), Y + Utility.RandomMinMax(-2, 2), Z);
                EclipseOrb orb = new EclipseOrb();
                orb.MoveToWorld(loc, Map);

                orb.SetMaster(this);
                _activeOrbs.Add(orb);
            }
        }

        public void NotifyOrbDestroyed(EclipseOrb orb)
        {
            _activeOrbs.Remove(orb);

            if (_activeOrbs.Count == 0 && _immune)
            {
                _immune = false;
                PublicOverheadMessage(MessageType.Emote, 1175, false, "*The eclipse fades and Nyx is vulnerable once more!*");
            }
        }

        private void EndUmbralChains()
        {
            _chainsActive = false;
            _focusTarget = null;

            if (_activeChains != null && _activeChains.Count > 0)
            {
                foreach (var chain in _activeChains.Values)
                {
                    if (chain != null && !chain.Deleted)
                        chain.Delete();
                }
                _activeChains.Clear();
            }

            PublicOverheadMessage(MessageType.Emote, 2301, false, "*The umbral chains fade into nothingness...*");
        }


        public override void AlterDamageScalarFrom(Mobile caster, ref double scalar)
        {
            if (_immune)
                scalar = 0.0;

            if (_chainsActive && caster != _focusTarget)
                scalar *= 1.50; 
        }

        public override int Damage(int amount, Mobile from)
        {
            if (_immune)
            {
                if (from != null && from.Player)
                {
                    PublicOverheadMessage(MessageType.Emote, 1175, false, "*Nyx’s eclipse negates the blow!*");
                }
                return 0;
            }

            if (from != null && from.Player)
            {
                if (DateTime.UtcNow >= _nextReactivePoolAllowed && Utility.RandomDouble() < 0.05)
                {
                    SpawnNightsPool();
                    _nextReactivePoolAllowed = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                }

                if (_chainsActive && from == _focusTarget)
                {
                    amount = (int)(amount * 1.05); 
                }
            }

            return base.Damage(amount, from);
        }

        private void SpawnNightsPool()
        {
            Mobile target = null;

            foreach (Mobile m in GetMobilesInRange(12))
            {
                if (m is PlayerMobile pm && pm.Alive)
                {
                    target = pm;
                    break;
                }
            }

            if (target != null)
            {
                NightsPool pool = new NightsPool();
                pool.MoveToWorld(target.Location, target.Map);

                PublicOverheadMessage(MessageType.Emote, 1175, false, "*Nyx’s blood spills!*");
            }
        }

        private void DoUmbralChains()
        {
            if (_chainsActive)
                return;

            PublicOverheadMessage(MessageType.Emote, 1175, false, "*Nyx raises the shadows into chains!*");

            Timer.DelayCall(TimeSpan.FromSeconds(2), () =>
            {
                if (Deleted || !Alive) return;

                _activeChains.Clear();
                _chainsActive = true;

                foreach (Mobile m in GetMobilesInRange(15))
                {
                    if (m is PlayerMobile pm && pm.Alive)
                    {
                        var chain = new UmbralChain(pm, this);
                        _activeChains[pm] = chain;
                        pm.PublicOverheadMessage(MessageType.Regular, 2301, false, "*Umbral chains bind you!*");
                    }
                }

                if (_activeChains.Count == 0)
                {
                    _chainsActive = false;
                    return;
                }

                List<Mobile> candidates = new List<Mobile>(_activeChains.Keys);
                _focusTarget = candidates[Utility.Random(candidates.Count)];

                PublicOverheadMessage(MessageType.Emote, 2301, false, $"*Nyx hisses: 'You cannot escape me, {_focusTarget.Name}!'*");
            });
        }
        public void NotifyChainBroken(Mobile m)
        {
            if (_activeChains.ContainsKey(m))
            {
                _activeChains.Remove(m);

                if (_focusTarget == m)
                    _focusTarget = null;

                if (_activeChains.Count == 0)
                    EndUmbralChains();
            }
        }
        public override void OnDelete()
        {
            if (_activeOrbs != null)
            {
                foreach (var orb in _activeOrbs)
                {
                    if (orb != null && !orb.Deleted)
                        orb.Delete();
                }
                _activeOrbs.Clear();
            }

            if (_activeChains != null)
            {
                foreach (var chain in _activeChains.Values)
                {
                    if (chain != null && !chain.Deleted)
                        chain.Delete();
                }
                _activeChains.Clear();
            }

            base.OnDelete();
        }

        public Nyx(Serial serial) : base(serial) { }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version

            writer.Write(_nextChainsAllowed);
            writer.Write(_orbs50Triggered);
            writer.Write(_orbs5Triggered);
            writer.Write(_immune);

            writer.Write(_nextPoolTime);
            writer.Write(_nextReactivePoolAllowed);

            writer.Write(_focusTarget);

            writer.Write(_chainsActive);
            writer.Write(_activeChains.Count);
            foreach (var kv in _activeChains)
            {
                writer.Write(kv.Key);
                writer.Write(kv.Value);
            }

            writer.Write(_activeOrbs.Count);
            foreach (var orb in _activeOrbs)
                writer.Write(orb);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            Timer.DelayCall(TimeSpan.Zero, () =>
            {
                if (!Deleted)
                    Delete();
            });
        }
    }
}
