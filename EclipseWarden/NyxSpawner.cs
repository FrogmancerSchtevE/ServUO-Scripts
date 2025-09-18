using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;
using Server.ContextMenus;
using Server.Gumps;

namespace Server.Mobiles
{
    public class NyxSpawner : Item
    {
        private Point3D _corner1;
        private Point3D _corner2;
        private Rectangle2D _rect;
        private Timer _tick;

        private SealState _state = SealState.Dormant;

        private EclipseSeal _seal;
        private Nyx _nyx;
        private List<BaseCreature> _adds = new List<BaseCreature>();

        private int _lastKnownSealRemaining = -1;

        [CommandProperty(AccessLevel.GameMaster)]
        public int SealKillsRequired { get; set; } = 30;

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan WaveInterval { get; set; } = TimeSpan.FromSeconds(20);

        [CommandProperty(AccessLevel.GameMaster)]
        public int WaveCount { get; set; } = 2;

        [CommandProperty(AccessLevel.GameMaster)]
        public int MaxActiveAdds { get; set; } = 6;

        [CommandProperty(AccessLevel.GameMaster)]
        public int AddHomeRange { get; set; } = 4;

        [CommandProperty(AccessLevel.GameMaster)]
        public int SealPulseRadius { get; set; } = 4;

        [CommandProperty(AccessLevel.GameMaster)]
        public int SealPulsePlayerDamageMin { get; set; } = 10;

        [CommandProperty(AccessLevel.GameMaster)]
        public int SealPulsePlayerDamageMax { get; set; } = 20;

        [CommandProperty(AccessLevel.GameMaster)]
        public int SealPulseMobDamageMin { get; set; } = 6;

        [CommandProperty(AccessLevel.GameMaster)]
        public int SealPulseMobDamageMax { get; set; } = 10;

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan MinRespawn { get; set; } = TimeSpan.FromHours(6);

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan MaxRespawn { get; set; } = TimeSpan.FromHours(12);

        [CommandProperty(AccessLevel.GameMaster)]
        public int SealBodyID { get; set; } = 0x3A;

        [CommandProperty(AccessLevel.GameMaster)]
        public int SealHue { get; set; } = 2301;

        private string _mobTypesCsv = string.Empty;

        [CommandProperty(AccessLevel.GameMaster)]
        public string MobTypesCsv
        {
            get => _mobTypesCsv;
            set { _mobTypesCsv = value ?? string.Empty; RebuildMobTable(); }
        }

        // Read-only staff props for progress
        [CommandProperty(AccessLevel.GameMaster)]
        public int SealRemainingKills
        {
            get
            {
                if (_seal != null && !_seal.Deleted) return _seal.Remaining;
                return _lastKnownSealRemaining;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public double SealProgressPercent
        {
            get
            {
                if (SealKillsRequired <= 0) return 0.0;
                int rem = SealRemainingKills;
                if (rem < 0) return 0.0;
                return 100.0 * (SealKillsRequired - rem) / SealKillsRequired;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool RebuildSealTrigger
        {
            get => false;
            set { if (value) { ForceRebuildSeal(); } }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool StartCycleNowTrigger
        {
            get => false;
            set { if (value) { ForceStartCycleNow(); } }
        }

        private struct MobEntry { public Type T; public int W; }
        private List<MobEntry> _mobTable = new List<MobEntry>();
        private int _mobTableTotalWeight = 0;

        private DateTime _nextWave;
        private DateTime _nextRespawn;

        public enum SealState
        {
            Dormant,
            SealActive,
            BossActive
        }

        [Constructable]
        public NyxSpawner() : base(0x1F13)
        {
            Name = "Nyx Spawner";
            Hue = 1109;
            Movable = false;
            Visible = false;

            _tick = new InternalTick(this);
            _tick.Start();
        }

        public NyxSpawner(Serial serial) : base(serial) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (from.AccessLevel >= AccessLevel.GameMaster)
            {
                from.SendMessage(0x44, "Right-click this spawner for staff options.");
            }
        }

        public override void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, list);

            if (from.AccessLevel >= AccessLevel.GameMaster)
            {
                list.Add(new SetArenaEntry(this, from));
                list.Add(new OpenPropsEntry(this, from));
            }
        }

        private class SetArenaEntry : ContextMenuEntry
        {
            private readonly NyxSpawner _spawner;
            private readonly Mobile _from;

            public SetArenaEntry(NyxSpawner spawner, Mobile from)
                : base(2055)
            {
                _spawner = spawner;
                _from = from;
            }

            public override void OnClick()
            {
                if (_spawner == null || _spawner.Deleted || _from == null || _from.Deleted)
                    return;

                _from.SendMessage(0x44, "Target first corner of Nyx arena.");
                _from.Target = new CornerTarget(_spawner, true);
            }
        }

        private class OpenPropsEntry : ContextMenuEntry
        {
            private readonly NyxSpawner _spawner;
            private readonly Mobile _from;

            public OpenPropsEntry(NyxSpawner spawner, Mobile from)
                : base(6103)
            {
                _spawner = spawner;
                _from = from;
            }

            public override void OnClick()
            {
                if (_spawner == null || _spawner.Deleted || _from == null || _from.Deleted)
                    return;

                _from.SendGump(new PropertiesGump(_from, _spawner));
            }
        }

        // ===== GM Corner Targeting =====
        private void BeginCornerTarget()
        {
            if (Deleted || Map == null)
                return;

            foreach (Mobile m in Map.GetMobilesInRange(Location, 18))
            {
                if (m.AccessLevel >= AccessLevel.GameMaster)
                {
                    m.SendMessage(0x44, "Target first corner of Nyx arena.");
                    m.Target = new CornerTarget(this, true);
                    break;
                }
            }
        }

        private void SetCorners(Point3D a, Point3D b)
        {
            _corner1 = a;
            _corner2 = b;

            int minX = Math.Min(a.X, b.X);
            int minY = Math.Min(a.Y, b.Y);
            int maxX = Math.Max(a.X, b.X);
            int maxY = Math.Max(a.Y, b.Y);

            _rect = new Rectangle2D(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);

            SpawnSeal();
        }

        private class CornerTarget : Target
        {
            private readonly NyxSpawner _spawner;
            private readonly bool _first;

            public CornerTarget(NyxSpawner spawner, bool first) : base(-1, true, TargetFlags.None)
            {
                _spawner = spawner;
                _first = first;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(_spawner?.Deleted == false))
                    return;

                if (targeted is IPoint3D p)
                {
                    if (_first)
                    {
                        _spawner._corner1 = new Point3D(p);
                        from.SendMessage(0x44, "First corner set. Target the opposite corner.");
                        from.Target = new CornerTarget(_spawner, false);
                    }
                    else
                    {
                        _spawner._corner2 = new Point3D(p);
                        from.SendMessage(0x44, "Nyx arena defined.");
                        _spawner.SetCorners(_spawner._corner1, _spawner._corner2);
                    }
                }
            }
        }
        // ===== Core Flow =====
        private void SpawnSeal()
        {
            if (Deleted || Map == null || _rect.Width <= 0 || _rect.Height <= 0)
                return;

            CleanupAll();

            Point3D center = GetCenterZ();
            _seal = new EclipseSeal(this);
            _seal.MoveToWorld(center, Map);

            _state = SealState.SealActive;
            _nextWave = DateTime.UtcNow + WaveInterval;

            PublicOverheadMessage(MessageType.Emote, 1109, false, "*A humming Eclipse Seal materializes...*");
        }

        private void SpawnNyxAt(Point3D loc)
        {
            if (Deleted || Map == null)
                return;

            if (_nyx != null && !_nyx.Deleted)
                _nyx.Delete();

            _nyx = new Nyx();
            _nyx.MoveToWorld(loc, Map);

            _state = SealState.BossActive;

            PublicOverheadMessage(MessageType.Emote, 1109, false, "*The Eclipse Seal shatters — Nyx awakens!*");
        }

        internal void OnSealProgress(int remaining)
        {
            _lastKnownSealRemaining = remaining;
        }

        private void EnterCooldown()
        {
            TimeSpan min = MinRespawn;
            TimeSpan max = MaxRespawn;
            if (max < min) { var t = min; min = max; max = t; }

            double roll = Utility.RandomDouble();
            var delay = min + TimeSpan.FromMilliseconds(roll * (max - min).TotalMilliseconds);

            _nextRespawn = DateTime.UtcNow + delay;
            _state = SealState.Dormant;

            CleanupAll();

            PublicOverheadMessage(MessageType.Emote, 1109, false, "*The arena grows silent. The eclipse will return... in time.*");
        }

        private Point3D GetCenterZ()
        {
            int cx = _rect.X + _rect.Width / 2;
            int cy = _rect.Y + _rect.Height / 2;
            int cz = Map.GetAverageZ(cx, cy);
            return new Point3D(cx, cy, cz);
        }

        private class InternalTick : Timer
        {
            private readonly NyxSpawner _s;

            public InternalTick(NyxSpawner s) : base(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0))
            {
                _s = s;
                Priority = TimerPriority.TwoFiftyMS;
            }

            protected override void OnTick()
            {
                _s?.Tick();
            }
        }

        private void Tick()
        {
            if (Deleted || Map == null)
                return;

            switch (_state)
            {
                case SealState.Dormant:
                    if (DateTime.UtcNow >= _nextRespawn && _rect.Width > 0)
                    {
                        SpawnSeal();
                    }
                    break;

                case SealState.SealActive:
                    if (_seal == null || _seal.Deleted)
                    {
                        EnterCooldown();
                        break;
                    }

                    if (DateTime.UtcNow >= _nextWave)
                    {
                        SpawnWave();
                        _nextWave = DateTime.UtcNow + WaveInterval;
                    }
                    break;

                case SealState.BossActive:
                    if (_nyx == null || _nyx.Deleted)
                    {
                        EnterCooldown();
                    }
                    break;
            }

            for (int i = _adds.Count - 1; i >= 0; i--)
            {
                var m = _adds[i];
                if (m == null || m.Deleted)
                    _adds.RemoveAt(i);
            }
        }

        private void SpawnWave()
        {
            if (_seal == null || _seal.Deleted)
                return;

            if (_adds.Count >= Math.Max(0, MaxActiveAdds))
                return;

            var center = _seal.Location;
            int toSpawn = Math.Min(WaveCount, Math.Max(0, MaxActiveAdds - _adds.Count));

            for (int i = 0; i < toSpawn; i++)
            {
                Point3D loc = RandomPointInArena(pad: 1);

                BaseCreature mob = CreateConfiguredAdd();
                if (mob == null) continue;

                mob.Home = center;
                mob.RangeHome = Math.Max(0, AddHomeRange);
                mob.FightMode = FightMode.Closest;
                mob.MoveToWorld(loc, Map);

                _adds.Add(mob);
            }

            PublicOverheadMessage(MessageType.Emote, 1109, false, "*Shadows gather around the Seal...*");
        }

        private Point3D RandomPointInArena(int maxTries = 20, int pad = 0)
        {
            int minX = _rect.X + pad;
            int minY = _rect.Y + pad;
            int maxX = _rect.X + _rect.Width - 1 - pad;
            int maxY = _rect.Y + _rect.Height - 1 - pad;

            for (int i = 0; i < maxTries; i++)
            {
                int x = Utility.RandomMinMax(minX, maxX);
                int y = Utility.RandomMinMax(minY, maxY);
                int z = Map.GetAverageZ(x, y);

                Point3D loc = new Point3D(x, y, z);

                if (Map.CanFit(x, y, z, 16, false, false)) // 16 = mob height allowance AFAIK
                    return loc;
            }

            return GetCenterZ();
        }

        private BaseCreature CreateConfiguredAdd()
        {
            BaseCreature made = null;

            if (_mobTableTotalWeight > 0 && _mobTable.Count > 0)
            {
                int pick = Utility.Random(_mobTableTotalWeight);
                int cursor = 0;

                for (int i = 0; i < _mobTable.Count; i++)
                {
                    cursor += _mobTable[i].W;
                    if (pick < cursor)
                    {
                        try { made = Activator.CreateInstance(_mobTable[i].T) as BaseCreature; }
                        catch { made = null; }
                        break;
                    }
                }
            }

            if (made != null)
                return made;

            switch (Utility.Random(8))
            {
                case 0: return new AirElemental();
                case 1: return new WaterElemental();
                case 2: return new BloodElemental();
            }
            return new EarthElemental();
        }

        private void RebuildMobTable()
        {
            _mobTable.Clear();
            _mobTableTotalWeight = 0;

            string csv = _mobTypesCsv ?? string.Empty;
            if (csv.Length == 0)
                return;

            string[] parts = csv.Split(',');
            foreach (var raw in parts)
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;

                string name = token;
                int weight = 1;

                int colon = token.IndexOf(':');
                if (colon > 0)
                {
                    name = token.Substring(0, colon).Trim();
                    int w;
                    if (int.TryParse(token.Substring(colon + 1).Trim(), out w) && w > 0)
                        weight = w;
                }

                Type t = ScriptCompiler.FindTypeByName(name);
                if (t == null || !typeof(BaseCreature).IsAssignableFrom(t))
                    continue;

                _mobTable.Add(new MobEntry { T = t, W = weight });
                _mobTableTotalWeight += weight;
            }
        }

        private Point3D RandomPointNear(Point3D c, int min, int max)
        {
            int dx = Utility.RandomMinMax(-max, max);
            int dy = Utility.RandomMinMax(-max, max);
            if (Math.Abs(dx) + Math.Abs(dy) < min)
            {
                dx = (dx >= 0 ? 1 : -1) * min;
                dy = (dy >= 0 ? 1 : -1) * min;
            }

            int minX = _rect.X;
            int minY = _rect.Y;
            int maxX = _rect.X + _rect.Width - 1;
            int maxY = _rect.Y + _rect.Height - 1;

            int x = Math.Max(minX, Math.Min(c.X + dx, maxX));
            int y = Math.Max(minY, Math.Min(c.Y + dy, maxY));
            int z = Map.GetAverageZ(x, y);
            return new Point3D(x, y, z);
        }

        // Seal / spawner callbacks
        public void OnSealBroken(Point3D at)
        {
            if (_state != SealState.SealActive)
                return;

            try { Effects.SendLocationEffect(at, Map, 0x36BD, 30, 0, 0); Effects.PlaySound(at, Map, 0x307); } catch { }
            _seal?.Delete();
            _seal = null;

            SpawnNyxAt(at);
        }

        public void ForceRebuildSeal()
        {
            CleanupAll();
            SpawnSeal();
            _lastKnownSealRemaining = SealKillsRequired;
        }

        public void ForceStartCycleNow()
        {
            CleanupAll();
            _state = SealState.Dormant;
            _nextRespawn = DateTime.UtcNow; 
        }

        private void CleanupAll()
        {
            if (_seal != null && !_seal.Deleted)
                _seal.Delete();
            _seal = null;

            if (_nyx != null && !_nyx.Deleted)
                _nyx.Delete();
            _nyx = null;

            for (int i = _adds.Count - 1; i >= 0; i--)
            {
                var m = _adds[i];
                if (m != null && !m.Deleted)
                    m.Delete();
            }
            _adds.Clear();
        }

        public override void OnDelete()
        {
            base.OnDelete();
            _tick?.Stop();
            _tick = null;
            CleanupAll();
        }

        // ===== Serialization =====
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); 

            writer.Write(_corner1);
            writer.Write(_corner2);

            writer.Write(_rect);

            writer.Write((int)_state);

            writer.Write(_seal);
            writer.Write(_nyx);

            writer.Write(_adds.Count);
            foreach (var m in _adds) writer.Write(m);

            writer.Write(SealKillsRequired);
            writer.Write(WaveInterval);
            writer.Write(WaveCount);

            writer.Write(MaxActiveAdds);
            writer.Write(AddHomeRange);

            writer.Write(SealPulseRadius);
            writer.Write(SealPulsePlayerDamageMin);
            writer.Write(SealPulsePlayerDamageMax);
            writer.Write(SealPulseMobDamageMin);
            writer.Write(SealPulseMobDamageMax);

            writer.Write(MinRespawn);
            writer.Write(MaxRespawn);
            writer.Write(_nextWave);
            writer.Write(_nextRespawn);

            writer.Write(SealBodyID);
            writer.Write(SealHue);

            writer.Write(_mobTypesCsv);

            writer.Write(_lastKnownSealRemaining);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int v = reader.ReadInt();

            _corner1 = reader.ReadPoint3D();
            _corner2 = reader.ReadPoint3D();

            _rect = reader.ReadRect2D();

            _state = (SealState)reader.ReadInt();

            _seal = reader.ReadMobile() as EclipseSeal;
            _nyx = reader.ReadMobile() as Nyx;

            int addCount = reader.ReadInt();
            _adds = new List<BaseCreature>();
            for (int i = 0; i < addCount; i++)
            {
                var m = reader.ReadMobile() as BaseCreature;
                if (m != null && !m.Deleted)
                    _adds.Add(m);
            }

            SealKillsRequired = reader.ReadInt();
            WaveInterval = reader.ReadTimeSpan();
            WaveCount = reader.ReadInt();

            if (v >= 1)
            {
                MaxActiveAdds = reader.ReadInt();
                AddHomeRange = reader.ReadInt();
            }
            else
            {
                MaxActiveAdds = 6;
                AddHomeRange = 4;
            }

            SealPulseRadius = reader.ReadInt();
            SealPulsePlayerDamageMin = reader.ReadInt();
            SealPulsePlayerDamageMax = reader.ReadInt();
            SealPulseMobDamageMin = reader.ReadInt();
            SealPulseMobDamageMax = reader.ReadInt();

            MinRespawn = reader.ReadTimeSpan();
            MaxRespawn = reader.ReadTimeSpan();
            _nextWave = reader.ReadDateTime();
            _nextRespawn = reader.ReadDateTime();

            if (v >= 1)
            {
                SealBodyID = reader.ReadInt();
                SealHue = reader.ReadInt();
                _mobTypesCsv = reader.ReadString();
            }
            else
            {
                SealBodyID = 0x3A;
                SealHue = 2301;
                _mobTypesCsv = string.Empty;
            }

            if (v >= 2)
            {
                _lastKnownSealRemaining = reader.ReadInt();
            }
            else
            {
                _lastKnownSealRemaining = -1;
            }

            RebuildMobTable();

            if (_tick == null)
            {
                _tick = new InternalTick(this);
                _tick.Start();
            }

            if (_state == SealState.Dormant && _nextRespawn < DateTime.UtcNow)
                EnterCooldown();
        }

        // ===== Nested: EclipseSeal =====
        private class EclipseSeal : BaseCreature
        {
            private NyxSpawner _spawner;
            private int _remaining;
            private Timer _pulseTimer;

            public override bool DeleteCorpseOnDeath => true;
            public override bool BardImmune => true;
            public override bool CanBeParagon => false;

            public int Remaining => _remaining;

            [Constructable]
            public EclipseSeal(NyxSpawner spawner)
                : base(AIType.AI_Melee, FightMode.None, 10, 1, 0.2, 0.4)
            {
                _spawner = spawner;
                Name = "the Eclipse Seal";
                Body = _spawner?.SealBodyID ?? 0x3A;
                Hue = _spawner?.SealHue ?? 2301;
                CantWalk = true;

                SetStr(1000); SetDex(100); SetInt(1000);
                SetDamage(0);

                SetResistance(ResistanceType.Physical, 100);
                SetResistance(ResistanceType.Fire, 100);
                SetResistance(ResistanceType.Cold, 100);
                SetResistance(ResistanceType.Poison, 100);
                SetResistance(ResistanceType.Energy, 100);

                _remaining = Math.Max(1, spawner?.SealKillsRequired ?? 30);

                SetHits(_remaining);

                try { Server.Custom.InteractiveEffects.SendEffect(Server.Custom.AnimEffect.Smoke, 1109, this); }
                catch { Effects.SendLocationEffect(Location, Map, 0x3728, 30, 1109, 0); Effects.PlaySound(Location, Map, 0x228); }

                _pulseTimer = new PulseTimer(this);
                _pulseTimer.Start();
            }

            public EclipseSeal(Serial serial) : base(serial) { }

            public override bool IsEnemy(Mobile m) => false;
            public override bool OnBeforeDeath() { Delete(); return false; }

            public override void AlterDamageScalarFrom(Mobile caster, ref double scalar) => scalar = 0.0;
            public override int Damage(int amount, Mobile from) => 0;

            private class PulseTimer : Timer
            {
                private readonly EclipseSeal _seal;
                public PulseTimer(EclipseSeal s) : base(TimeSpan.FromSeconds(3.0), TimeSpan.FromSeconds(3.0))
                {
                    _seal = s; Priority = TimerPriority.TwoFiftyMS;
                }

                protected override void OnTick()
                {
                    var s = _seal;
                    if (s == null || s.Deleted || s.Map == Map.Internal)
                    {
                        Stop(); return;
                    }

                    s.DoPulse();
                }
            }

            private void DoPulse()
            {
                try { Server.Custom.InteractiveEffects.SendEffect(Server.Custom.AnimEffect.Sparkle, 2301, this); }
                catch { Effects.SendLocationEffect(Location, Map, 0x375A, 10, 2301); }

                foreach (Mobile m in GetMobilesInRange(_spawner?.SealPulseRadius ?? 4))
                {
                    if (m is PlayerMobile pm && pm.Alive)
                    {
                        pm.PublicOverheadMessage(MessageType.Regular, 2301, false, "The Seal flickers and attempts to repel you!");
                        int dmg = Utility.RandomMinMax(_spawner.SealPulsePlayerDamageMin, _spawner.SealPulsePlayerDamageMax);
                        pm.Damage(dmg, this);
                    }
                }

                var killed = new List<BaseCreature>();
                foreach (Mobile m in GetMobilesInRange(_spawner?.SealPulseRadius ?? 4))
                {
                    if (m is BaseCreature bc && bc.Alive && bc != this && !(bc is Nyx))
                    {
                        int dmg = Utility.RandomMinMax(_spawner.SealPulseMobDamageMin, _spawner.SealPulseMobDamageMax);
                        bc.Damage(dmg, this);

                        if (!bc.Alive || bc.Deleted || bc.Hits <= 0)
                            killed.Add(bc);
                    }
                }

                if (killed.Count > 0)
                {
                    foreach (var k in killed)
                    {
                        Timer.DelayCall(TimeSpan.FromMilliseconds(100), () =>
                        {
                            if (k == null) return;

                            foreach (Item it in k.GetItemsInRange(0))
                            {
                                if (it is Corpse c0 && !c0.Deleted)
                                    c0.Delete();
                            }
                            foreach (Item it in k.GetItemsInRange(1))
                            {
                                if (it is Corpse c1 && !c1.Deleted)
                                    c1.Delete();
                            }

                            if (_remaining > 0)
                            {
                                _remaining--;

                                Hits = Math.Max(0, Math.Min(_remaining, HitsMax));

                                _spawner?.OnSealProgress(_remaining);

                                if (_remaining <= 0)
                                {
                                    try
                                    {
                                        Effects.SendLocationEffect(Location, Map, 0x36BD, 30, 0, 0);
                                        Effects.PlaySound(Location, Map, 0x307);
                                    }
                                    catch { }
                                    _spawner?.OnSealBroken(Location);
                                }
                                else
                                {
                                    if (Utility.RandomDouble() < 0.25)
                                        PublicOverheadMessage(MessageType.Emote, 2301, false, "*The Eclipse Seal weakens...*");
                                }
                            }
                        });
                    }
                }
            }

            public override void OnDelete()
            {
                _pulseTimer?.Stop();
                _pulseTimer = null;
                base.OnDelete();
            }

            public override void Serialize(GenericWriter writer)
            {
                base.Serialize(writer);
                writer.Write(0);
                writer.Write(_spawner);
                writer.Write(_remaining);
            }

            public override void Deserialize(GenericReader reader)
            {
                base.Deserialize(reader);
                int v = reader.ReadInt();
                _spawner = reader.ReadItem() as NyxSpawner;
                _remaining = reader.ReadInt();

                Hits = Math.Max(0, Math.Min(_remaining, HitsMax));
                _spawner?.OnSealProgress(_remaining);

                if (_pulseTimer == null)
                {
                    _pulseTimer = new PulseTimer(this);
                    _pulseTimer.Start();
                }
            }
        }
    }
}
