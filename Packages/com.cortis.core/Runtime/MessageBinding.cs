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

        // ---- Routed overloads ----

        /// <summary>
        /// Command + Event をルーティング付きでバインドする。
        /// Gateway は root 型で通信し、unwrap/wrap 関数で inner 型と変換する。
        /// </summary>
        public static Binder BindRouted<TRootCommand, TCommand, TRootEvent, TEvent>(
            ICommandHandler<TCommand> handler,
            IEventSource<TEvent> source,
            IMessageGateway gateway,
            Func<TRootCommand, TCommand> unwrapCommand,
            Func<TEvent, TRootEvent> wrapEvent)
            where TRootCommand : IMessage<TRootCommand>, new()
            where TCommand : IMessage<TCommand>
            where TRootEvent : IMessage<TRootEvent>, new()
            where TEvent : IMessage<TEvent>
        {
            return new Binder(
                SubscribeRoutedCommand(handler, gateway, unwrapCommand),
                SubscribeRoutedEvent(source, gateway, wrapEvent));
        }

        /// <summary>
        /// Command のみをルーティング付きでバインドする。
        /// </summary>
        public static Binder BindRouted<TRootCommand, TCommand>(
            ICommandHandler<TCommand> handler,
            IMessageGateway gateway,
            Func<TRootCommand, TCommand> unwrapCommand)
            where TRootCommand : IMessage<TRootCommand>, new()
            where TCommand : IMessage<TCommand>
        {
            return new Binder(
                SubscribeRoutedCommand(handler, gateway, unwrapCommand));
        }

        /// <summary>
        /// Event のみをルーティング付きでバインドする。
        /// </summary>
        public static Binder BindRouted<TRootEvent, TEvent>(
            IEventSource<TEvent> source,
            IMessageGateway gateway,
            Func<TEvent, TRootEvent> wrapEvent)
            where TRootEvent : IMessage<TRootEvent>, new()
            where TEvent : IMessage<TEvent>
        {
            return new Binder(
                SubscribeRoutedEvent(source, gateway, wrapEvent));
        }

        static IDisposable SubscribeRoutedCommand<TRootCommand, TCommand>(
            ICommandHandler<TCommand> handler,
            IMessageGateway gateway,
            Func<TRootCommand, TCommand> unwrapCommand)
            where TRootCommand : IMessage<TRootCommand>, new()
            where TCommand : IMessage<TCommand>
        {
            var descriptor = new TRootCommand().Descriptor;
            var typeName = typeof(TCommand).Name;

            return gateway.Messages
                .Where(any => any.Is(descriptor))
                .Select(any =>
                {
                    try
                    {
                        return any.Unpack<TRootCommand>();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[MessageBinding<{typeName}>] Failed to unpack root: {e}");
                        return default;
                    }
                })
                .Where(msg => msg != null)
                .Select(root =>
                {
                    try
                    {
                        return unwrapCommand(root);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[MessageBinding<{typeName}>] Failed to unwrap command: {e}");
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

        static IDisposable SubscribeRoutedEvent<TRootEvent, TEvent>(
            IEventSource<TEvent> source,
            IMessageGateway gateway,
            Func<TEvent, TRootEvent> wrapEvent)
            where TRootEvent : IMessage<TRootEvent>, new()
            where TEvent : IMessage<TEvent>
        {
            var typeName = typeof(TEvent).Name;

            return source.Events
                .DistinctUntilChanged()
                .Select(evt =>
                {
                    try
                    {
                        return Any.Pack(wrapEvent(evt));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[MessageBinding<{typeName}>] Failed to wrap event: {e}");
                        return null;
                    }
                })
                .Where(any => any != null)
                .Subscribe(evt =>
                {
                    try { gateway.Send(evt); }
                    catch (Exception e) { Debug.LogError($"[MessageBinding<{typeName}>] Event error: {e}"); }
                });
        }
    }
}
