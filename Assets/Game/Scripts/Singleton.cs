using UnityEngine;

namespace Farm
{
    /// <summary>
    /// Base for the handful of objects there is exactly one of.
    ///
    /// It does NOT search the scene and it does NOT create itself. A manager's whole value is the
    /// references wired into it, so an auto-created stand-in would come up with every slot empty
    /// and turn one missing-from-the-scene mistake into null references scattered across unrelated
    /// systems. Better to fail loudly at the manager than to quietly fake one.
    ///
    /// THE CONTRACT: read Instance from OnEnable or later, never from Awake. Give every concrete
    /// singleton [DefaultExecutionOrder(-100)] - the attribute is not inherited, so it has to go on
    /// each one - and both its Awake and its OnEnable then run before anyone else's, which makes
    /// OnEnable the earliest guaranteed point. In exchange, the accessors on a subclass need not
    /// null-check: a null there is a wiring or ordering mistake and should surface at once.
    ///
    /// Binding happens in Awake AND OnEnable. A domain reload during play wipes statics and re-runs
    /// OnEnable but NOT Awake, so Awake alone would leave Instance null for the rest of the session.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake() => Bind();

        protected virtual void OnEnable() => Bind();

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Bind()
        {
            if (Instance != null && Instance != this)
            {
                // A duplicate is an authoring mistake worth seeing. Destroying the GameObject would
                // take whatever else is on it down with no explanation.
                Debug.LogError(typeof(T).Name + ": a second one exists on '" + name + "'. Keeping the first.", this);
                return;
            }

            Instance = (T)this;
            OnBind();
        }

        /// <summary>Cache anything derived from the wired references here. Runs on every bind.</summary>
        protected virtual void OnBind() { }
    }
}
