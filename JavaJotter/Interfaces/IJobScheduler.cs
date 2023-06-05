namespace JavaJotter.Interfaces;

public interface IJobScheduler
{
    Task Start();

    Task Stop();

}