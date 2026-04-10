using System.Collections;
using TMPro;
using UnityEngine;

// Self-contained floating damage number — rises and fades, then destroys itself.
// Spawned by DamageNumberSpawner; not parented to the enemy so it survives enemy death.
//
// Network: MonoBehaviour — client-side visual only.
[RequireComponent(typeof(TextMeshPro))]
public class FloatingDamageText : MonoBehaviour
{
    private TextMeshPro _tmp;

    private void Awake()
    {
        _tmp = GetComponent<TextMeshPro>();
    }

    // Starts the animation. Called immediately after the GO is created.
    public void Play(int amount, float duration = 1f, float riseDistance = 1.2f)
    {
        _tmp.text = amount.ToString();
        StartCoroutine(Animate(duration, riseDistance));
    }

    private IEnumerator Animate(float duration, float riseDistance)
    {
        Vector3 startPos = transform.position;
        Vector3 endPos   = startPos + Vector3.up * riseDistance;
        float elapsed    = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(startPos, endPos, t);
            _tmp.alpha         = Mathf.Lerp(1f, 0f, t);
            elapsed           += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
