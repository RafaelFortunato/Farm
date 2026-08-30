using UnityEngine;

/// <summary>
/// The stove: turns raw produce into something worth far more.
///
/// Idle -> (player picks a recipe) -> Cooking -> Ready -> (player collects) -> Idle, on the
/// same elapsed-real-time clock a SoilPlot uses, so a throttled browser tab catches up rather
/// than losing progress. Ingredients are spent when the cook starts, not when it finishes -
/// the player should feel the cost at the moment they commit.
///
/// `level` is the stove's tier and gates which recipes are offered. It rises with the
/// farmhouse; until that exists it is set in the inspector.
/// </summary>
public class Stove : Interactable, ITimedProgress
{
    public enum StoveState { Idle, Cooking, Ready }

    [Header("Recipes")]
    [Tooltip("Everything this stove could ever make. Entries above the current tier show as locked.")]
    public RecipeDef[] recipes;

    [Tooltip("Which tier of recipes is unlocked. Rises with the farmhouse.")]
    public int level = 1;

    [Header("Player action")]
    [Tooltip("Beat the player performs when taking the dish out. Leave empty to collect instantly.")]
    public CharacterAction collectAction;

    [Header("Wiring")]
    [Tooltip("Where the finished dish pops out. Defaults to this transform.")]
    public Transform outputAnchor;

    [Header("Runtime (read-only)")]
    [SerializeField] StoveState _state = StoveState.Idle;
    [SerializeField] RecipeDef _cooking;
    [SerializeField] float _startedAt;

    string _prompt = "Cook";

    public StoveState Current => _state;
    public RecipeDef CookingNow => _cooking;

    public bool InProgress => _state == StoveState.Cooking;

    /// <summary>0..1 through the cook.</summary>
    public float Progress
    {
        get
        {
            if (_state == StoveState.Ready) return 1f;
            if (_state != StoveState.Cooking || _cooking == null) return 0f;
            return Mathf.Clamp01((Time.time - _startedAt) / Mathf.Max(_cooking.cookSeconds, 0.01f));
        }
    }

    public override bool CanInteract => _state != StoveState.Cooking;

    // The stove's own state enum drives the key, so it changes exactly when the stove does.
    public override int StateKey => (int)_state;

    // Cached rather than built on access: the prompt is read every frame while focused.
    public override string Prompt => _prompt;

    /// <summary>True when this stove's tier is high enough to offer the recipe at all.</summary>
    public bool IsUnlocked(RecipeDef recipe) => recipe != null && recipe.requiredLevel <= level;

    /// <summary>True when the recipe is unlocked AND the crate can pay for it right now.</summary>
    public bool CanCook(RecipeDef recipe) => IsUnlocked(recipe) && recipe.HasIngredients;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (outputAnchor == null) outputAnchor = transform;
        SetState(_state);   // rebuild the cached prompt from whatever state was serialised
    }

    /// <summary>Single place where state changes, so the prompt can never drift from it.</summary>
    void SetState(StoveState next)
    {
        _state = next;
        switch (_state)
        {
            case StoveState.Idle:  _prompt = "Cook"; break;
            case StoveState.Ready: _prompt = _cooking != null && _cooking.output != null
                                          ? "Collect " + Mathf.Max(_cooking.outputCount, 1) + " " + _cooking.output.displayName
                                          : "Collect"; break;
            default: _prompt = string.Empty; break;
        }
    }

    void Update()
    {
        if (_state == StoveState.Cooking && Progress >= 1f) SetState(StoveState.Ready);
    }

    public override void Interact(PlayerInteractor interactor)
    {
        switch (_state)
        {
            case StoveState.Idle:
                RecipeMenu.Instance?.Open(this, interactor);
                break;
            case StoveState.Ready:
                // The dish pops out at the end of the beat, so the animation reads as its cause.
                interactor.Controller.BeginAction(collectAction, transform, Collect);
                break;
        }
    }

    /// <summary>
    /// Starts a cook if the stove is free and the crate can pay. Returns false without
    /// spending anything otherwise, so the menu can just refresh and stay open.
    /// </summary>
    public bool TryStartCooking(RecipeDef recipe)
    {
        if (_state != StoveState.Idle || !CanCook(recipe)) return false;
        if (!recipe.TryConsume()) return false;

        _cooking = recipe;
        _startedAt = Time.time;
        SetState(StoveState.Cooking);
        return true;
    }

    void Collect()
    {
        if (_state != StoveState.Ready || _cooking == null || _cooking.output == null) return;

        var made = _cooking.output;
        int amount = Mathf.Max(_cooking.outputCount, 1);
        var prefab = made.DisplayPrefab;

        if (prefab != null)
        {
            var spawnAt = outputAnchor.position + Vector3.up * 0.3f;
            var go = Instantiate(prefab, spawnAt, Quaternion.identity);
            var col = go.GetComponent<Collectable>();
            if (col == null) col = go.AddComponent<Collectable>();
            col.item = made;
            col.amount = amount;
        }
        else Inventory.AddProduce(made, amount);   // no model to fly over; still pay out

        _cooking = null;
        SetState(StoveState.Idle);
    }
}
