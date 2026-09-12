using UnityEngine;

namespace Farm.UI
{
    /// <summary>
    /// The house rule for a list row the player cannot use right now: its text greys out.
    ///
    /// Unity's own answer - Button.interactable - only tints the button's background, and on these
    /// near-white rows that is a change of a few percent. The label stays full-strength black, so a
    /// locked recipe goes on looking exactly as clickable as one you can cook. Reading a menu then
    /// means reading every line rather than scanning it.
    ///
    /// So the text is dimmed as well, and it is done from here rather than row by row, so the shop,
    /// the kitchen and anything added later all say "not now" in the same voice.
    ///
    /// Dimming here means going *lighter*, which is the opposite of what greying-out usually means.
    /// A dead row swaps to a mid-slate sprite that blends to about #6D7282, so grey text lands right
    /// on top of the background it is sitting on - measured at 1.3:1, which is no contrast at all.
    /// Going light instead clears 3.9:1, still far quieter than a live row's 11.8:1.
    /// </summary>
    public static class RowInk
    {
        /// <summary>
        /// The colour a dead row's text is pulled towards. Light on purpose - see above. If the
        /// disabled button art ever goes pale again, this is the one value to flip back.
        /// </summary>
        public static readonly Color Dead = new Color(0.894f, 0.910f, 0.937f);   // #E4E8EF

        /// <summary>
        /// How far towards that colour a dead label goes. Nearly all the way, so labels authored at
        /// different darknesses all land on the same quiet tone rather than keeping their pecking
        /// order in a state where none of them can be acted on anyway.
        /// </summary>
        public const float Strength = 0.95f;

        /// <summary>The colour a label should be, given the authored one and whether the row is live.</summary>
        public static Color For(Color authored, bool usable) =>
            usable ? authored : Color.Lerp(authored, Dead, Strength);
    }
}
