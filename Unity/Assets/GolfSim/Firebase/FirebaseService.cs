namespace GolfSimZA.Firebase
{
    /// <summary>
    /// Application-level boundary for Firebase.
    /// Firebase Unity SDK packages and project credentials are intentionally
    /// configured locally and are never committed to source control.
    /// </summary>
    public sealed class FirebaseService
    {
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            // TODO: Initialize Firebase Unity SDK and validate dependencies.
            IsInitialized = false;
        }
    }
}
