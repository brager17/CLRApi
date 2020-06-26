using System.Threading.Tasks;

namespace Immediate
{
    public class TaskCompletionSourceResettable
    {
        private TaskCompletionSource<bool> _taskCompletionSource = new TaskCompletionSource<bool>();
        public Task<bool> Task => _taskCompletionSource.Task;

        public void SetResultTrue() => _taskCompletionSource.SetResult(true);
        public void SetResultFalse() => _taskCompletionSource.SetResult(false);

        public void Reset() => _taskCompletionSource = new TaskCompletionSource<bool>();
    }
}