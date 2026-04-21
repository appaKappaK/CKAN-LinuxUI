using CKAN.Versioning;

namespace CKAN.GUI
{
    public sealed class Main
    {
        public readonly IUser? currentUser;
        public readonly GameInstanceManager? Manager;

        public static Main? Instance { get; private set; }

        public GameInstance? CurrentInstance => Manager?.CurrentInstance;

        public bool Waiting => false;

        private Main(GameInstanceManager? manager,
                     IUser?              user)
        {
            Manager = manager;
            currentUser = user;
        }

        public static void SetInstance(GameInstanceManager? manager,
                                       IUser?              user)
            => Instance = new Main(manager, user);

        public static void ClearInstance()
            => Instance = null;
    }
}
