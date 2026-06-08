using System;
using System.Threading.Tasks;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Interface for managing Native Messaging host registration in Windows registry
    /// </summary>
    public interface INativeMessagingRegistrar
    {
        /// <summary>
        /// Registers the Native Messaging host for all supported browsers
        /// </summary>
        Task RegisterHostAsync();

        /// <summary>
        /// Unregisters the Native Messaging host from all browsers
        /// </summary>
        Task UnregisterHostAsync();

        /// <summary>
        /// Updates the host path in registry (useful when app is moved)
        /// </summary>
        /// <param name="newPath">New path to the executable</param>
        Task UpdateHostPathAsync(string newPath);

        /// <summary>
        /// Checks if the host is currently registered
        /// </summary>
        bool IsRegistered { get; }

        /// <summary>
        /// Gets the current registered host path
        /// </summary>
        string? RegisteredHostPath { get; }

        /// <summary>
        /// Event fired when registration state changes
        /// </summary>
        event EventHandler<RegistrationStateEventArgs> RegistrationStateChanged;

        /// <summary>
        /// Event fired when a registration error occurs
        /// </summary>
        event EventHandler<RegistrationErrorEventArgs> RegistrationError;
    }

    /// <summary>
    /// Event arguments for registration state changes
    /// </summary>
    public class RegistrationStateEventArgs : EventArgs
    {
        public bool IsRegistered { get; }
        public string Browser { get; }
        public string Message { get; }

        public RegistrationStateEventArgs(bool isRegistered, string browser, string message = "")
        {
            IsRegistered = isRegistered;
            Browser = browser;
            Message = message;
        }
    }

    /// <summary>
    /// Event arguments for registration errors
    /// </summary>
    public class RegistrationErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string Browser { get; }
        public string Operation { get; }
        public string Message { get; }

        public RegistrationErrorEventArgs(Exception exception, string browser, string operation)
        {
            Exception = exception;
            Browser = browser;
            Operation = operation;
            Message = exception.Message;
        }

        public RegistrationErrorEventArgs(string message, string browser, string operation)
        {
            Message = message;
            Browser = browser;
            Operation = operation;
            Exception = new Exception(message);
        }
    }
}