using System;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using R3;
using UnityEngine;

namespace Cortis
{
    public static class MessageBinding
    {
        public static Binder Bind<TCommand, TEvent>(
            ICommandHandler<TCommand> handler,
            IEventSource<TEvent> source,
            IMessageGateway gateway)
            where TCommand : IMessage<TCommand>, new()
            where TEvent : IMessage<TEvent>, new()
        {
            return new Binder(
                SubscribeCommand(handler, gateway),
                SubscribeEvent(source, gateway));
        }

        public static Binder Bind<TCommand>(
            ICommandHandler<TCommand> handler,
            IMessageGateway gateway)
            where TCommand : IMessage<TCommand>, new()
        {
            return new Binder(
                SubscribeCommand(handler, gateway));
        }

        public static Binder Bind<TEvent>(
            IEventSource<TEvent> source,
            IMessageGateway gateway)
            where TEvent : IMessage<TEvent>, new()
        {
            return new Binder(
                SubscribeEvent(source, gateway));
        }

        static IDisposable SubscribeCommand<TCommand>(
            ICommandHandler<TCommand> handler,
            IMessageGateway gateway)
            where TCommand : IMessage<TCommand>, new()
        {
            var descriptor = new TCommand().Descriptor;
            var typeName = typeof(TCommand).Name;

            return gateway.Messages
                .Where(any => any.Is(descriptor))
                .Select(any =>
                {
                    try
                    {
                        return any.Unpack<TCommand>();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[MessageBinding<{typeName}>] Failed to unpack: {e}");
                        return default;
                    }
                })
                .Where(msg => msg != null)
                .Subscribe(msg =>
                {
                    try { handler.Handle(msg); }
                    catch (Exception e) { Debug.LogError($"[MessageBinding<{typeName}>] Command error: {e}"); }
                });
        }

        static IDisposable SubscribeEvent<TEvent>(
            IEventSource<TEvent> source,
            IMessageGateway gateway)
            where TEvent : IMessage<TEvent>, new()
        {
            var typeName = typeof(TEvent).Name;

            return source.Events
                .DistinctUntilChanged()
                .Select(evt => Any.Pack(evt))
                .Subscribe(evt =>
                {
                    try { gateway.Send(evt); }
                    catch (Exception e) { Debug.LogError($"[MessageBinding<{typeName}>] Event error: {e}"); }
                });
        }
    }
}
