using System;

namespace imp
{
    public class ModuleException: Exception
    {
        public ModuleException()
        {
        }

        public ModuleException(string message): base(message)
        {
        }
    }
}