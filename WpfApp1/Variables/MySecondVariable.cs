using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfApp1.Variables
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MySecondVariable
    {
        public Header header;
        public Body2 body;
        public Footer footer;
    }
}
