namespace SharedKernel.Interfaces
{
    public interface IPublisher<T>
    {
        Task PublishAsync(T item,CancellationToken cancellationToken);
    }
}
