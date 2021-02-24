using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AT.Geometry.Objects
{
    public class Point
    {
        public decimal X = 0M;
        public decimal Y = 0M;
    }

    [MessagePackObject]
    public class Line
    {
        [Key(0)]
        public decimal Slope = 0M;

        [Key(1)]
        public decimal YIntercept = 0M;
    }
}
