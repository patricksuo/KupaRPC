using System;
using System.Collections.Generic;
using System.Text;

namespace KupaRPC
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class ServiceAttribute : Attribute
    {
        public readonly ushort ServiceID;

        public ServiceAttribute(ushort serviceID)
        {
            ServiceID = serviceID;
        }
    }

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
