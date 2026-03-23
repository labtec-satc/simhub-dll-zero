using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfApp1.Variables
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Header
    {
        public byte startHeader01;
        public byte startHeader02;
    }
}
