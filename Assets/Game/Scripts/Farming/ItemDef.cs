using UnityEngine;

namespace Farm.Farming
{
    /// <summary>
    /// Anything the player can hold: a harvested crop, an egg, a bottle of milk, a cake.
    ///
    /// Split out of CropDef because the inventory, the truck orders and the sell counter all deal in
    /// "an item and a count" and none of them care whether that item was grown. CropDef extends this
    /// with the parts that only apply to something you plant.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "Farm/Item Definition")]
    public class ItemDef : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Item";

        [Tooltip("Name used when there is more than one, e.g. \"Apples\". Leave empty for words that " +
                 "do not change, like Milk - the display name is used as-is.")]
        public string pluralName;

        [Tooltip("Tints this item's chip in the inventory HUD and its row in menus.")]
        public Color tintColor = Color.white;

        [Tooltip("UI icon. Rendered from the model below by the Farm/Render Item Icons pass.")]
        public Sprite icon;

        [Header("Economy")]
        [Tooltip("Coins one of these fetches at the shop counter, which is always open and takes any " +
                 "quantity. A truck pays a multiple of it, so this is the floor rather than the prize. " +
                 "Set so that later, slower goods earn more per minute than early ones - see " +
                 "GAME_CONCEPT.md section 5.")]
        public int sellValue = 10;

        [Header("World")]
        [Tooltip("Model spawned as a pickup and shown on a truck's order badge.")]
        public GameObject worldPrefab;

        [Tooltip("Extra scale for the rendered icon when the model reads too small or too large.")]
        public float iconScale = 1f;

        /// <summary>
        /// The model to show for this item. Virtual so a crop can fall back to its final growth
        /// stage when no dedicated pickup model is authored.
        /// </summary>
        public virtual GameObject DisplayPrefab => worldPrefab;

        /// <summary>The name to print beside a count: the plural for anything but one, when one is set.</summary>
        /// <param name="count">How many are being described.</param>
        public string NameFor(int count) =>
            count != 1 && !string.IsNullOrEmpty(pluralName) ? pluralName : displayName;
    }
}
