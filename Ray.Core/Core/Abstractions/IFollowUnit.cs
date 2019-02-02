﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ray.Core.Abstractions;

namespace Ray.Core
{
    public interface IFollowUnit<K> : IGrainID
    {
        List<Func<K, Task<long>>> GetAllVersionsFunc();
        List<Func<byte[], Task>> GetEventHandlers();
    }
}