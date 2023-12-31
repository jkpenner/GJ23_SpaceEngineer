namespace SpaceEngineer
{
    public static class GameEvents
    {
        public static readonly GameEvent<GameState> GameStateEntered = new GameEvent<GameState>();
        public static readonly GameEvent<GameState> GameStateExited = new GameEvent<GameState>();
        public static readonly GameEvent<Notification> GameNotification = new GameEvent<Notification>();


        /// <summary>
        /// Invoked when an inpact ship event occurs.
        /// </summary>
        public static readonly GameEvent Impact = new GameEvent();

        public static readonly GameEvent LifeSupportDepleting = new GameEvent();
        public static readonly GameEvent LifeSupportRestored = new GameEvent();

        /// <summary>
        /// Occurs when the energy usage for all active system exceeds the ship's energy capacity
        /// and the ship is about to be overloaded.
        /// </summary>
        public static readonly GameEvent ShipEnergyOverloading = new GameEvent();
        
        /// <summary>
        /// Occurs when the energy usage for all active system exceeds the ship's energy capacity
        /// for an extended period of time.
        /// </summary>
        public static readonly GameEvent ShipEnergyOverloaded = new GameEvent();
        
        /// <summary>
        /// Occurs when the energy usage falls back below the ship's capacity after entering
        /// an overloading state.
        /// </summary>
        public static readonly GameEvent ShipEnergyNormalized = new GameEvent();

        /// <summary>
        /// Invoked when the amount of energy being used by the player ship changes.
        /// </summary>
        public static readonly GameEvent<int> ShipEnergyUsageChanged = new GameEvent<int>();


        

        /// <summary>
        /// Invoked when the total usable amount of energy for the player ship changes.
        /// </summary>
        public static readonly GameEvent<int> ShipEnergyCapacityChanged = new GameEvent<int>();
        
        /// <summary>
        /// Ship Fires a Weapon
        /// </summary>
        public static readonly GameEvent<AmmoType> PlayerFired = new GameEvent<AmmoType>();
        
        /// <summary>
        /// Enemy Fires a Weapon
        /// </summary>
        public static readonly GameEvent<AmmoType> EnemyFired = new GameEvent<AmmoType>();
        
        /// <summary>
        /// Create Random an enemy
        /// </summary>
        public static readonly GameEvent<EnemyController> EnemySpawned = new GameEvent<EnemyController>();
        public static readonly GameEvent<EnemyController> EnemyDestroy = new GameEvent<EnemyController>();

        public static readonly GameEvent<ShipSystemType> ShipSystemStateChanged = new GameEvent<ShipSystemType>();
        public static readonly GameEvent<ShipSystemType> SystemDestroyed = new GameEvent<ShipSystemType>();

        public static readonly GameEvent<Interactable> PlayerTargetChanged = new GameEvent<Interactable>();

    }
}