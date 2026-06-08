using System;
using System.Threading.Tasks;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Interface for Native Messaging Host communication with browser extensions
    /// </summary>
    public interface INativeMessagingHost
    {
        /// <summary>
        /// Starts the Native Messaging Host and begins listening for messages
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the Native Messaging Host and closes all connections
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Sends a message to the connected browser extension
        /// </summary>
        /// <param name="message">The message object to send</param>
        Task SendMessageAsync(object message);

        /// <summary>
        /// Gets whether the host is currently running and listening
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Event fired when a message is received from a browser extension
        /// </summary>
        event EventHandler<NativeMessageEventArgs> MessageReceived;

        /// <summary>
        /// Event fired when the connection state changes
        /// </summary>
        event EventHandler<ConnectionStateEventArgs> ConnectionStateChanged;

        /// <summary>
        /// Event fired when an error occurs
        /// </summary>
        event EventHandler<NativeMessagingErrorEventArgs> ErrorOccurred;
    }

    /// <summary>
    /// Event arguments for Native Messaging message received events
    /// </summary>
    public class NativeMessageEventArgs : EventArgs
    {
        public NativeMessage Message { get; }
        public string Browser { get; }
        public DateTime Timestamp { get; }

        public NativeMessageEventArgs(NativeMessage message, string browser = "unknown")
        {
            Message = message;
            Browser = browser;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for connection state change events
    /// </summary>
    public class ConnectionStateEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string Browser { get; }
        public string Message { get; }

        public ConnectionStateEventArgs(bool isConnected, string browser = "unknown", string message = "")
        {
            IsConnected = isConnected;
            Browser = browser;
            Message = message;
        }
    }

    /// <summary>
    /// Event arguments for Native Messaging error events
    /// </summary>
    public class NativeMessagingErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string ErrorType { get; }
        public string Message { get; }

        public NativeMessagingErrorEventArgs(Exception exception, string errorType = "Unknown")
        {
            Exception = exception;
            ErrorType = errorType;
            Message = exception.Message;
        }

        public NativeMessagingErrorEventArgs(string message, string errorType = "Unknown")
        {
            Message = message;
            ErrorType = errorType;
            Exception = new Exception(message);
        }
    }
}