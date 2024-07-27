namespace System.Net.Sockets
{
    public static class StreamExtension
    {
        /// <summary>
        /// Extension method of the ReadAsync to support timeouts, as sometimes the read hangs when waiting remote servers to send data
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="buffer"></param>
        /// <param name="timeoutInSeconds"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="TimeoutException"></exception>
        public static async Task<int> ReadAsyncWithTimeout(this Stream stream, Memory<byte> buffer, int timeoutInSeconds, CancellationToken cancellationToken)
        {
            // Create a task that completes after a specified timeout period
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutInSeconds), cancellationToken);

            // Create a task to perform the read operation
            Task<int> readTask = stream.ReadAsync(buffer, cancellationToken).AsTask();

            // Wait for either the read operation or the timeout to complete
            Task completedTask = await Task.WhenAny(readTask, timeoutTask);

            // If the timeout task completed first, throw a TimeoutException
            if (completedTask == timeoutTask)
                throw new TimeoutException($"The read operation has timed out after {timeoutInSeconds} seconds");

            // Otherwise, return the result of the read operation
            return await readTask;
        }
    }
}
