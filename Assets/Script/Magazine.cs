using UnityEngine;

public sealed class Magazine : MonoBehaviour
{
    public int Capacity { get; private set; }
    public int CurrentAmmo { get; private set; }
    public int ReserveAmmo { get; private set; }
    public bool Chambered { get; private set; }

    public void Configure(int capacity, int currentAmmo, int reserveAmmo, bool chambered)
    {
        Capacity = Mathf.Max(1, capacity);
        CurrentAmmo = Mathf.Clamp(currentAmmo, 0, Capacity);
        ReserveAmmo = Mathf.Max(0, reserveAmmo);
        Chambered = chambered && CurrentAmmo > 0;
    }

    public bool CanFire => Chambered;

    public bool ConsumeShot()
    {
        if (!Chambered)
        {
            return false;
        }

        CurrentAmmo = Mathf.Max(0, CurrentAmmo - 1);
        Chambered = CurrentAmmo > 0;
        return true;
    }

    public bool Reload(bool loadOneRound = false)
    {
        if (CurrentAmmo >= Capacity || ReserveAmmo <= 0)
        {
            return false;
        }

        int roundsLoaded = loadOneRound ? 1 : Mathf.Min(Capacity - CurrentAmmo, ReserveAmmo);
        roundsLoaded = Mathf.Min(roundsLoaded, ReserveAmmo);
        CurrentAmmo += roundsLoaded;
        ReserveAmmo -= roundsLoaded;
        Chambered = CurrentAmmo > 0;
        return roundsLoaded > 0;
    }
}
