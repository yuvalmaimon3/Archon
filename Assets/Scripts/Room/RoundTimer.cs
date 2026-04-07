using System;
using System.Collections;
using UnityEngine;

// Countdown timer for a room round.
// Fires OnTimerTick every second (for UI) and OnTimerExpired when it reaches zero.
// Cancel() stops the timer cleanly without firing OnTimerExpired.
public class RoundTimer : MonoBehaviour
{
    // ── Events ────────────────────────────────────────────────────────────────

    // Fired every second while running. Passes (secondsRemaining, totalDuration).
    // Subscribe for countdown UI display.
    public event Action<float, float> OnTimerTick;

    // Fired once when the countdown reaches zero.
    public event Action OnTimerExpired;

    // ── Read-only state ───────────────────────────────────────────────────────

    public float Remaining  { get; private set; }
    public float Total      { get; private set; }
    public bool  IsRunning  { get; private set; }

    // ── Private ───────────────────────────────────────────────────────────────

    private Coroutine _coroutine;

    // ── Public API ────────────────────────────────────────────────────────────

    // Starts a new countdown. Cancels any existing one first.
    public void Begin(float duration)
    {
        Cancel();

        Total     = duration;
        Remaining = duration;
        IsRunning = true;

        _coroutine = StartCoroutine(Countdown());
        Debug.Log($"[RoundTimer] Started — {duration}s.");
    }

    // Stops the timer without firing OnTimerExpired.
    // Call when the round ends early (all enemies defeated).
    public void Cancel()
    {
        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
            _coroutine = null;
        }
        IsRunning = false;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    // Ticks down once per second, firing OnTimerTick on each tick.
    private IEnumerator Countdown()
    {
        while (Remaining > 0f)
        {
            OnTimerTick?.Invoke(Remaining, Total);
            yield return new WaitForSeconds(1f);
            Remaining = Mathf.Max(0f, Remaining - 1f);
        }

        IsRunning = false;
        OnTimerTick?.Invoke(0f, Total);

        Debug.Log("[RoundTimer] Expired.");
        OnTimerExpired?.Invoke();
    }
}
