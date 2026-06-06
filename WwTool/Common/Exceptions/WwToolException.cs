using System;

namespace WwTool.Common.Exceptions
{
    public class WwToolException : Exception
    {
        public WwToolException() : base() { }

        public WwToolException(string message) : base(message) { }

        public WwToolException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class WwToolApiException : WwToolException
    {
        public WwToolApiException() : base() { }
        public WwToolApiException(string message) : base(message) { }
        public WwToolApiException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class WwToolDatabaseException : WwToolException
    {
        public WwToolDatabaseException() : base() { }
        public WwToolDatabaseException(string message) : base(message) { }
        public WwToolDatabaseException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class WwToolConfigException : WwToolException
    {
        public WwToolConfigException() : base() { }
        public WwToolConfigException(string message) : base(message) { }
        public WwToolConfigException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class WwToolGamePathException : WwToolException
    {
        public WwToolGamePathException() : base() { }
        public WwToolGamePathException(string message) : base(message) { }
        public WwToolGamePathException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class WwToolAuthException : WwToolException
    {
        public WwToolAuthException() : base() { }
        public WwToolAuthException(string message) : base(message) { }
        public WwToolAuthException(string message, Exception innerException) : base(message, innerException) { }
    }
}
