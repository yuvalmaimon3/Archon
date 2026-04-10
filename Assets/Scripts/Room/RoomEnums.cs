// Shared enums for the room run flow system.
// Kept in one file so all room scripts share a single import.

// The current state of a room's run flow.
public enum RoomState
{
    Idle,              // Room exists but has not started yet
    RoundActive,       // A round is in progress (enemies alive / timer running)
    BetweenRounds,     // Brief pause after a round before the next begins
    WaitingForUpgrade, // All rounds done — waiting for the player to choose an upgrade
    Complete           // Room fully complete, gate is open
}

// Why a round ended.
public enum RoundEndReason
{
    AllEnemiesDefeated, // Player killed all enemies before the timer ran out
    TimerExpired        // Timer ran out with enemies still alive (non-final rounds only)
}
