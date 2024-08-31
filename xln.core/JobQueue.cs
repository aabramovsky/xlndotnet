using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace xln.core
{
  public interface ITask
  {
    Task ExecuteAsync();
  }

  public class JobQueue
  {
    private readonly BlockingCollection<ITask> _tasks = new BlockingCollection<ITask>();
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private Task _processingTask;

    public JobQueue()
    {
      _processingTask = Task.Run(ProcessTasks);
    }

    public void EnqueueTask(ITask task)
    {
      _tasks.Add(task);
    }

    private async Task ProcessTasks()
    {
      while (!_cts.Token.IsCancellationRequested)
      {
        try
        {
          var task = _tasks.Take(_cts.Token);
          await task.ExecuteAsync();
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error processing task in JobQueue {ex.Message}");
        }
      }
    }

    public async Task StopAsync()
    {
      _cts.Cancel();
      _tasks.CompleteAdding();
      await _processingTask;
    }
  }

  // Менеджер JobQueue
  public class JobQueueManager
  {
    private readonly ConcurrentDictionary<string, JobQueue> _jobQueues = new ConcurrentDictionary<string, JobQueue>();

    public JobQueue GetOrCreateJobQueue(string id)
    {
      return _jobQueues.GetOrAdd(id, _ => new JobQueue());
    }

    public void EnqueueTask(string jobQueueId, ITask task)
    {
      var jobQueue = GetOrCreateJobQueue(jobQueueId);
      jobQueue.EnqueueTask(task);
    }

    public async Task StopAllQueuesAsync()
    {
      var stopTasks = new List<Task>();
      foreach (var jobQueue in _jobQueues.Values)
      {
        stopTasks.Add(jobQueue.StopAsync());
      }
      await Task.WhenAll(stopTasks);
    }
  }
}