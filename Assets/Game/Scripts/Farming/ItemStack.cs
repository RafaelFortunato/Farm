namespace Farm.Farming
{
    /// <summary>
    /// An item and how many of it: a recipe's input, an upgrade's price in produce.
    ///
    /// A pair rather than two parallel arrays, so an item can never end up holding someone else's
    /// count when a list is reordered in the inspector.
    /// </summary>
    [System.Serializable]
    public struct ItemStack
    {
        public ItemDef item;
        public int count;

        /// <summary>True when the crate holds at least this many.</summary>
        public bool InStock => item != null && Inventory.ProduceCount(item) >= count;
    }
}
