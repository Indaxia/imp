using System;

namespace imp
{
    public class PackageException: Exception
    {
        public PackageException()
        {
        }

        public PackageException(string message): base(message)
        {
        }
    }
}