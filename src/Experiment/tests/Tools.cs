using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests
{
    class Tools
    {
        public static void BlockForResult(in ValueTask task)
        {
            if (task.IsCompleted)
            {
                task.GetAwaiter().GetResult();
            }
            else
            {
                task.AsTask().GetAwaiter().GetResult();
            }
        }

        public static T BlockForResult<T>(in ValueTask<T> task)
        {
            return task.IsCompleted ? task.GetAwaiter().GetResult() : task.AsTask().GetAwaiter().GetResult();
        }
    }
}
