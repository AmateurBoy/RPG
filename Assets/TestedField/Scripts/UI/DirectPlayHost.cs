using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Игровая сцена запущена напрямую (Play в редакторе на Run/TestField, минуя меню) — стартует хост соло,
    /// как PIE в UE. При обычном переходе из лобби сеть уже запущена и компонент ничего не делает.
    /// </summary>
    public class DirectPlayHost : MonoBehaviour
    {
        void Start()
        {
            NetSession.EnsureHostForDirectPlay();
        }
    }
}
