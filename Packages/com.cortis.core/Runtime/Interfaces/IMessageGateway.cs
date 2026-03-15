using Google.Protobuf.WellKnownTypes;
using R3;

namespace Cortis
{
    public interface IMessageGateway
    {
        Observable<Any> Messages { get; }
        void Send(Any packed);
    }
}
