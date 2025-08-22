using Splitio.Services.Logger;
using System;

namespace ProviderTests
{
    public class CustomLogger : ISplitLogger
    {
        public void Debug(string message, Exception exception)
        {
            Console.WriteLine($"DEBUG: {message}: {exception}");
        }

        public void Debug(string message) {
            Console.WriteLine($"DEBUG: {message}");
        }

        public void Error(string message, Exception exception) {
            Console.WriteLine($"ERROR: {message}: {exception}");
        }

        public void Error(string message) {
            Console.WriteLine($"DEBUG: {message}");
        }

        public void Info(string message, Exception exception) {
            Console.WriteLine($"INFO: {message}: {exception}");
        }

        public void Info(string message) {
            Console.WriteLine($"INFO: {message}");
        }

        public void Trace(string message, Exception exception) {
            Console.WriteLine($"TRACE: {message}: {exception}");
        }

        public void Trace(string message) {
            Console.WriteLine($"TRACE: {message}");
        }

        public void Warn(string message, Exception exception) {
            Console.WriteLine($"WARN: {message}: {exception}");
        }

        public void Warn(string message) {
            Console.WriteLine($"WARN: {message}");
        }

        public bool IsDebugEnabled { get { return true; } }
    }
}
