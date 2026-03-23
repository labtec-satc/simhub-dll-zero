using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfApp1.Variables
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Body
    {
        public UInt16 force;
        public UInt16 distance;
        public byte abs;

    }
}
