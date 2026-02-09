using UnityEngine;

/// <summary>
/// Guarantees MainMenuOverlay exists in play mode without manual scene setup.
/// </summary>
public static class MainMenuAutoBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMainMenuOverlay()
    {
        MainMenuOverlay existing = Object.FindObjectOfType<MainMenuOverlay>(true);
        if (existing != null)
        {
            if (existing.gameObject.activeInHierarchy)
            {
                existing.OpenMenu();
            }
            return;
        }

        GameObject go = new GameObject("MainMenuOverlay_Auto");
        Object.DontDestroyOnLoad(go);
        MainMenuOverlay overlay = go.AddComponent<MainMenuOverlay>();
        overlay.showOnStart = true;
        overlay.pauseGameWhileMenuOpen = false;
        overlay.gatePlayerControl = true;
        overlay.freezePlayerPhysicsWhileMenuOpen = false;
        overlay.autoDodgeWhileMenuOpen = true;
        overlay.allowKeyboardStart = true;
        overlay.OpenMenu();
    }
}
