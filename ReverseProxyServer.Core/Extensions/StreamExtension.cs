using System.Threading;

namespace System.Net.Sockets
{
    public static class StreamExtension
    {
        public static async Task<int> ReadAsyncWithTimeout(this Stream stream, Memory<byte> buffer, int timeout, CancellationToken cancellationToken)
        {
            // Create a task that completes after a specified timeout period
            var timeoutTask = Task.Delay(timeout, cancellationToken);

            // Create a task to perform the read operation
            var readTask = stream.ReadAsync(buffer, cancellationToken).AsTask();

            // Wait for either the read operation or the timeout to complete
            var completedTask = await Task.WhenAny(readTask, timeoutTask);

            // If the timeout task completed first, throw a TimeoutException
            if (completedTask == timeoutTask)
                throw new TimeoutException("The read operation has timed out.");

            // Otherwise, return the result of the read operation
            return await readTask;


        }
    }
}
