namespace SharedKernel.Interfaces
{
    public interface ISubscriber<T>
    {
        Task SubscribeAsync(CancellationToken cancellationToken);
    }
}
