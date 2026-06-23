using Microsoft.Extensions.Caching.Memory;
using ZenBotCS.Entities.Models;

namespace ZenBotCS.Helper
{
    /// <summary>
    /// Wraps the in-progress CWL signup state stored in <see cref="IMemoryCache"/> during the
    /// interactive signup wizard. Entries are keyed by the wizard message id and expire via
    /// <see cref="Options.MemoryCacheEntryOptions"/>.
    /// </summary>
    public class CwlSignupCache(IMemoryCache _cache)
    {
        public void Set(ulong messageId, CwlSignup signup)
            => _cache.Set(messageId, signup, Options.MemoryCacheEntryOptions);

        public CwlSignup? Get(ulong messageId)
            => _cache.Get(messageId) as CwlSignup;

        /// <summary>
        /// Applies <paramref name="update"/> to the cached signup for <paramref name="messageId"/>
        /// and writes it back. Returns false when no signup is cached for that message.
        /// </summary>
        public bool TryUpdate(ulong messageId, Action<CwlSignup> update)
        {
            if (!_cache.TryGetValue(messageId, out CwlSignup? signup) || signup is null)
                return false;

            update(signup);
            _cache.Set(messageId, signup, Options.MemoryCacheEntryOptions);
            return true;
        }
    }
}
