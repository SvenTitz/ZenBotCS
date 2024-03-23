using Microsoft.Extensions.Caching.Memory;

namespace ZenBotCS;

public static class Options
{
    public static MemoryCacheEntryOptions MemoryCacheEntryOptions => new MemoryCacheEntryOptions()
                                                                    .SetSlidingExpiration(TimeSpan.FromHours(1));
}
