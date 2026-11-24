using UnityEngine;

public class ScreenManager : MonoBehaviour
{
    [SerializeField] private ScreenBounds currentScreen;

    public ScreenBounds CurrentScreen => currentScreen;

    public void SetCurrentScreen(ScreenBounds newScreen)
    {
        currentScreen = newScreen;
    }
}
