using System;

namespace LoqNova.Lib.Extensions;

public static class TimeExtensions
{
    public static Time UtcNow
    {
        get
        {
            var utcNow = DateTime.UtcNow;
            return new(utcNow.Hour, utcNow.Minute);
        }
    }
}
