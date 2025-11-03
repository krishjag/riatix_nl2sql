using   System.Threading.Channels;

namespace Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Queue
{
    public class QueryLogQueue : IQueryLogQueue
    {
        private readonly Channel<QueryLog> _channel;

        public QueryLogQueue(int capacity = 5000)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite
            };
            _channel = Channel.CreateBounded<QueryLog>(options);
        }

        public ValueTask EnqueueAsync(QueryLog log) => _channel.Writer.WriteAsync(log);

        public async IAsyncEnumerable<QueryLog> DequeueAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_channel.Reader.TryRead(out var log))
                    yield return log;
            }
        }
    }
}
