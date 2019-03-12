using System;
using System.Collections.Generic;
using System.Text;

namespace KupaRPC
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MethodAttribute : Attribute
    {
        public readonly ushort MethodID;

        public MethodAttribute(ushort methodID)
        {
            MethodID = methodID;
        }
    }
}
