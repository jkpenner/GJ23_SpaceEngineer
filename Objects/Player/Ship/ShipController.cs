using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace SpaceEngineer
{
    public enum ShipSystemType
    {
        Engines,
        Weapons,
        Shields,
        Sensors,
    }

    public enum ShipSystemState
    {
        /// <summary>
        /// System is powered off. It will not consume energy and is not functioning.
        /// </summary>
        Disabled,
        /// <summary>
        /// System is powered on. It is functioning at normal capacity.
        /// </summary>
        Powered,
        /// <summary>
        /// System is powered on. It is functioning at an increased capacity.
        /// </summary>
        Overclocked,
        /// <summary>
        /// System is powered on. It is functioning at a reduced capacity and
        /// must be repaired to return to its normal state.
        /// </summary>
        Damaged,
        /// <summary>
        /// System is powered off. It does not consume energy and is not functioning.
        /// It must be repaired in order to be powered back on.
        /// </summary>
        Destroyed,
    }

    public enum ShipOverloadState
    {
        NotOverloaded,
        Overloading,
        Overloaded,
    }

    public enum ShipCombatState
    {
        Idle,
        Targeting
    }

    public partial class ShipController : Node3D
    {
        [Export] int initialEnergyCapacity = 10;
        [Export] int maximumEnergy = 10;
        [Export] float energyRegenDuration = 30f;
        // A value of 1 would increase the counter by 1 every second, which
        // would essentially reduce the recharge rate by half.
        [Export] float energyRegenPerPlayerRate = 1f;
        [Export] float energyRegenMaxPlayerRate = 2f; // Multiplayer?
        [Export] float timeTillOverload = 15f;
        [Export] float overloadCooldownRate = 0.2f;

        [ExportGroup("Life Support")]
        [Export] float lifeSupportDuration = 20f;

        [ExportGroup("Weapon System")]
        [Export] ShipSystemState initialWeaponState = ShipSystemState.Powered;
        [Export] int weaponNormalEnergy = 2;
        [Export] int weaponOverclockEnergy = 4;
        [Export] float weaponOverclockDuration = 30f;
        [Export] float weaponTargetingDuration = 10f;

        [ExportGroup("Engine System")]
        [Export] ShipSystemState initialEngineState = ShipSystemState.Powered;
        [Export] int engineNormalEnergy = 2;
        [Export] int engineOverclockEnergy = 4;
        [Export] float engineOverclockDuration = 30f;
        [Export] float engineDodgeChancePowered = 0.25f;
        [Export] float engineDodgeChangeOverclocked = 0.4f;
        [Export] float engineDodgeChangeDamaged = 0.1f;

        [ExportGroup("Shield System")]
        [Export] ShipSystemState initialShieldState = ShipSystemState.Powered;
        [Export] int shieldNormalEnergy = 2;
        [Export] int shieldOverclockEnergy = 4;
        [Export] float shieldOverclockDuration = 30f;
        [Export] int baseShieldCharges = 3;
        [Export] int shieldOverclockCharges = 2;
        [Export] float shieldRechargeRate = 0.5f;
        [Export] float shieldOverclockRechargeRate = 1f;

        [ExportGroup("Sensor System")]
        [Export] ShipSystemState initialSensorState = ShipSystemState.Powered;
        [Export] int sensorNormalEnergy = 2;
        [Export] int sensorOverclockEnergy = 4;
        [Export] float sensorOverclockDuration = 30f;

        [ExportGroup("Debug")]
        [Export] bool debugSimulateHullBreach = false;
        [Export] bool debugDisableLifeSupport = false;
        [Export] bool debugSimulateKineticDamage = false;
        [Export] bool debugSimulateMissileDamage = false;
        [Export] bool debugSimulateLaserDamage = false;


        public ShipSystem WeaponSystem { get; private set; }
        public ShipSystem EngineSystem { get; private set; }
        public ShipSystem ShieldSystem { get; private set; }
        public ShipSystem SensorSystem { get; private set; }

        public int MaximumEnergy => maximumEnergy;
        public int EnergyCapacity { get; private set; }
        public int EnergyUsage { get; private set; }
        public ShipOverloadState OverloadState { get; private set; }

        public ShipCombatState CombatState { get; private set; }
        public EnemyController ActiveTarget { get; private set; }
        public Weapon ActiveWeapon { get; private set; }
        public IReadOnlyList<Weapon> Weapons => weapons;

        /// <summary>
        /// Number of shield charges currently active.
        /// </summary>
        public int ShieldCharges { get; private set; }

        /// <summary>
        /// The maximum number of shield charges the ship can have at once.
        /// </summary>
        public int MaxShieldCharges { get; private set; }

        /// <summary>
        /// Check if the life support is currently failing and the timer is running.
        /// </summary>
        public bool IsLifeSupportDepleting { get; private set; }

        /// <summary>
        /// The maximum amount of time the life support can remain active.
        /// </summary>
        public float LifeSupportMaxDuration => lifeSupportDuration;

        /// <summary>
        /// The current remaining amount of time till life support is depleted and game over.
        /// </summary>
        public float LifeSupportRemainingTime => lifeSupportDuration - lifeSupportCounter;

        private GameManager gameManager;
        private float overloadCounter;
        private float energyRegenCounter;
        private int energyRegenPlayerInput;
        private float lifeSupportCounter;
        private float shieldRechargeCounter;
        private float combatTargetingCounter;

        public float WeaponTargetingPercent => Mathf.Clamp(combatTargetingCounter / weaponTargetingDuration, 0f, 1f);
        public float ShieldRechargePercent => Mathf.Clamp(shieldRechargeCounter / 1f, 0f, 1f);

        private List<Weapon> weapons;
        private List<Treadmill> treadmills;
        private List<DamagableHull> hulls;

        public IReadOnlyList<DamagableHull> Hulls => hulls;

        public event Action Overloading;
        public event Action OverloadEventStarted;
        public event Action OverloadEventCompleted;
        public event Action EnergyNormalized;

        public event Action<int> EnergyUsageChanged;
        public event Action<int> EnergyCapacityChanged;
        public event Action<ShipSystemType> SystemStateChanged;

        public event Action LifeSupportRestored;
        public event Action LifeSupportDepleting;
        public event Action LifeSupportDepleted;

        public event Action ShieldBroken;
        public event Action ShieldRestored;
        public event Action ShieldChargesChanged;

        public event Action<Weapon> WeaponRegistered;
        public event Action<Weapon> WeaponUnegistered;
        public event Action<Weapon> WeaponLoading;
        public event Action<Weapon> WeaponFired;

        public event DamagableHull.HullEvent HullDamaged;

        public ShipController()
        {
            WeaponSystem = new ShipSystem();
            EngineSystem = new ShipSystem();
            ShieldSystem = new ShipSystem();
            SensorSystem = new ShipSystem();

            weapons = new List<Weapon>();
            treadmills = new List<Treadmill>();
            hulls = new List<DamagableHull>();
        }

        public override void _Ready()
        {
            this.TryGetGameManager(out gameManager);

            EnergyCapacity = initialEnergyCapacity;
            MaxShieldCharges = baseShieldCharges;
            ShieldCharges = baseShieldCharges;

            WeaponSystem.Setup(initialWeaponState, weaponNormalEnergy, weaponOverclockEnergy, weaponOverclockDuration);
            EngineSystem.Setup(initialEngineState, engineNormalEnergy, engineOverclockEnergy, engineOverclockDuration);
            ShieldSystem.Setup(initialShieldState, shieldNormalEnergy, shieldOverclockEnergy, shieldOverclockDuration);
            SensorSystem.Setup(initialSensorState, sensorNormalEnergy, sensorOverclockEnergy, sensorOverclockDuration);

            ShieldSystem.StateChanged += OnShieldStateChange;

            WeaponSystem.EnergyUsageChanged += OnEnergyUsageChanged;
            EngineSystem.EnergyUsageChanged += OnEnergyUsageChanged;
            ShieldSystem.EnergyUsageChanged += OnEnergyUsageChanged;
            SensorSystem.EnergyUsageChanged += OnEnergyUsageChanged;

            WeaponSystem.StateChanged += () => SystemStateChanged?.Invoke(ShipSystemType.Weapons);
            ShieldSystem.StateChanged += () => SystemStateChanged?.Invoke(ShipSystemType.Shields);
            EngineSystem.StateChanged += () => SystemStateChanged?.Invoke(ShipSystemType.Engines);
            SensorSystem.StateChanged += () => SystemStateChanged?.Invoke(ShipSystemType.Sensors);

            // Initial calls to populate values
            OnEnergyUsageChanged();
        }

        public override void _Process(double delta)
        {
            if (!gameManager.IsGameActive)
            {
                return;
            }

            ProcessCombat(delta);
            ProcessLifeSupport(delta);
            ProcessShields(delta);

            WeaponSystem.Process(delta);
            EngineSystem.Process(delta);
            ShieldSystem.Process(delta);
            SensorSystem.Process(delta);

            if (OverloadState == ShipOverloadState.Overloading)
            {
                overloadCounter += (float)delta;
                if (overloadCounter >= timeTillOverload)
                {
                    OverloadState = ShipOverloadState.Overloaded;
                    OnOverloadEvent();
                }
            }
            else if (OverloadState == ShipOverloadState.NotOverloaded)
            {
                // overload is cooling down at have rate.
                overloadCounter -= (float)delta * overloadCooldownRate;
                overloadCounter = Mathf.Max(overloadCounter, 0f);
            }

            if (EnergyCapacity < MaximumEnergy && OverloadState == ShipOverloadState.NotOverloaded)
            {
                energyRegenCounter += GetEnergyRegenRate() * (float)delta;

                if (energyRegenCounter > energyRegenDuration)
                {
                    var newEnergyCapacity = Mathf.Min(EnergyCapacity + 1, MaximumEnergy);
                    if (EnergyCapacity != newEnergyCapacity)
                    {
                        energyRegenCounter = 0f;
                        EnergyCapacity = newEnergyCapacity;
                        EnergyCapacityChanged?.Invoke(EnergyCapacity);
                        OnEnergyUsageChanged();
                    }
                }
            }

            // Reset amount of player input each frame.
            energyRegenPlayerInput = 0;

            if (debugSimulateKineticDamage)
            {
                debugSimulateKineticDamage = false;
                Damage(AmmoType.Kinetic);
            }

            if (debugSimulateMissileDamage)
            {
                debugSimulateMissileDamage = false;
                Damage(AmmoType.Missile);
            }

            if (debugSimulateLaserDamage)
            {
                debugSimulateLaserDamage = false;
                Damage(AmmoType.Laser);
            }
        }

        private void ProcessCombat(double delta)
        {
            switch (CombatState)
            {
                case ShipCombatState.Idle:
                    var hasLaserReady = HasWeaponReady(AmmoType.Laser);
                    var hasKineticReady = HasWeaponReady(AmmoType.Kinetic);
                    var hasMissileReady = HasWeaponReady(AmmoType.Missile);

                    /// Attack enemys in order from first to last
                    foreach (var enemy in gameManager.Enemies)
                    {
                        if (enemy.IsDestroyed)
                        {
                            continue;
                        }

                        // Ship must always damage shields first before
                        // it can damage the enemy hull.
                        if (enemy.HasLaserShields)
                        {
                            if (!hasLaserReady)
                            {
                                continue;
                            }

                            AttackTarget(enemy, AmmoType.Laser);
                            return;
                        }

                        // Can attack either kinetic or missile shields once
                        // the laser shield is removed. Attack what ever the 
                        // ship has ammo for.
                        if (enemy.HasKineticShields && hasKineticReady)
                        {
                            AttackTarget(enemy, AmmoType.Kinetic);
                        }
                        else if (enemy.HasMissileShields && hasMissileReady)
                        {
                            AttackTarget(enemy, AmmoType.Missile);
                        }
                    }
                    break;
                case ShipCombatState.Targeting:
                    combatTargetingCounter += (float)delta;
                    if (combatTargetingCounter > weaponTargetingDuration)
                    {
                        // GD.Print("Damaging Target");
                        combatTargetingCounter = 0f;
                        ActiveTarget.Damage(ActiveWeapon.AmmoType);

                        ActiveWeapon.Fire();
                        ActiveWeapon.DestroyItem();

                        WeaponFired?.Invoke(ActiveWeapon);

                        ActiveWeapon = null;
                        ActiveTarget = null;
                        CombatState = ShipCombatState.Idle;
                    }
                    break;
            }
        }

        private void AttackTarget(EnemyController enemy, AmmoType ammoType)
        {
            // GD.Print($"Attacking target");
            CombatState = ShipCombatState.Targeting;
            ActiveTarget = enemy;
            ActiveWeapon = GetReadyWeapon(ammoType);
            ActiveWeapon.StartFiringProceedure();
            combatTargetingCounter = 0f;

            WeaponLoading?.Invoke(ActiveWeapon);
        }

        private bool HasWeaponReady(AmmoType ammoType)
        {
            foreach (var weapon in weapons)
            {
                if (weapon.IsReadyToFire() && weapon.AmmoType == ammoType)
                {
                    return true;
                }
            }
            return false;
        }

        private Weapon GetReadyWeapon(AmmoType ammoType)
        {
            foreach (var weapon in weapons)
            {
                if (weapon.IsReadyToFire() && weapon.AmmoType == ammoType)
                {
                    return weapon;
                }
            }
            return null;
        }

        private void OnShieldStateChange()
        {
            if (ShieldSystem.State == ShipSystemState.Overclocked)
            {
                MaxShieldCharges = shieldOverclockCharges + baseShieldCharges;
                ShieldCharges += shieldOverclockCharges;
            }
            else
            {
                MaxShieldCharges = baseShieldCharges;
                ShieldCharges = Mathf.Min(ShieldCharges, MaxShieldCharges);
            }

            if (ShieldCharges == MaxShieldCharges)
            {
                shieldRechargeCounter = 0f;
            }

            ShieldChargesChanged?.Invoke();
        }

        private void ProcessShields(double delta)
        {
            if (ShieldCharges < MaxShieldCharges)
            {
                if (ShieldSystem.State == ShipSystemState.Overclocked)
                {
                    shieldRechargeCounter += shieldOverclockRechargeRate * (float)delta;
                }
                else
                {
                    shieldRechargeCounter += shieldRechargeRate * (float)delta;
                }

                if (shieldRechargeCounter >= 1f)
                {
                    shieldRechargeCounter = 0f;
                    ShieldCharges += 1;
                    ShieldChargesChanged?.Invoke();

                    if (ShieldCharges == 1)
                    {
                        ShieldRestored?.Invoke();
                    }
                }
            }
        }

        public DamageType Damage(AmmoType type)
        {
            Random random = new Random();

            float hitChance = type switch
            {
                AmmoType.Kinetic => 0.8f,
                AmmoType.Missile => 0.7f,
                AmmoType.Laser => 0.9f,
                _ => 0,
            };

            float dodgeChange = EngineSystem.State switch
            {
                ShipSystemState.Powered => engineDodgeChancePowered,
                ShipSystemState.Overclocked => engineDodgeChangeOverclocked,
                ShipSystemState.Damaged => engineDodgeChangeDamaged,
                _ => 0f
            };

            if (random.NextSingle() > hitChance)
            {
                // GD.Print("Damage Missed the ship");
                return DamageType.Missed;
            }

            if (ShieldCharges > 0)
            {
                int shieldDamage = type switch
                {
                    AmmoType.Kinetic => 1,
                    AmmoType.Missile => 2,
                    AmmoType.Laser => 3,
                    _ => 0,
                };

                // Each damage has a chance to be dodge.
                for (int i = 0; i < shieldDamage; i++)
                {
                    if (random.NextSingle() <= dodgeChange)
                    {
                        continue;
                    }

                    // GD.Print($"Taking 1 damage to shields");
                    ShieldCharges -= 1;
                }

                ShieldChargesChanged?.Invoke();

                if (ShieldCharges <= 0)
                {
                    ShieldCharges = 0;
                    ShieldBroken?.Invoke();
                }

                // Damage already applyed to shield, exit early.
                return DamageType.ShieldDamage;
            }

            int hullDamage = type switch
            {
                AmmoType.Kinetic => 2,
                AmmoType.Missile => 3,
                AmmoType.Laser => 1,
                _ => 0,
            };

            if (hulls.Count == 0)
            {
                GD.PrintErr("No Hulls assigned to the ship");
                return DamageType.Missed;
            }

            // GD.Print($"Taking {hullDamage} hull damage");

            // Find all hulls that can be damage
            var targets = hulls.Where(h => h.CanBeDamaged()).ToList();

            // Randomly target up to hull damage amount. Each hull
            // can only be damaged once each time the ship takes damage.
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                int index = random.Next(targets.Count);
                var hull = targets[index];
                targets.RemoveAt(index);

                if (random.NextSingle() <= dodgeChange)
                {
                    // GD.Print("Dodged part of the enemy attack");
                    continue;
                }

                hull.Damage();
                hullDamage -= 1;

                if (hullDamage <= 0)
                {
                    break;
                }
            }

            return DamageType.HullDamage;
        }

        private void ProcessLifeSupport(double delta)
        {
            if (debugDisableLifeSupport)
            {
                if (IsLifeSupportDepleting)
                {
                    IsLifeSupportDepleting = false;
                    lifeSupportCounter = 0f;
                    LifeSupportRestored?.Invoke();
                }

                return;
            }

            // Life Support starts to deplete if there are any breaches in the ship's hull. When the
            // ship's shields are overclocked the life support remains stable and does not deplete.
            if ((CheckForHullBreach() && ShieldSystem.State != ShipSystemState.Overclocked) || debugSimulateHullBreach)
            {
                if (!IsLifeSupportDepleting)
                {
                    IsLifeSupportDepleting = true;
                    LifeSupportDepleting?.Invoke();
                }
            }
            else if (IsLifeSupportDepleting)
            {
                IsLifeSupportDepleting = false;
                lifeSupportCounter = 0f;
                LifeSupportRestored?.Invoke();
            }

            if (IsLifeSupportDepleting)
            {
                lifeSupportCounter += (float)delta;
                if (lifeSupportCounter >= lifeSupportDuration)
                {
                    // GD.Print("Life support ran out.");
                    LifeSupportDepleted?.Invoke();
                }
            }
        }

        /// <summary>
        /// Inform the ship that a player is generating energy.
        /// Method must be called each frame.
        /// </summary>
        public void IncrementEnergyRegen()
        {
            energyRegenPlayerInput += 1;
        }

        public float GetEnergyRegenRate()
        {
            if (EnergyCapacity >= MaximumEnergy || OverloadState != ShipOverloadState.NotOverloaded)
            {
                return 0f;
            }

            return 1f + Mathf.Min(energyRegenPerPlayerRate * energyRegenPlayerInput, energyRegenMaxPlayerRate);
        }

        /// <summary>
        /// Check if any hull is breached on the ship.
        /// </summary>
        public bool CheckForHullBreach()
        {
            foreach (var hull in hulls)
            {
                if (hull.State == HullState.Breached)
                {
                    return true;
                }
            }

            return false;
        }

        public ShipSystem GetSystem(ShipSystemType systemType)
        {
            return systemType switch
            {
                ShipSystemType.Engines => EngineSystem,
                ShipSystemType.Weapons => WeaponSystem,
                ShipSystemType.Shields => ShieldSystem,
                ShipSystemType.Sensors => SensorSystem,
                _ => throw new NotImplementedException()
            };
        }

        public float GetRemainingTimeTillOverload()
        {
            return Mathf.Max(timeTillOverload - overloadCounter, 0f);
        }

        private void OnEnergyUsageChanged()
        {
            EnergyUsage = WeaponSystem.CurrentEnergyUsage
                + EngineSystem.CurrentEnergyUsage
                + ShieldSystem.CurrentEnergyUsage
                + SensorSystem.CurrentEnergyUsage;

            EnergyUsageChanged?.Invoke(EnergyUsage);

            if (EnergyUsage > EnergyCapacity && OverloadState == ShipOverloadState.NotOverloaded)
            {
                // GD.Print($"Ship energy is overloading ({timeTillOverload} seconds)");
                OverloadState = ShipOverloadState.Overloading;
                Overloading?.Invoke();
            }
            else if (EnergyUsage <= EnergyCapacity && OverloadState == ShipOverloadState.Overloading)
            {
                // GD.Print("Ship energy returned to normal, cooling down.");
                OverloadState = ShipOverloadState.NotOverloaded;
                EnergyNormalized?.Invoke();
            }
        }

        private void OnOverloadEvent()
        {
            overloadCounter = 0f;
            OverloadEventStarted?.Invoke();

            DestroyAllOverclockedSystems();
            DestroyRandomUntilNormalized();

            // Stop overload event and reduce energy capacity by 1
            OverloadState = ShipOverloadState.NotOverloaded;

            var newEnergyCapacity = Mathf.Max(EnergyCapacity - 1, 0);
            if (EnergyCapacity != newEnergyCapacity)
            {
                EnergyCapacity = newEnergyCapacity;
                EnergyCapacityChanged?.Invoke(EnergyCapacity);
            }

            OverloadEventCompleted?.Invoke();
        }

        private void DestroyAllOverclockedSystems()
        {
            foreach (var systemType in GetSystemTypeArray())
            {
                var system = GetSystem(systemType);
                if (system.State != ShipSystemState.Overclocked)
                {
                    continue;
                }

                system.Destroy();
            }
        }

        private void DestroyRandomUntilNormalized()
        {
            if (EnergyUsage <= EnergyCapacity)
            {
                return;
            }

            Random random = new Random();
            var systemTypes = GetSystemTypeArray().ToList();

            for (int i = systemTypes.Count; i > 0; i--)
            {
                int index = random.Next(i);
                var systemType = systemTypes[index];
                systemTypes.RemoveAt(index);

                var system = GetSystem(systemType);
                if (system.State != ShipSystemState.Powered)
                {
                    system.TogglePower();
                }
                else if (system.State != ShipSystemState.Damaged)
                {
                    system.Destroy();
                }

                if (EnergyUsage <= EnergyCapacity)
                {
                    break;
                }
            }
        }

        private ShipSystemType[] GetSystemTypeArray()
        {
            return new ShipSystemType[] {
                ShipSystemType.Engines,
                ShipSystemType.Shields,
                ShipSystemType.Weapons,
                ShipSystemType.Sensors,
            };
        }

        public void RegisterTreadmill(Treadmill treadmill)
        {
            if (treadmill is null)
            {
                return;
            }

            treadmill.EnergyGenerated += IncrementEnergyRegen;
            treadmills.Add(treadmill);
        }

        public void UnregisterTreadmill(Treadmill treadmill)
        {
            if (treadmill is null)
            {
                return;
            }

            treadmill.EnergyGenerated -= IncrementEnergyRegen;
            treadmills.Remove(treadmill);
        }

        public void RegisterHull(DamagableHull damagableHull)
        {
            if (damagableHull is null)
            {
                return;
            }

            hulls.Add(damagableHull);
            damagableHull.HullDamaged += OnHullDamaged;
        }

        public void UnregisterHull(DamagableHull damagableHull)
        {
            if (damagableHull is null)
            {
                return;
            }

            damagableHull.HullDamaged -= OnHullDamaged;
            hulls.Remove(damagableHull);
        }

        private void OnHullDamaged(DamagableHull hull)
        {
            HullDamaged?.Invoke(hull);
        }

        public void RegisterWeapon(Weapon weapon)
        {
            if (weapon is null)
            {
                return;
            }

            weapons.Add(weapon);
            WeaponRegistered?.Invoke(weapon);
        }

        public void UnregisterWeapon(Weapon weapon)
        {
            if (weapon is null)
            {
                return;
            }

            weapons.Remove(weapon);
            WeaponUnegistered?.Invoke(weapon);
        }

        public float GetOverloadPercent()
        {
            return Mathf.Clamp(overloadCounter / timeTillOverload, 0f, 1f);
        }

        public float GetEnergyRechargePercent()
        {
            return Mathf.Clamp(energyRegenCounter / energyRegenDuration, 0f, 1f);
        }
    }
}