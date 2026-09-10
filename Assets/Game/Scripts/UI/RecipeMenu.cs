using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The cooking menu, opened by walking up to the Stove and pressing interact.
///
/// Same shape as StoreMenu: the panel is authored as a prefab under Prefabs/UI, saved
/// deactivated, and this script only shows it and fills in data. What differs is what a row
/// costs - a recipe is paid for in produce rather than coins - and that picking one closes
/// the menu, because the stove is busy the moment a cook starts.
/// </summary>
public class RecipeMenu : MonoBehaviour
{
    /// <summary>True while the menu is up, so gameplay input can ignore Interact.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Prefab wiring")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Transform rowContainer;
    [SerializeField] RecipeRow rowPrefab;
    [SerializeField] Button closeButton;

    [Header("Camera")]
    [Tooltip("How far the camera pushes in while the menu is open, in world units.")]
    public float cameraZoom = 5f;
    [Tooltip("Extra tilt while the menu is open, in degrees. Negative drops the camera lower.")]
    public float cameraPitch = -8f;
    [Tooltip("Seconds to ease the camera in, and back out on close.")]
    public float cameraBlend = 0.35f;

    CameraFollow _cameraRig;

    readonly List<RecipeRow> _rows = new List<RecipeRow>();
    RecipeDef[] _menu;
    Stove _stove;
    PlayerInteractor _interactor;

    void OnDestroy()
    {
        IsOpen = false;
    }

    /// <summary>
    /// The stove's list, ordered by the level that unlocks it.
    ///
    /// Every row says "Needs farmhouse LvN", so a list that jumps back down a tier reads as a
    /// mistake - which is exactly what happened when a level-3 dish was appended after the
    /// level-4 ones. Sorting here rather than asking whoever adds a recipe to insert it in the
    /// right slot means it cannot drift again.
    ///
    /// Insertion sort because it is STABLE and the list is a handful of entries: recipes that
    /// share a tier keep the order they were authored in, so the inspector still decides how
    /// bread and salad sit relative to each other.
    ///
    /// Copied rather than sorted in place - _menu would otherwise alias the stove's own
    /// serialized array and reordering it at runtime would quietly rewrite the asset.
    /// Nulls are dropped here too: BuildRows skips them while Refresh indexes rows against
    /// this array, so a hole in the middle would pair every later row with the wrong recipe.
    /// </summary>
    static RecipeDef[] ByTier(RecipeDef[] source)
    {
        var list = new List<RecipeDef>(source != null ? source.Length : 0);
        if (source != null)
            foreach (var r in source)
                if (r != null) list.Add(r);

        for (int i = 1; i < list.Count; i++)
        {
            var item = list[i];
            int j = i - 1;
            while (j >= 0 && list[j].requiredLevel > item.requiredLevel)
            {
                list[j + 1] = list[j];
                j--;
            }
            list[j + 1] = item;
        }
        return list.ToArray();
    }

    public void Open(Stove stove, PlayerInteractor interactor = null)
    {
        if (stove == null) return;

        AudioManager.PlayOpen();

        _stove = stove;
        _menu = ByTier(stove.recipes);
        _interactor = interactor;

        gameObject.SetActive(true);              // this object is the toggle
        if (panelRoot != null) panelRoot.SetActive(true);

        // same push-in the shop uses, so the two menus feel like one thing. Resolved here
        // rather than in OnEnable: this object starts disabled.
        if (_cameraRig == null) _cameraRig = GameManager.CameraRig;
        _cameraRig.SetActionFraming(cameraZoom, cameraPitch, cameraBlend);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
        BuildRows();

        IsOpen = true;
        Refresh();

        // rows were just spawned into a panel enabled this frame - settle it now
        Menus.RebuildLayout(rowContainer as RectTransform,
                            rowContainer != null ? rowContainer.parent as RectTransform : null);
    }

    public void Close()
    {
        AudioManager.PlayClose();
        _stove = null;
        _interactor = null;
        IsOpen = false;

        if (_cameraRig != null) _cameraRig.ClearActionFraming();

        gameObject.SetActive(false);
    }

    /// <summary>
    /// One row per recipe the stove knows. Cleared first so this is safe to call again -
    /// two stoves could offer different menus.
    /// </summary>
    void BuildRows()
    {
        if (rowPrefab == null || rowContainer == null || _menu == null) return;

        _rows.Clear();
        for (int i = rowContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(rowContainer.GetChild(i).gameObject);

        foreach (var recipe in _menu)
        {
            if (recipe == null) continue;
            var row = Instantiate(rowPrefab, rowContainer);
            row.name = "RecipeRow_" + recipe.DisplayName;
            row.Bind(recipe, Cook);
            _rows.Add(row);
        }
    }

    void Refresh()
    {
        if (_stove == null) return;
        for (int i = 0; i < _rows.Count && i < _menu.Length; i++)
            if (_rows[i] != null && _menu[i] != null)
                _rows[i].SetAvailable(_stove.IsUnlocked(_menu[i]), _stove.CanCook(_menu[i]));
    }

    void Cook(RecipeDef recipe)
    {
        if (_stove == null) return;

        // Failure just refreshes: the row greys out and the player can see why.
        if (!_stove.TryStartCooking(recipe)) { AudioManager.PlayDenied(); Refresh(); return; }

        // The stove is busy now, so there is nothing left to choose.
        Close();
    }
}
