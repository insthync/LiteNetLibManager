namespace LiteNetLibManager
{
    public static class LiteNetLibGameInitializer
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            LiteNetLibGameManager.LoadingServerScenes.Clear();
        }
    }
}
