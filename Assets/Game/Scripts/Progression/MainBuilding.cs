using System;
using System.Collections;
using UnityEngine;
using Farm.Audio;
using Farm.Cooking;
using Farm.Farming;
using Farm.Foraging;
using Farm.Interaction;
using Farm.Selling;
using Farm.UI;

namespace Farm.Progression
{
    /// <summary>
    /// The farmhouse, and the spine of the game's progression.
    ///
    /// Every upgrade costs coins and nothing else. An earlier ladder also charged produce - eggs,
    /// then bread, then cakes - to force the player through their own pipeline, but the truck
    /// queue does that job better now that it buys only cooked goods: the pressure to run the
    /// stove comes from where the money is, rather than from a toll gate on the farmhouse door.
    /// One currency also means one number to tune when the pacing is wrong. Reaching the top
    /// level wins.
    ///
    /// The ladder is steep on purpose and not evenly spaced. Levels 2 and 3 are cheap, because
    /// they hand over the stove and the trucks and the game does not really start until the player
    /// has both; levels 4 and 5 carry most of the run's length.
    ///
    /// Its level is also the one number the rest of the farm reads: the stove's recipe tier and
    /// what the trucks ask for both follow it, so the world opens up in step instead of each
    /// system keeping its own idea of how far along the player is.
    /// </summary>
    public class MainBuilding : Interactable
    {
        [Header("Ladder")]
        [Tooltip("What each step up costs. An asset rather than a list on this component, so the " +
                 "ladder sits beside the crops and recipes it has to stay in balance with.")]
        public FarmhouseLadder ladder;

        [Header("Wiring")]
        public FarmExpansion expansion;

        [Tooltip("Recipe tier follows the farmhouse.")]
        public Stove stove;

        [Tooltip("What the trucks ask for follows the farmhouse.")]
        public TruckQueue truckQueue;

        [Tooltip("Which crops can be planted follows the farmhouse.")]
        public SeedMenu cropMenu;

        [Tooltip("Which foraging spots are in play follows the farmhouse, so growing the island " +
                 "opens the new ground for mushrooms as well.")]
        public MushroomPatch mushroomPatch;

        [Header("Player action")]
        [Tooltip("Beat the player performs when upgrading. Leave empty to upgrade instantly.")]
        public CharacterAction upgradeAction;

        [Header("Runtime (read-only)")]
        [SerializeField] int _level = 1;

        /// <summary>Raised with the new level whenever the farmhouse goes up.</summary>
        public static event Action<int> Changed;

        public int Level => _level;

        /// <summary>The level that wins the game.</summary>
        public int MaxLevel => ladder != null ? ladder.Count + 1 : 1;

        public bool IsMaxLevel => _level >= MaxLevel;

        /// <summary>The next step up, or null once the farmhouse is finished.</summary>
        public FarmhouseLadder.Step Next => IsMaxLevel ? null : ladder.steps[_level - 1];

        public override bool CanInteract => !IsMaxLevel;

        // Recomputed only when the level or affordability changes, so the prompt never allocates
        // per frame and the world badge refreshes at exactly the right moments.
        public override int StateKey => _stateKey;

        /// <summary>"Upgrade - 220c", or what is still missing when the requirements are not met.</summary>
        public override string Prompt => _prompt;

        string _prompt = string.Empty;
        int _stateKey;
        Action _onUpgraded;

        protected override void OnEnable()
        {
            base.OnEnable();
            Apply();            // the world must match the serialised level from the first frame

            // The prompt depends on the level and the inventory and nothing else, so it is rebuilt
            // when the inventory says so rather than polled every frame. An earlier version cached
            // on "can afford" alone and went stale the moment the reason changed - coins arriving
            // while the eggs were still missing left it saying "Need 450 coins".
            Inventory.Changed += Refresh;
            Refresh();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Inventory.Changed -= Refresh;
        }

        /// <summary>True when the player can pay for the next step right now.</summary>
        public bool CanAfford()
        {
            var next = Next;
            if (next == null) return false;
            if (Inventory.Coins < next.coinCost) return false;

            if (next.alsoNeeds != null)
                foreach (var need in next.alsoNeeds)
                    if (!need.InStock) return false;

            return true;
        }

        /// <summary>
        /// Rebuild the prompt. The state key moves only when the wording actually changes, so the
        /// world badge refreshes when the player would see something different and not otherwise.
        /// </summary>
        void Refresh()
        {
            string next = BuildPrompt(CanAfford());
            if (next == _prompt) return;

            _prompt = next;
            _stateKey++;
        }

        /// <summary>Names the first thing standing in the way, so the prompt reads as an instruction.</summary>
        string BuildPrompt(bool affordable)
        {
            var next = Next;
            if (next == null) return string.Empty;
            if (affordable) return "Upgrade to Lv" + (_level + 1);
            if (Inventory.Coins < next.coinCost) return "Need " + next.coinCost + " coins";

            if (next.alsoNeeds != null)
                foreach (var need in next.alsoNeeds)
                    if (!need.InStock)
                        return "Need " + need.count + " " + (need.item != null ? need.item.displayName : "?");

            return "Upgrade";
        }

        [Header("Sound")]
        [Tooltip("Building work - hammering, sawing. Runs UNDER the upgrade beat, so it should be " +
                 "about as long as the action itself.")]
        [SerializeField] SoundEvent buildSound;
        [Tooltip("Fanfare when the work finishes and the new farmhouse is standing there.")]
        [SerializeField] SoundEvent upgradeSound;
        [Tooltip("Victory sting on the final upgrade.")]
        [SerializeField] SoundEvent winSound;

        [Header("Victory beat")]
        [Tooltip("The dance the cat performs when the last upgrade lands. Its duration drives the " +
                 "whole celebration, and its camera framing is what pushes in on the cat.")]
        [SerializeField] CharacterAction victoryAction;

        [Tooltip("Confetti, fired at the cat. Spawned rather than kept in the scene so it cannot " +
                 "be seen sitting idle, and it cleans itself up.")]
        [SerializeField] ParticleSystem confettiPrefab;

        [Tooltip("How far above the cat the confetti bursts from.")]
        [SerializeField] float confettiHeight = 2.6f;

        [Tooltip("What the ambience bed drops to while the fanfare plays, as a fraction of the " +
                 "player's own setting. 0.15 leaves it just audible underneath.")]
        [SerializeField, Range(0f, 1f)] float ambienceDuck = 0.15f;

        [Tooltip("Pause after the camera has pulled back before the stats appear, in seconds. " +
                 "The panel should land on a settled shot, not interrupt the move.")]
        [SerializeField] float pauseBeforePanel = 0.35f;

        /// <summary>Buys the next rung if it is affordable, and refuses audibly if it is not.</summary>
        public override void Interact(PlayerInteractor interactor)
        {
            // The prompt already says what is missing; this is the audible half of that answer,
            // so a player who walks up and presses the button is never met with nothing at all.
            if (!CanAfford()) { AudioManager.PlayDenied(); return; }

            // cached delegate rather than a lambda, the way the sell counter does it
            // The work runs UNDER the animation - the player is swinging a hammer, and the sound
            // has to be happening while they do it rather than reporting it afterwards.
            AudioManager.PlayAt(buildSound, transform.position);

            _onUpgraded ??= Upgraded;
            interactor.Controller.BeginAction(upgradeAction, transform, _onUpgraded);
        }

        void Upgraded()
        {
            var next = Next;

            // re-checked rather than trusted: the beat takes real time, and a truck could have
            // been paid or a recipe started in the middle of it
            if (next == null || !CanAfford()) return;

            Inventory.TrySpend(next.coinCost);
            if (next.alsoNeeds != null)
                foreach (var need in next.alsoNeeds)
                    Inventory.TrySpendProduce(need.item, need.count);

            _level++;
            Apply();
            Refresh();
            Changed?.Invoke(_level);

            // AFTER Apply, which is what swaps the art - so the fanfare lands on the new building
            // appearing rather than a moment before it. Plays on every upgrade INCLUDING the last,
            // where the victory sting stacks on top: the building really did just get built, and
            // swallowing that to make room would lose the payoff of the final tap.
            AudioManager.PlayAt(upgradeSound, transform.position);

            if (IsMaxLevel) Win();
        }

        /// <summary>
        /// Push the current level out to everything that follows it. Called on enable as well as on
        /// upgrade, so the world is never out of step with the number.
        /// </summary>
        void Apply()
        {
            if (expansion != null) expansion.ApplyUpTo(_level);
            if (stove != null) stove.level = _level;
            if (truckQueue != null) truckQueue.level = _level;
            if (cropMenu != null) cropMenu.level = _level;
            if (mushroomPatch != null) mushroomPatch.level = _level;
        }

        /// <summary>
        /// The end of the run, as a sequence rather than a single moment.
        ///
        /// The cat turns to camera and dances, the camera comes in close, confetti goes up and the
        /// fanfare plays over a ducked music bed. Only when all of that has finished and the camera
        /// has settled back does the stats panel appear. Showing the panel up front, which is what
        /// this used to do, threw away the celebration by covering it.
        ///
        /// A coroutine because this is a one-off ordered sequence with waits in it - the thing
        /// coroutines are for - and because it needs no per-frame work of its own between beats.
        /// </summary>
        void Win()
        {
            // The run's time is read HERE, not at the end: the celebration takes five seconds and
            // the player did not spend them farming.
            _finishedAt = Time.timeSinceLevelLoad;

            StartCoroutine(VictorySequence());
        }

        float _finishedAt;

        IEnumerator VictorySequence()
        {
            AudioManager.PlayUI(winSound);   // flat: the run is over, it is not coming from a place

            // under the fanfare for its whole length, then eased back
            float fanfare = 5.8f;
            AudioManager.DuckAmbience(ambienceDuck, fanfare, 1.2f);

            var controller = GameManager.Player;
            var player = GameManager.PlayerTransform;
            var camera = GameManager.CameraTransform;

            if (confettiPrefab != null && player != null)
            {
                var burst = Instantiate(confettiPrefab,
                                        player.position + Vector3.up * confettiHeight,
                                        Quaternion.identity);
                burst.Play();
                // outlives its own emission by the longest particle lifetime, so nothing pops out
                Destroy(burst.gameObject, burst.main.duration + burst.main.startLifetime.constantMax + 1f);
            }

            bool danced = false;
            if (controller != null && victoryAction != null)
            {
                // faceTarget is the CAMERA, so the cat turns out of the world and toward the player
                controller.BeginAction(victoryAction, camera, () => danced = true);

                // unscaled, in case anything else has frozen the clock by now
                float guard = victoryAction.duration + 2f;
                for (float t = 0f; !danced && t < guard; t += Time.unscaledDeltaTime) yield return null;
            }

            // BeginAction's completion fires as the camera STARTS easing back, so wait out the
            // blend before the panel lands on top of a moving shot.
            float settle = (victoryAction != null ? victoryAction.cameraBlend : 0.4f) + pauseBeforePanel;
            for (float t = 0f; t < settle; t += Time.unscaledDeltaTime) yield return null;

            var panel = UIManager.Win;
            if (panel == null) yield break;

            panel.Show(_finishedAt, RunStats.CoinsEarned,
                       RunStats.DishesBaked, RunStats.TruckOrdersFilled);
        }

    }
}
