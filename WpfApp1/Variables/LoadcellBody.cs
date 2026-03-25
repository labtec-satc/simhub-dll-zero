using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfApp1.Variables
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LoadcellBody
    {
        public byte handbrakeForce;
        public byte brakeForce;
        public byte clutchForce;
        public byte throttleForce;
    }
}
