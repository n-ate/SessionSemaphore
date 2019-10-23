using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Web.SessionState;

namespace Application
{
    public static class SessionSemaphoreExtensions
    {
        public static SessionSemaphore ObtainCrossServerLock(this HttpSessionState session, string sessionKey)
        {
            return SessionSemaphore.ObtainCrossServerLock(session, sessionKey);
        }
    }

    public class SessionSemaphore : IDisposable
    {
        private static readonly ConcurrentDictionary<string, object> _singleServerLocks = new ConcurrentDictionary<string, object>();
        private static readonly string SEMAPHORE_PREFIX = "__SEMAPHORE:";
        private string identity;
        private bool released = false;
        private string semaphoreKey;
        private HttpSessionState session;

        private SessionSemaphore(string identity, string semaphoreKey, HttpSessionState session)
        {
            this.identity = identity;
            this.semaphoreKey = semaphoreKey;
            this.session = session;
        }

        public void Dispose()
        {
            Release();//always release
        }

        public void Release()
        {
            if (released) return;
            ReleaseCrossServerLock(session, semaphoreKey, identity);
            released = true;
        }

        internal static SessionSemaphore ObtainCrossServerLock(HttpSessionState session, string sessionKey)
        {
            SessionSemaphore semaphore = null;
            var identity = Guid.NewGuid().ToString("N");//compute identity before locking
            string semaphoreKey = SEMAPHORE_PREFIX + sessionKey;//compute key before locking
            lock (_singleServerLocks.GetOrAdd(sessionKey, new object()))//one thread per server per session key will pass beyond this point
            {
                do
                {
                    while (session[semaphoreKey] != null) Thread.Sleep(1);
                    session[semaphoreKey] = identity;
                    Thread.Sleep(1);
                    if (session[semaphoreKey].ToString() == identity) semaphore = new SessionSemaphore(identity, semaphoreKey, session);
                } while (semaphore == null);
            }
            return semaphore;
        }

        private static void ReleaseCrossServerLock(HttpSessionState session, string semaphoreKey, string identity)
        {
            if (session[semaphoreKey].ToString() == identity) session[semaphoreKey] = null;
        }
    }
}
