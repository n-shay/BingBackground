using System.Threading.Tasks;
using System.Threading;
using System;

namespace BingBackground
{
    public static class TaskExtensions
    {
        public static async Task<TResult> TimeoutAfter<TResult>(this Func<Task<TResult>> func, TimeSpan timeout)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var task = Task.Run(func, timeoutCancellationTokenSource.Token);
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                
                if (completedTask != task) throw new TimeoutException("The operation has timed out.");

                timeoutCancellationTokenSource.Cancel();

                return await task;  // Very important in order to propagate exceptions
            }
        }
    }
}