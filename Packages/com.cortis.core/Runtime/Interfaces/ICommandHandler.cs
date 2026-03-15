using Google.Protobuf;

namespace Cortis
{
    public interface ICommandHandler<TCommand>
        where TCommand : IMessage<TCommand>
    {
        void Handle(TCommand command);
    }
}
