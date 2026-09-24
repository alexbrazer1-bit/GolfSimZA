using System;

namespace GolfSimZA.Firebase
{
    public interface IFirebaseBackend
    {
        bool IsInitialized { get; }
        void Initialize(Action<bool> completed);
    }
}
