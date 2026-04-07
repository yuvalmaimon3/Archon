using UnityEngine;

// ScriptableObject that defines the enemy layout for one room.
// Contains exactly 3 rounds — the last one is always the final round (no timer).
// Create via: Assets > Create > Arcon > Room > Room Config
[CreateAssetMenu(fileName = "RoomConfig", menuName = "Arcon/Room/Room Config")]
public class RoomConfig : ScriptableObject
{
    [Tooltip("Room rounds. Should contain exactly 3 entries. " +
             "The last entry is treated as the final round and will have its timer forced to 0.")]
    public RoundConfig[] Rounds = new RoundConfig[3]
    {
        new RoundConfig { TimerDuration = 30f },
        new RoundConfig { TimerDuration = 20f },
        new RoundConfig { TimerDuration = 0f  }  // final round — no timer
    };

    // Total number of rounds (expected 3, but flexible for future extension).
    public int RoundCount => Rounds?.Length ?? 0;

    // Shorthand for the last round.
    public RoundConfig FinalRound => RoundCount > 0 ? Rounds[RoundCount - 1] : null;

    // Enforce that the last round never has a timer.
    // Unity calls this automatically when values change in the Inspector.
    private void OnValidate()
    {
        if (Rounds != null && Rounds.Length > 0)
            Rounds[Rounds.Length - 1].TimerDuration = 0f;
    }
}
