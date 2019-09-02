﻿using BinarySerialization;
using Texnomic.DNS.Models;

namespace Texnomic.DNS.Records
{
    public class CName : IRecord
    {
        [FieldOrder(0)]
        public Domain Domain { get; set; }
    }
}
