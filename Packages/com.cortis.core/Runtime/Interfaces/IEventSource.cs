using Google.Protobuf;
using R3;

namespace Cortis
{
    public interface IEventSource<TEvent>
        where TEvent : IMessage<TEvent>
    {
        Observable<TEvent> Events { get; }
    }
}
