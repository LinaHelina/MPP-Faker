﻿using System;
using System.Collections.Generic;
using System.Text;

namespace FakerLibrary.Generators
{
    class IntGenerator : Generator<int>
    {
        private Random rand = new Random();
        public override int Generate()
        {
            return rand.Next();
        }
    }
}
