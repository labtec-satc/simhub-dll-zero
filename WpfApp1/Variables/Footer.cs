using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfApp1.Variables
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Footer
    {
        public byte endFooter01;
        public byte endFooter02;
        public UInt16 checkSum;
    }
}
