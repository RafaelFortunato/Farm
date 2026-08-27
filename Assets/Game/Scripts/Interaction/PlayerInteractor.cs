using UnityEngine;

/// <summary>
/// Picks the nearest usable Interactable within range and routes the Interact input to it.
///
/// Uses the static Interactable registry rather than physics overlaps: the farm has a
/// dozen or so interactables, so a straight distance compare is cheaper than a query
/// and needs no colliders or layer setup.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    [Tooltip("How close the player must be, in world units. A ground tile is 2 wide.")]
    public float range = 2.5f;

    [Tooltip("Optional marker placed over the focused object.")]
    public GameObject focusMarker;

    public Interactable Current { get; private set; }

    InputSystem_Actions _input;
    Transform _tf;
    float _rangeSq;

    void OnEnable()
    {
        _tf = transform;
        _rangeSq = range * range;

        _input ??= new InputSystem_Actions();
        _input.Player.Enable();
    }

    void OnDisable() => _input?.Player.Disable();

    void OnDestroy() => _input?.Dispose();

    void OnValidate() => _rangeSq = range * range;

    void Update()
    {
        UpdateFocus();

        // Polled rather than a 'performed' subscription: there is no subscribe/unsubscribe
        // lifecycle to get wrong across domain reloads, and it reads next to the focus logic.
        if (_input.Player.Interact.WasPressedThisFrame()
            && !SeedMenu.IsOpen                       // the menu owns input while it is up
            && Current != null && Current.CanInteract)
        {
            Current.Interact(this);
        }
    }

    void UpdateFocus()
    {
        Interactable best = null;
        float bestSq = _rangeSq;
        Vector3 me = _tf.position;

        var all = Interactable.All;
        for (int i = 0; i < all.Count; i++)
        {
            var it = all[i];
            if (!it.CanInteract) continue;

            Vector3 d = it.FocusPoint - me;
            float sq = d.x * d.x + d.z * d.z;   // planar - height shouldn't matter
            if (sq > bestSq) continue;

            bestSq = sq;
            best = it;
        }

        if (best != Current)
        {
            if (Current != null) Current.OnFocusExit();
            Current = best;
            if (Current != null) Current.OnFocusEnter();

            if (focusMarker != null) focusMarker.SetActive(Current != null);
        }

        if (focusMarker != null && Current != null)
            focusMarker.transform.position = Current.FocusPoint;
    }

    /// <summary>Called by UI so a menu can be dismissed without re-triggering interaction.</summary>
    public void ClearFocus()
    {
        if (Current != null) Current.OnFocusExit();
        Current = null;
        if (focusMarker != null) focusMarker.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
